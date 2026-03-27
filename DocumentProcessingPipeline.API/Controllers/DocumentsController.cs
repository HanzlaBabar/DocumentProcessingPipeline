using DocumentProcessingPipeline.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace DocumentProcessingPipeline.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentService _service;

        public DocumentsController(IDocumentService service)
        {
            _service = service;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest("File is empty");

            var filePath = Path.Combine("uploads", file.FileName);

            Directory.CreateDirectory("uploads");

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var document = await _service.UploadAndProcessAsync(file.FileName, filePath);

            return Ok(document);
        }
    }
}
