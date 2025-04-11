using Azure.Storage.Blobs;

public class AzureBlobService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;

    public AzureBlobService(IConfiguration configuration)
    {
        var connectionString = configuration["AzureStorage:ConnectionString"];
        _containerName = configuration["AzureStorage:ContainerName"];
        _blobServiceClient = new BlobServiceClient(connectionString);
    }

    // Upload a file to Azure Blob Storage
    public async Task UploadFileAsync(string blobName, Stream fileStream)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        await containerClient.CreateIfNotExistsAsync();
        var blobClient = containerClient.GetBlobClient(blobName);
        fileStream.Position = 0; // Ensure stream is at start
        await blobClient.UploadAsync(fileStream, overwrite: true);
    }

    // Delete a file from Azure Blob Storage
    public async Task DeleteFileAsync(string blobName)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        var blobClient = containerClient.GetBlobClient(blobName);
        await blobClient.DeleteIfExistsAsync();
    }
}