using Newtonsoft.Json;
using System.Diagnostics;
using System.Text.Json;
using System.Xml;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

public class XmlSplitterService
{
    private readonly AzureBlobService _blobService;
    private readonly AzureQueueService _queueService;
    private readonly ILogger<XmlSplitterService> _logger;

    public XmlSplitterService(AzureBlobService blobService, AzureQueueService queueService, ILogger<XmlSplitterService> logger)
    {
        _blobService = blobService;
        _queueService = queueService;
        _logger = logger;
    }

    // New method: Split and Store Invoices
    public async Task<List<string>> SplitAndStoreInvoicesAsync(Stream xmlStream)
    {
        var createdFiles = new List<string>(); // To store the names of the created blobs

        XmlNodeList invoiceNodes;
        XmlDocument xmlDoc;

        var splittingStopwatch = Stopwatch.StartNew(); // ⏱️ Start timing before split

        try
        {
            xmlDoc = new XmlDocument();
            xmlDoc.Load(xmlStream);

            invoiceNodes = xmlDoc.SelectNodes("/Invoices/Invoice");

            if (invoiceNodes == null || invoiceNodes.Count == 0)
                throw new Exception("No <Invoice> elements found under <Invoices>");

            splittingStopwatch.Stop(); // ⏱️ Stop timing right after splitting/parsing
            _logger.LogInformation($"🕒 XML splitting only took {splittingStopwatch.Elapsed.TotalSeconds:N2} seconds to extract {invoiceNodes.Count} invoices.");

            int counter = 1;

            // Start total JSON conversion timer
            var totalJsonConversionStopwatch = Stopwatch.StartNew();

            foreach (XmlNode invoiceNode in invoiceNodes)
            {
                var invoiceDoc = new XmlDocument();
                if (xmlDoc.FirstChild is XmlDeclaration declaration)
                {
                    invoiceDoc.AppendChild(invoiceDoc.CreateXmlDeclaration(declaration.Version, declaration.Encoding, declaration.Standalone));
                }

                var clonedInvoice = invoiceDoc.ImportNode(invoiceNode, true);
                invoiceDoc.AppendChild(clonedInvoice);

                // Generate filename and upload blob for JSON
                DateTime now = DateTime.UtcNow;
                string folderPath = $"{now:yyyy}/{now:MM}/{now:dd}/invoice-group-0";
                string fileName = $"invoice-{counter}-{Guid.NewGuid():N}.json"; // Change file extension to .json
                string blobName = $"{folderPath}/{fileName}";

                createdFiles.Add(blobName);

                using (var invoiceStream = new MemoryStream())
                {
                    // Convert XML to JSON using the provided conversion logic
                    string jsonContent = ConvertXmlToJson(invoiceDoc);

                    // Create a memory stream for the JSON content
                    var jsonStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonContent));
                    await _blobService.UploadFileAsync(blobName, jsonStream);  // Upload the JSON to blob storage
                }

                await _queueService.SendMessageAsync(blobName);

                counter++;
            }

            // Stop and log total conversion time
            totalJsonConversionStopwatch.Stop();
            Console.WriteLine($"🟢 Total time taken to convert all invoices to JSON: {totalJsonConversionStopwatch.Elapsed.TotalSeconds:N2} seconds.");

            _logger.LogInformation($"Processed and uploaded {createdFiles.Count} invoices.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error processing XML: {ex.Message}");
            throw;
        }

        return createdFiles;  // Return the list of created files
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
        return System.Text.Encoding.UTF8.GetString(outputStream.ToArray());
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
        writer.WriteString("InvoiceNumber", invoiceNumber); // Write explicitly

        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && !reader.IsEmptyElement)
            {
                string elementName = reader.LocalName;

                if (elementName == "InvoiceNumber")
                {
                    reader.Skip(); // already handled
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






    //Method to convert XML Document to JSON string
    //private string ConvertXmlToJson(XmlDocument xmlDoc)
    //{
    //    using var stringWriter = new StringWriter();
    //    using var jsonWriter = new JsonTextWriter(stringWriter)
    //    {
    //        Formatting = Newtonsoft.Json.Formatting.Indented
    //    };

    //    // Convert the XML to JSON
    //    JsonSerializer serializer = new JsonSerializer();
    //    serializer.Serialize(jsonWriter, xmlDoc.DocumentElement);
    //    return stringWriter.ToString();
    //}

}
