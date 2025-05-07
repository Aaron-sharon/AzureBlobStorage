public class RedisMessageProcessorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RedisService _redisService;
    private readonly ILogger<RedisMessageProcessorService> _logger;
    private const string QueueName = "invoice-queue";

    public RedisMessageProcessorService(IServiceScopeFactory scopeFactory, RedisService redisService, ILogger<RedisMessageProcessorService> logger)
    {
        _scopeFactory = scopeFactory;
        _redisService = redisService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("RedisMessageProcessorService started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // Dequeue message
                var message = await _redisService.DequeueAsync(QueueName);

                if (message != null)
                {
                    _logger.LogInformation($"Processing message: {message}");

                    using (var scope = _scopeFactory.CreateScope())
                    {
                        var blobService = scope.ServiceProvider.GetRequiredService<AzureBlobService>();

                        // Process the message
                        // For demonstration, we will just log the message
                        _logger.LogInformation($"Blob processing logic would go here. Message: {message}");

                        // Example: You can perform some action with the AzureBlobService here
                        // await blobService.ProcessBlobAsync(message);
                    }
                }
                else
                {
                    _logger.LogInformation("No messages in the queue. Waiting...");
                    await Task.Delay(1000); // Poll every 1 second
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error processing message: {ex.Message}");
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("RedisMessageProcessorService is stopping.");
        await base.StopAsync(cancellationToken);
    }
}
