using System.Diagnostics;
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
        int batchNumber = 1;
        List<string> currentBatchFiles = new List<string>();
        var batchStopwatch = new Stopwatch();
        batchStopwatch.Start();

        try
        {
            XmlReaderSettings readerSettings = new XmlReaderSettings
            {
                Async = true
            };

            using (XmlReader reader = XmlReader.Create(xmlStream, readerSettings))
            {
                while (await reader.ReadAsync())
                {
                    if (reader.IsStartElement() && reader.LocalName == "Invoice")
                    {
                        counter++;
                        DateTime now = DateTime.UtcNow;
                        string folderPath = $"{now:yyyy}/{now:MM}/{now:dd}/invoice-group-0";
                        string fileName = $"invoice-{counter}-{Guid.NewGuid():N}.xml";
                        string blobName = $"{folderPath}/{fileName}";

                        using (var memoryStream = new MemoryStream())
                        {
                            // Configure XmlWriter with Async enabled
                            XmlWriterSettings writerSettings = new XmlWriterSettings
                            {
                                Async = true,
                                Indent = true,
                                Encoding = new System.Text.UTF8Encoding(false) // No BOM
                            };

                            using (XmlWriter writer = XmlWriter.Create(memoryStream, writerSettings))
                            {
                                await writer.WriteStartDocumentAsync();
                                await writer.WriteNodeAsync(reader, true);
                                await writer.WriteEndDocumentAsync();
                                await writer.FlushAsync();
                            }

                            memoryStream.Position = 0;
                            await _blobService.UploadFileAsync(blobName, memoryStream);
                        }

                        createdFiles.Add(blobName);
                        currentBatchFiles.Add(blobName);

                        // Check for batch size (1000 invoices per batch)
                        if (counter % 1000 == 0)
                        {
                            _logger.LogInformation($"Batch {batchNumber} completed. Time taken: {batchStopwatch.Elapsed.TotalSeconds:N2} seconds. Files: {currentBatchFiles.Count}");

                            batchNumber++;
                            currentBatchFiles.Clear();
                            batchStopwatch.Restart();
                        }
                    }
                }

                // Handle the remaining files in the last batch
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
}
