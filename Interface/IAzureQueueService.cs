using Azure.Storage.Queues.Models;

namespace Azurite.Interface
{
    public interface IAzureQueueService
    {
        Task<List<QueueMessage>> ReceiveMessagesAsync(int maxMessages = 10);
        Task<bool> DeleteMessageAsync(string messageId, string popReceipt);
        Task SendMessageAsync(string blobName);
    }
}
