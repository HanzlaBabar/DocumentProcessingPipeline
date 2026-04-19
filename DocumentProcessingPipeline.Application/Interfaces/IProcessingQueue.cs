
namespace DocumentProcessingPipeline.Application.Interfaces
{
    public interface IProcessingQueue
    {
        void Enqueue(string documentId);
        Task<string?> DequeueAsync(CancellationToken cancellationToken);
    }
}
