using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Newtonsoft.Json;

public class AzureQueueService
{
    private readonly QueueClient _queueClient;

    public AzureQueueService(IConfiguration configuration)
    {
        var connectionString = configuration["AzureStorage:ConnectionString"];
        var queueName = configuration["AzureStorage:QueueName"];

        _queueClient = new QueueClient(connectionString, queueName);
        _queueClient.CreateIfNotExists(); // Ensure the queue exists before use
    }

    public async Task SendMessageAsync(string blobName)
    {
        var message = new
        {
            EventType = "NewInvoice",
            BlobName = blobName,
            Timestamp = DateTime.UtcNow
        };

        string jsonMessage = JsonConvert.SerializeObject(message);
        await _queueClient.SendMessageAsync(jsonMessage);
    }

    public async Task<List<QueueMessage>> ReceiveMessagesAsync(int maxMessages = 10)
    {
        var response = await _queueClient.ReceiveMessagesAsync(maxMessages, TimeSpan.FromMinutes(5));
        return response.Value.ToList(); // Includes MessageId, PopReceipt, Body
    }

    public async Task DeleteMessageAsync(string messageId, string popReceipt)
    {
        await _queueClient.DeleteMessageAsync(messageId, popReceipt);
    }
}
