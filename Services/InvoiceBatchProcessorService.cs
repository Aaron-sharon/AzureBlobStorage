using System.Diagnostics;
using Newtonsoft.Json;

public class InvoiceBatchProcessorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InvoiceBatchProcessorService> _logger;
    private int _groupCounter = 1;

    public InvoiceBatchProcessorService(
        IServiceScopeFactory scopeFactory,
        ILogger<InvoiceBatchProcessorService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var queueService = scope.ServiceProvider.GetRequiredService<AzureQueueService>();
            var blobService = scope.ServiceProvider.GetRequiredService<AzureBlobService>();

            var allMessages = new List<Azure.Storage.Queues.Models.QueueMessage>();
            const int maxBatchSize = 1000;
            const int chunkSize = 32;

            // Stopwatch for timing the entire batch
            var stopwatch = Stopwatch.StartNew();

            _logger.LogInformation("Starting to collect messages...");

            // Receive messages in chunks
            while (allMessages.Count < maxBatchSize && !stoppingToken.IsCancellationRequested)
            {
                var chunk = await queueService.ReceiveMessagesAsync(Math.Min(chunkSize, maxBatchSize - allMessages.Count));
                if (chunk.Count == 0) break;

                allMessages.AddRange(chunk);
            }

            if (allMessages.Count > 0)
            {
                string folderName = $"invoice-group-{_groupCounter}";
                _logger.LogInformation($"Processing {allMessages.Count} messages into folder {folderName}");

                foreach (var message in allMessages)
                {
                    var body = message.Body.ToString();
                    var blobRef = JsonConvert.DeserializeObject<BlobQueueMessage>(body);

                    // Log and move
                    _logger.LogInformation($"Moving blob '{blobRef.BlobName}' to folder '{folderName}'");

                    await blobService.CopyBlobAsync(blobRef.BlobName, $"{folderName}/{blobRef.BlobName}");
                    await blobService.DeleteFileAsync(blobRef.BlobName);
                    await queueService.DeleteMessageAsync(message.MessageId, message.PopReceipt);
                }

                _logger.LogInformation($"Batch processing complete for {allMessages.Count} messages.");
                _groupCounter++;

                stopwatch.Stop();
                _logger.LogInformation($"Total time taken: {stopwatch.Elapsed.TotalSeconds:F2} seconds");

                // Fast retry
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            else
            {
                _logger.LogInformation("No messages found. Waiting before retry...");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                stopwatch.Stop();
            }
        }
    }

    private class BlobQueueMessage
    {
        public string EventType { get; set; }
        public string BlobName { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
