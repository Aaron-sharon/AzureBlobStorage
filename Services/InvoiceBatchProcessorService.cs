using System.Diagnostics;
using Newtonsoft.Json;

public class InvoiceBatchProcessorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InvoiceBatchProcessorService> _logger;
    private int _groupCounter = 1;
    private const int MaxBatchSize = 1000;
    private const int ChunkSize = 32;
    private const int IdleTimeoutSeconds = 60; // Max wait time if messages stop arriving
    private const string TraceFolder = "trace";

    public InvoiceBatchProcessorService(IServiceScopeFactory scopeFactory, ILogger<InvoiceBatchProcessorService> logger)
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
            var stopwatch = Stopwatch.StartNew();
            var idleWatch = Stopwatch.StartNew();

            _logger.LogInformation("Waiting to accumulate 1000 messages...");

            // Accumulate messages until batch is full or idle timeout
            while (allMessages.Count < MaxBatchSize && !stoppingToken.IsCancellationRequested)
            {
                var chunk = await queueService.ReceiveMessagesAsync(Math.Min(ChunkSize, MaxBatchSize - allMessages.Count));

                if (chunk.Count == 0)
                {
                    if (idleWatch.Elapsed.TotalSeconds >= IdleTimeoutSeconds)
                    {
                        _logger.LogInformation($"Idle timeout reached. Processing partial batch with {allMessages.Count} messages.");
                        break;
                    }

                    await Task.Delay(1000, stoppingToken); // Wait briefly before trying again
                    continue;
                }

                allMessages.AddRange(chunk);
                idleWatch.Restart(); // Reset idle timer on message receive
            }

            if (allMessages.Count == 0)
            {
                _logger.LogInformation("No messages received. Waiting before retry...");
                await Task.Delay(5000, stoppingToken);
                continue;
            }

            string folderName = $"invoice-group-{_groupCounter}";
            _logger.LogInformation($"Processing batch #{_groupCounter} with {allMessages.Count} messages");

            var traceEntries = new List<TraceEntry>();

            foreach (var message in allMessages)
            {
                var blobRef = JsonConvert.DeserializeObject<BlobQueueMessage>(message.Body.ToString());

                traceEntries.Add(new TraceEntry
                {
                    BlobName = blobRef.BlobName,
                    EventType = blobRef.EventType,
                    Timestamp = blobRef.Timestamp
                });

                await blobService.CopyBlobAsync(blobRef.BlobName, $"{folderName}/{blobRef.BlobName}");
                await blobService.DeleteFileAsync(blobRef.BlobName);
                await queueService.DeleteMessageAsync(message.MessageId, message.PopReceipt);
            }

            // Write trace log to blob
            var traceFileName = $"trace-batch-{_groupCounter:D5}.json";
            using var traceStream = new MemoryStream();
            var traceJson = JsonConvert.SerializeObject(traceEntries, Formatting.Indented);
            using (var writer = new StreamWriter(traceStream, leaveOpen: true))
            {
                writer.Write(traceJson);
                writer.Flush();
                traceStream.Position = 0;
                await blobService.UploadFileAsync($"{folderName}/{TraceFolder}/{traceFileName}", traceStream);
            }

            _logger.LogInformation($"Batch #{_groupCounter} completed. Time taken: {stopwatch.Elapsed.TotalSeconds:F2} sec");

            _groupCounter++;
            stopwatch.Stop();
        }
    }

    public class BlobQueueMessage
    {
        public string EventType { get; set; }
        public string BlobName { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class TraceEntry
    {
        public string EventType { get; set; }
        public string BlobName { get; set; }
        public DateTime Timestamp { get; set; }

    }
    public class TraceLog
    {
        public int BatchId { get; set; }
        public int TotalEntries { get; set; }
        public DateTime GeneratedAt { get; set; }
        public List<TraceEntry> Entries { get; set; }
    }

}
