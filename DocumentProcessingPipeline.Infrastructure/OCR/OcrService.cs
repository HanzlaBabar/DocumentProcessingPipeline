using Tesseract;

namespace DocumentProcessingPipeline.Infrastructure.OCR
{
    public class OcrService
    {
        public string ExtractText(string imagePath)
        {
            var tessDataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");

            using var engine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default);
            using var img = Pix.LoadFromFile(imagePath);
            using var page = engine.Process(img);

            return page.GetText();
        }
    }
}
