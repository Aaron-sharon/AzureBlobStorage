using System.Diagnostics;
using System.Text.Json;
using System.Text;
using System.Xml;




public class XmlSplitterService
{
    private readonly AzureBlobService _blobService;
    private readonly ILogger<XmlSplitterService> _logger;

    public XmlSplitterService(AzureBlobService blobService, ILogger<XmlSplitterService> logger)
    {
        _blobService = blobService;
        _logger = logger;
    }

    public async Task<List<string>> SplitAndStoreInvoicesAsync(Stream xmlStream)
    {
        var createdFiles = new List<string>();
        int counter = 0;
        int batchNumber = 0;
        List<string> currentBatchFiles = new List<string>();
        var batchStopwatch = new Stopwatch();
        batchStopwatch.Start();

        try
        {
            XmlReaderSettings readerSettings = new XmlReaderSettings { Async = true };

            using (XmlReader reader = XmlReader.Create(xmlStream, readerSettings))
            {
                while (await reader.ReadAsync())
                {
                    if (reader.IsStartElement() && reader.LocalName == "Invoice")
                    {
                        counter++;
                        DateTime now = DateTime.UtcNow;
                        string folderPath = $"{now.Year}/{now.Month}/{now.Day}/invoice-group-{batchNumber}";
                        string fileName = $"invoice-{counter}-{Guid.NewGuid():N}.json";
                        string blobName = $"{folderPath}/{fileName}";

                        using (var invoiceSubtree = reader.ReadSubtree())
                        {
                            invoiceSubtree.Read(); // Move to root element
                            XmlDocument xmlDoc = new XmlDocument();
                            xmlDoc.Load(invoiceSubtree);

                            string jsonContent = ConvertXmlToJson(xmlDoc);
                            byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonContent);

                            using (var jsonStream = new MemoryStream(jsonBytes))
                            {
                                await _blobService.UploadFileAsync(blobName, jsonStream);
                            }

                            createdFiles.Add(blobName);
                            currentBatchFiles.Add(blobName);

                            if (counter % 1000 == 0)
                            {
                                _logger.LogInformation($"Batch {batchNumber} completed. Time taken: {batchStopwatch.Elapsed.TotalSeconds:N2} seconds. Files: {currentBatchFiles.Count}");
                                batchNumber++;
                                currentBatchFiles.Clear();
                                batchStopwatch.Restart();
                            }
                        }
                    }
                }

                if (currentBatchFiles.Count > 0)
                {
                    _logger.LogInformation($"Final Batch {batchNumber} completed. Time taken: {batchStopwatch.Elapsed.TotalSeconds:N2} seconds. Files: {currentBatchFiles.Count}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error processing XML: {ex.Message}");
            throw;
        }

        return createdFiles;
    }


    private string ConvertXmlToJson(XmlDocument xmlDoc)
    {
        using var outputStream = new MemoryStream();
        using var writer = new Utf8JsonWriter(outputStream, new JsonWriterOptions
        {
            Indented = true,
            SkipValidation = true
        });

        using var xmlReader = XmlReader.Create(new StringReader(xmlDoc.OuterXml));
        WriteInvoiceToJson(xmlReader, writer, GetInvoiceNumber(xmlDoc));

        writer.Flush();
        return Encoding.UTF8.GetString(outputStream.ToArray());
    }

    private string GetInvoiceNumber(XmlDocument xmlDoc)
    {
        var node = xmlDoc.SelectSingleNode("//InvoiceNumber");
        return node?.InnerText?.Trim().Replace(" ", "_").Replace("/", "-") ?? "Unknown_Invoice";
    }

    private void WriteCustomerToJson(XmlReader reader, Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && !reader.IsEmptyElement)
            {
                string elementName = reader.LocalName;
                if (reader.Read() && reader.NodeType == XmlNodeType.Text)
                {
                    writer.WriteString(elementName, reader.Value.Trim());
                }
            }
        }
        writer.WriteEndObject();
    }

    private void WriteItemsToJson(XmlReader reader, Utf8JsonWriter writer)
    {
        writer.WriteStartArray();
        while (reader.ReadToFollowing("Item"))
        {
            using var itemReader = reader.ReadSubtree();
            itemReader.Read();
            writer.WriteStartObject();

            while (itemReader.Read())
            {
                if (itemReader.NodeType == XmlNodeType.Element && !itemReader.IsEmptyElement)
                {
                    string elementName = itemReader.LocalName;
                    if (itemReader.Read() && itemReader.NodeType == XmlNodeType.Text)
                    {
                        writer.WriteString(elementName, itemReader.Value.Trim());
                    }
                }
            }

            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }

    private void WriteInvoiceToJson(XmlReader reader, Utf8JsonWriter writer, string invoiceNumber)
    {
        writer.WriteStartObject();
        writer.WriteString("InvoiceNumber", invoiceNumber);

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && !reader.IsEmptyElement)
            {
                string elementName = reader.LocalName;

                if (elementName == "InvoiceNumber")
                {
                    reader.Skip();
                    continue;
                }

                if (elementName == "Customer")
                {
                    writer.WritePropertyName(elementName);
                    WriteCustomerToJson(reader.ReadSubtree(), writer);
                    continue;
                }

                if (elementName == "Items")
                {
                    writer.WritePropertyName(elementName);
                    WriteItemsToJson(reader.ReadSubtree(), writer);
                    continue;
                }

                if (reader.Read() && reader.NodeType == XmlNodeType.Text)
                {
                    writer.WriteString(elementName, reader.Value.Trim());
                }
            }
        }

        writer.WriteEndObject();
    }
}















