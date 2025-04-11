
using Azure.Storage.Queues;

public class AzureQueueService
{
    private readonly QueueServiceClient _queueServiceClient;
    private readonly string _queueName;

    public AzureQueueService(IConfiguration configuration)
    {
        var connectionString = configuration["AzureStorage:ConnectionString"];
        _queueName = configuration["AzureStorage:QueueName"];
        _queueServiceClient = new QueueServiceClient(connectionString);
    }

    public async Task SendMessageAsync(string message)
    {
        var queueClient = _queueServiceClient.GetQueueClient(_queueName);
        await queueClient.CreateIfNotExistsAsync();
        await queueClient.SendMessageAsync(message);
    }
}