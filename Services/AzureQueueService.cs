
using Azure.Storage.Queues;
using Newtonsoft.Json;

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

    public async Task SendMessageAsync(string blobName)
    {
        var queueClient = _queueServiceClient.GetQueueClient(_queueName);
        await queueClient.CreateIfNotExistsAsync();

        var message = new
        {
            EventType = "NewInvoice",
            BlobName = blobName,
            Timestamp = DateTime.UtcNow
        };

        string jsonMessage = JsonConvert.SerializeObject(message);
        await queueClient.SendMessageAsync(jsonMessage);
    }
}