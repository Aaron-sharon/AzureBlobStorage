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

            var messages = await queueService.ReceiveMessagesAsync(10);

            if (messages.Count == 10)
            {
                string folderName = $"invoice-group-{_groupCounter}";
                _logger.LogInformation($"Processing {messages.Count} messages into folder {folderName}");

                foreach (var message in messages)
                {
                    var body = message.Body.ToString();
                    var blobRef = JsonConvert.DeserializeObject<BlobQueueMessage>(body);

                    _logger.LogInformation($"Moving blob '{blobRef.BlobName}' to folder '{folderName}'");

                    // Copy blob
                    await blobService.CopyBlobAsync(blobRef.BlobName, $"{folderName}/{blobRef.BlobName}");
                    // Delete original blob
                    await blobService.DeleteFileAsync(blobRef.BlobName);
                    // Delete queue message
                    await queueService.DeleteMessageAsync(message.MessageId, message.PopReceipt);

                    _logger.LogInformation($"Processed blob: {blobRef.BlobName}");
                }

                _groupCounter++;
                // Fast retry after processing
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
            }
            else
            {
                // Nothing or not enough to process — wait longer
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
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
