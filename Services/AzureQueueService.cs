using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Newtonsoft.Json;

public class AzureQueueService
{
    private readonly QueueClient _queueClient;
    private readonly ILogger<AzureQueueService> _logger;

    public AzureQueueService(IConfiguration configuration, ILogger<AzureQueueService> logger)
    {
        var connectionString = configuration["AzureStorage:ConnectionString"];
        var queueName = configuration["AzureStorage:QueueName"];

        _queueClient = new QueueClient(connectionString, queueName);
        _queueClient.CreateIfNotExists(); // Ensure the queue exists before use
        _logger = logger;
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
        // 20-minute visibility timeout ensures time to process before retry
        var response = await _queueClient.ReceiveMessagesAsync(maxMessages, TimeSpan.FromMinutes(20));

        var messages = response.Value.ToList();

        foreach (var msg in messages)
        {
            _logger.LogInformation($"Received message ID: {msg.MessageId}, DequeueCount: {msg.DequeueCount}");
        }

        return messages;
    }

    public async Task<bool> DeleteMessageAsync(string messageId, string popReceipt)
    {
        try
        {
            await _queueClient.DeleteMessageAsync(messageId, popReceipt);
            _logger.LogInformation($"Message {messageId} successfully deleted.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to delete message {messageId}.");
            return false;
        }
    }
}
