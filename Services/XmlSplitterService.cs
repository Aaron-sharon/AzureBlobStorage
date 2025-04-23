using System.Diagnostics;
using System.Xml;

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

            foreach (XmlNode invoiceNode in invoiceNodes)
            {
                var invoiceDoc = new XmlDocument();
                if (xmlDoc.FirstChild is XmlDeclaration declaration)
                {
                    invoiceDoc.AppendChild(invoiceDoc.CreateXmlDeclaration(declaration.Version, declaration.Encoding, declaration.Standalone));
                }

                var clonedInvoice = invoiceDoc.ImportNode(invoiceNode, true);
                invoiceDoc.AppendChild(clonedInvoice);

                // Generate filename and upload blob
                string blobName = $"invoice-{counter}-{Guid.NewGuid():N}.xml";
                createdFiles.Add(blobName);
                counter++;

                using (var invoiceStream = new MemoryStream())
                {
                    invoiceDoc.Save(invoiceStream);
                    invoiceStream.Position = 0;
                    await _blobService.UploadFileAsync(blobName, invoiceStream);  // Upload the invoice to blob storage
                }

                // Enqueue the blob name
                await _queueService.SendMessageAsync(blobName);
            }

            _logger.LogInformation($"Processed and uploaded {createdFiles.Count} invoices.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error processing XML: {ex.Message}");
            throw;
        }

        return createdFiles;  // Return the list of created files
    }

}
