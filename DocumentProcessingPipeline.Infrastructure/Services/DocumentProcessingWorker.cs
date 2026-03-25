using DocumentProcessingPipeline.Core.Enums;
using DocumentProcessingPipeline.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DocumentProcessingPipeline.Infrastructure.Services
{
    public class DocumentProcessingWorker : BackgroundService
    {
        private readonly IProcessingQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;

        public DocumentProcessingWorker(IProcessingQueue queue, IServiceScopeFactory scopeFactory)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while(!stoppingToken.IsCancellationRequested)
            {
                var documentId = await _queue.DequeueAsync(stoppingToken);

                if(documentId != null)
                {
                    await ProcessDocument(documentId);
                }
            }
        }

        private async Task ProcessDocument(string documentId)
        {
            using var scope = _scopeFactory.CreateScope();

            var repository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();

            var document = await repository.GetByIdAsync(documentId);

            if (document == null) return;

            try
            {
                document.Status = DocumentStatus.Processing;
                await repository.UpdateAsync(document);

                await Task.Delay(3000); // simulate processing

                // fake tags
                document.Tags.Add(new()
                {
                    Label = "VALVE-101",
                    Type = "Valve",
                    X = 100,
                    Y = 200
                });

                document.Status = DocumentStatus.Completed;

                await repository.UpdateAsync(document);
            }
            catch
            {
                document.Status = DocumentStatus.Failed;
                await repository.UpdateAsync(document);
            }
        }
    }
}
