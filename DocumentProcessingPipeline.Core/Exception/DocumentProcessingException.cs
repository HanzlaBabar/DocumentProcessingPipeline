namespace DocumentProcessingPipeline.Core.Exceptions
{
    public class DocumentProcessingException : Exception
    {
        public string? ErrorCode { get; set; }
        public DateTime OccurredAt { get; set; }

        public DocumentProcessingException(string message, string? errorCode = null)
            : base(message)
        {
            ErrorCode = errorCode;
            OccurredAt = DateTime.UtcNow;
        }

        public DocumentProcessingException(string message, Exception innerException, string? errorCode = null)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
            OccurredAt = DateTime.UtcNow;
        }
    }

    public class InvalidDocumentException : DocumentProcessingException
    {
        public InvalidDocumentException(string message)
            : base(message, "INVALID_DOCUMENT") { }
    }

    public class OcrProcessingException : DocumentProcessingException
    {
        public OcrProcessingException(string message, Exception? innerException = null)
            : base($"OCR processing failed: {message}", innerException, "OCR_FAILED") { }
    }

    public class TagDetectionException : DocumentProcessingException
    {
        public TagDetectionException(string message, Exception? innerException = null)
            : base($"Tag detection failed: {message}", innerException, "TAG_DETECTION_FAILED") { }
    }
}