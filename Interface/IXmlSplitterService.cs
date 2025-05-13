namespace Azurite.Interface
{
    public interface IXmlSplitterService
    {
        Task<List<string>> SplitAndStoreInvoicesAsync(Stream xmlStream);
    }
}
