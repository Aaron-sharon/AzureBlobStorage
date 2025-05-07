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
        private readonly RedisService _redisService;
        private readonly ILogger<LogicController> _logger;

        public LogicController(AzureBlobService blobService, AzureQueueService queueService, XmlSplitterService xmlSplitterService, RedisService redisService, ILogger<LogicController> logger)
        {
            _blobService = blobService;
            _queueService = queueService;
            _xmlSplitterService = xmlSplitterService;
            _redisService = redisService;
            _logger = logger;
        }

        [HttpGet("enqueue")]
        public async Task<IActionResult> EnqueueMessage(string queueName, string message)
        {
            var length = await _redisService.EnqueueAsync(queueName, message);
            return Ok($"Message enqueued. Queue length: {length}");
        }

        [HttpGet("dequeue")]
        public async Task<IActionResult> DequeueMessage(string queueName)
        {
            var message = await _redisService.DequeueAsync(queueName);
            return Ok(message ?? "Queue is empty.");
        }

        [HttpGet("queue-length")]
        public async Task<IActionResult> GetQueueLength(string queueName)
        {
            var length = await _redisService.GetQueueLengthAsync(queueName);
            return Ok($"Queue length: {length}");
        }

        [HttpGet("test-redis")]
        public async Task<IActionResult> TestRedis()
        {
            await _redisService.SetDataAsync("testKey", "Hello Redis!");
            var data = await _redisService.GetDataAsync("testKey");
            return Ok($"Stored Data: {data}");
        }

        //[HttpPost("process-invoices")]
        //public async Task<IActionResult> ProcessInvoices(IFormFile file)
        //{
        //    if (file == null || file.Length == 0)
        //        return BadRequest("No file uploaded");

        //    try
        //    {
        //        using var stream = file.OpenReadStream();
        //        var createdFiles = await _xmlSplitterService.SplitAndStoreInvoicesAsync(stream);

        //        // Send each blob name to queue
        //        foreach (var blobName in createdFiles)
        //        {
        //            await _queueService.SendMessageAsync(blobName);
        //        }

        //        return Ok(new
        //        {
        //            Message = $"{createdFiles.Count} invoices processed and queued",
        //            Files = createdFiles
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        return StatusCode(500, $"Error: {ex.Message}");
        //    }
        //}


        [HttpPost("process-invoices")]
        public async Task<IActionResult> ProcessInvoices(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew(); // Start stopwatch

                using var stream = file.OpenReadStream();
                var createdFiles = await _xmlSplitterService.SplitAndStoreInvoicesAsync(stream); // Correct method name

                // Enqueue each blob name to Redis instead of Azure Queue
                foreach (var blobName in createdFiles)
                {
                    await _redisService.EnqueueAsync("invoiceQueue", blobName);
                }

                stopwatch.Stop(); // Stop stopwatch

                return Ok(new
                {
                    Message = $"{createdFiles.Count} invoices processed and enqueued to Redis.",
                    Files = createdFiles,
                    TimeTakenSeconds = stopwatch.Elapsed.TotalSeconds.ToString("N2") + "s"
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
