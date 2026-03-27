using DocumentProcessingPipeline.Core.Enums;
using DocumentProcessingPipeline.Core.Interfaces;
using DocumentProcessingPipeline.Infrastructure.OCR;
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
            var ocrService = scope.ServiceProvider.GetRequiredService<OcrService>();
            var tagService = scope.ServiceProvider.GetRequiredService<ITagDetectionService>();

            var document = await repository.GetByIdAsync(documentId);

            if (document == null) return;

            try
            {
                document.Status = DocumentStatus.Processing;
                await repository.UpdateAsync(document);

                //OCR
                var text = ocrService.ExtractText(document.FileName);


                // tag detection
                var tags = tagService.DetectTags(text);

                document.Tags = tags;
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
