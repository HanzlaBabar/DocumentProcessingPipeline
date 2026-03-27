
using DocumentProcessingPipeline.Core.Entities;

namespace DocumentProcessingPipeline.Core.Interfaces
{
    public interface ITagDetectionService
    {
        List<Tag> DetectTags(string text);
    }
}
