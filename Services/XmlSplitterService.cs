using System.Xml;

public class XmlSplitterService
{
    private readonly AzureBlobService _blobService;

    public XmlSplitterService(AzureBlobService blobService)
    {
        _blobService = blobService;
    }

    public async Task<List<string>> SplitAndStoreInvoicesAsync(Stream xmlStream)
    {
        var xmlDoc = new XmlDocument();
        xmlDoc.Load(xmlStream);
        var createdFiles = new List<string>();

        // Explicitly target <invoices>/<Invoice> structure
        XmlNodeList invoiceNodes = xmlDoc.SelectNodes("/Invoices/Invoice");
        if (invoiceNodes == null || invoiceNodes.Count == 0)
            throw new Exception("No <Invoice> elements found under <invoices>");

        int counter = 1;
        foreach (XmlNode invoiceNode in invoiceNodes)
        {
            var invoiceDoc = new XmlDocument();

            // Preserve XML declaration if exists
            if (xmlDoc.FirstChild is XmlDeclaration declaration)
            {
                invoiceDoc.AppendChild(invoiceDoc.CreateXmlDeclaration(
                    declaration.Version,
                    declaration.Encoding,
                    declaration.Standalone
                ));
            }

            // Clone entire <Invoice> node
            var clonedInvoice = invoiceDoc.ImportNode(invoiceNode, true);
            invoiceDoc.AppendChild(clonedInvoice);

            // Generate filename
            string blobName = $"invoice-{counter}-{Guid.NewGuid().ToString("N").Substring(0, 8)}.xml";
            createdFiles.Add(blobName);
            counter++;

            // Save to blob
            using var invoiceStream = new MemoryStream();
            invoiceDoc.Save(invoiceStream);
            invoiceStream.Position = 0;
            await _blobService.UploadFileAsync(blobName, invoiceStream);
        }

        return createdFiles;
    }
}