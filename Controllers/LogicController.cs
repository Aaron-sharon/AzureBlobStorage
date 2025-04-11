using System.Xml;
using Microsoft.AspNetCore.Mvc;      // For ControllerBase, ApiController, Route, HttpPost, HttpDelete, IActionResult
using Newtonsoft.Json;
namespace Azurite.Controllers
{
    [ApiController]
    [Route("api/blob")]
    public class LogicController : ControllerBase
    {
        private readonly AzureBlobService _blobService;
        private readonly AzureQueueService _queueService;
        private readonly XmlSplitterService _xmlSplitterService;

        public LogicController(AzureBlobService blobService, AzureQueueService queueService, XmlSplitterService xmlSplitterService)
        {
            _blobService = blobService;
            _queueService = queueService;
            _xmlSplitterService = xmlSplitterService;
        }


        [HttpPost("process-invoices")]
        public async Task<IActionResult> ProcessInvoices(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            try
            {
                using var stream = file.OpenReadStream();
                var createdFiles = await _xmlSplitterService.SplitAndStoreInvoicesAsync(stream);
                return Ok(new
                {
                    Message = $"{createdFiles.Count} invoices processed",
                    Files = createdFiles
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadToBlob(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded.");

            var blobName = $"{Guid.NewGuid()}_{file.FileName}";
            var tempFilePath = Path.GetTempFileName();

            try
            {
                // Save the file temporarily
                using (var stream = new FileStream(tempFilePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Read XML content from the temp file
                string xmlContent = await System.IO.File.ReadAllTextAsync(tempFilePath);

                // Convert XML to JSON
                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(xmlContent);
                string json = JsonConvert.SerializeXmlNode(xmlDoc, Newtonsoft.Json.Formatting.Indented);

                // Upload to Blob Storage
                using (var fileStream = new FileStream(tempFilePath, FileMode.Open))
                {
                    await _blobService.UploadFileAsync(blobName, fileStream);
                }

                // Send JSON to Queue Storage
                //await _queueService.SendMessageAsync(json);

                // Cleanup
                System.IO.File.Delete(tempFilePath);

                return Ok(new { BlobName = blobName, Status = "Uploaded and queued!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }
        [HttpDelete("delete/{blobName}")]
        public async Task<IActionResult> DeleteFromBlob(string blobName)
        {
            try
            {
                await _blobService.DeleteFileAsync(blobName);
                return Ok($"Blob {blobName} deleted successfully!");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error: {ex.Message}");
            }
        }
    }
}
