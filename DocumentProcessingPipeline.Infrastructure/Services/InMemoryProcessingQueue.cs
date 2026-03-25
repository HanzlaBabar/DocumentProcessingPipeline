using DocumentProcessingPipeline.Core.Interfaces;

namespace DocumentProcessingPipeline.Infrastructure.Services
{
    public class InMemoryProcessingQueue : IProcessingQueue
    {
        private readonly Queue<string> _queue = new();
        private readonly SemaphoreSlim _signal = new(0);

        public void Enqueue(string documentId)
        {
            lock(_queue)
            {
                _queue.Enqueue(documentId);
            }

            _signal.Release();
        }

        public async Task<string?> DequeueAsync(CancellationToken cancellationToken)
        {
            await _signal.WaitAsync(cancellationToken);

            lock(_queue)
            {
                return _queue.Count > 0 ? _queue.Dequeue() : null;
            }
        }
    }
}
