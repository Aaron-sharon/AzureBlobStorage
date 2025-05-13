namespace Azurite.Interface
{
    public interface IAzureBlobService
    {
        Task UploadFileAsync(string blobName, Stream fileStream);
        Task DeleteFileAsync(string blobName);
        Task CopyBlobAsync(string sourceBlobName, string destinationBlobName);
    }
}
