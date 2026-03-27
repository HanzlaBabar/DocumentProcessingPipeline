using DocumentProcessingPipeline.Core.Entities;
using DocumentProcessingPipeline.Core.Enums;
using DocumentProcessingPipeline.Core.Interfaces;
using System.Text.RegularExpressions;

namespace DocumentProcessingPipeline.Infrastructure.Services
{
    public class TagDetectionService : ITagDetectionService
    {
        public List<Tag> DetectTags(string text)
        {
            var tags = new List<Tag>();
            var pattern = new Regex(@"\b[A-Z]{1,3}\s*\d{0,3}\b");

            foreach (Match match in pattern.Matches(text))
            {
                var label = match.Value.Trim();

                var type = label switch
                {
                    "TI" or "TT" or "TR" or "TC" => TagType.Temperature,
                    "LI" or "LT" or "LR" or "LC" => TagType.Level,
                    "FI" or "FT" or "FR" or "FC" => TagType.Flow,
                    "PI" or "PT" or "PR" or "PC" => TagType.Pressure,
                    "IP" => TagType.Transducer,
                    "PIC" or "PRC" => TagType.Controller,
                    "LA" => TagType.Alarm,
                    "FE" => TagType.FlowElement,
                    "TE" => TagType.TemperatureElement,
                    "LG" => TagType.Gauge,
                    "AT" => TagType.Analyzer,
                    _ => TagType.Unknown // fallback for unknown tags
                };

                tags.Add(new Tag
                {
                    Label = label,
                    Type = type
                });
            }

            return tags;
        }
    }
}
