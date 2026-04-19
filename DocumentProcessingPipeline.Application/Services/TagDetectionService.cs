using DocumentProcessingPipeline.Application.Interfaces;
using DocumentProcessingPipeline.Core.Entities;
using DocumentProcessingPipeline.Core.Enums;
using DocumentProcessingPipeline.Core.Exceptions;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace DocumentProcessingPipeline.Application.Services
{
    public class TagDetectionService : ITagDetectionService
    {
        private readonly ILogger<TagDetectionService> _logger;
        private readonly Regex _tagPattern = new Regex(@"\b[A-Z]{1,3}\s*\d{0,3}\b");
        private const int MaxTags = 10000; // Prevent runaway processing

        public TagDetectionService(ILogger<TagDetectionService> logger)
        {
            _logger = logger;
        }

        public List<Tag> DetectTags(string text)
        {
            try
            {
                // Validation
                if (text == null)
                {
                    _logger.LogWarning("DetectTags called with null text");
                    return new List<Tag>();
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    _logger.LogInformation("DetectTags called with empty text");
                    return new List<Tag>();
                }

                if (text.Length > 10 * 1024 * 1024) // 10MB text limit
                {
                    _logger.LogWarning("Text exceeds size limit: {TextLength}", text.Length);
                    throw new TagDetectionException("Text size exceeds maximum allowed");
                }

                _logger.LogInformation("Starting tag detection, TextLength: {TextLength}", text.Length);

                var tags = new List<Tag>();
                var matches = _tagPattern.Matches(text);

                _logger.LogInformation("Found {MatchCount} potential tags", matches.Count);

                if (matches.Count > MaxTags)
                {
                    _logger.LogWarning("Tag count exceeds limit: {Count}", matches.Count);
                    throw new TagDetectionException($"Text contains too many potential tags ({matches.Count} > {MaxTags})");
                }

                var processedLabels = new HashSet<string>(); // Prevent duplicates

                foreach (Match match in matches)
                {
                    try
                    {
                        var label = match.Value?.Trim();

                        if (string.IsNullOrWhiteSpace(label))
                        {
                            _logger.LogDebug("Skipping empty match");
                            continue;
                        }

                        // Skip duplicates
                        if (processedLabels.Contains(label))
                        {
                            continue;
                        }

                        processedLabels.Add(label);

                        var type = DetectTagType(label);

                        tags.Add(new Tag
                        {
                            Label = label,
                            Type = type
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error processing tag match: {Match}", match.Value);
                        // Continue processing other matches
                        continue;
                    }
                }

                _logger.LogInformation("Tag detection completed, Found: {TagCount} unique tags", tags.Count);
                return tags;
            }
            catch (TagDetectionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during tag detection");
                throw new TagDetectionException($"Tag detection failed: {ex.Message}", ex);
            }
        }

        private TagType DetectTagType(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return TagType.Unknown;
            }

            return label switch
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
                _ => TagType.Unknown
            };
        }
    }
}