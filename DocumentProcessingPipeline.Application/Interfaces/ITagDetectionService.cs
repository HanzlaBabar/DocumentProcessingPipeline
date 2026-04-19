using DocumentProcessingPipeline.Core.Entities;

namespace DocumentProcessingPipeline.Application.Interfaces
{
    public interface ITagDetectionService
    {
        List<Tag> DetectTags(string text);
    }
}
