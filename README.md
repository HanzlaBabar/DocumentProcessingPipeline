# Document Processing Pipeline

An ASP.NET Core 8 Web API that accepts document uploads (images and PDFs), extracts text using Tesseract OCR, and automatically detects **P&ID (Process & Instrumentation Diagram) instrument tags** from the extracted text. Results are persisted in MongoDB.

---

## Architecture

The solution follows a **Clean Architecture / layered** approach with three projects:

```
DocumentProcessingPipeline/
├── DocumentProcessingPipeline.Core/           # Domain layer – no external dependencies*
│   ├── Entities/        Document, Tag
│   ├── Enums/           DocumentStatus, TagType
│   └── Interfaces/      IDocumentRepository, IDocumentService,
│                        IProcessingQueue, ITagDetectionService
│
├── DocumentProcessingPipeline.Infrastructure/ # Implementation layer
│   ├── OCR/             OcrService            (Tesseract wrapper)
│   ├── Persistence/     MongoDbContext, DocumentRepository
│   └── Services/        DocumentService, TagDetectionService,
│                        InMemoryProcessingQueue, DocumentProcessingWorker
│
└── DocumentProcessingPipeline.API/            # Presentation layer
    ├── Controllers/     DocumentsController
    └── Program.cs       DI registration & middleware
```

> \* `Document` and `Tag` entities currently carry MongoDB BSON attributes (`[BsonRepresentation]`). This is a known coupling between the Core and infrastructure concerns.

---

## Technology Stack

| Concern | Library / Tool |
|---|---|
| Web framework | ASP.NET Core 8 |
| OCR | [Tesseract 5.2](https://github.com/charlesw/tesseract) (English language data) |
| Database | MongoDB via [MongoDB.Driver 3.7](https://www.mongodb.com/docs/drivers/csharp/) |
| API docs | Swagger / Swashbuckle |
| Background work | `Microsoft.Extensions.Hosting` `BackgroundService` |

---

## How It Works

1. A client uploads a file to `POST /api/documents/upload`.
2. The API saves the file to the `uploads/` directory.
3. `DocumentService.UploadAndProcessAsync` runs synchronously in the request:
   - Creates a `Document` record in MongoDB with `Status = Processing`.
   - Calls `OcrService.ExtractText` (Tesseract) to get the raw text from the file.
   - Passes the text to `TagDetectionService.DetectTags` which uses a regex to find P&ID instrument tag codes (e.g. `TI`, `FT`, `PI 101`).
   - Saves the detected tags and sets `Status = Completed` (or `Failed` on error).
4. The enriched `Document` object is returned in the HTTP response.

### Tag Detection

Tags are matched with the pattern `\b[A-Z]{1,3}\s*\d{0,3}\b` and classified:

| Code(s) | `TagType` |
|---|---|
| `TI`, `TT`, `TR`, `TC` | Temperature |
| `LI`, `LT`, `LR`, `LC` | Level |
| `FI`, `FT`, `FR`, `FC` | Flow |
| `PI`, `PT`, `PR`, `PC` | Pressure |
| `IP` | Transducer |
| `PIC`, `PRC` | Controller |
| `LA` | Alarm |
| `FE` | FlowElement |
| `TE` | TemperatureElement |
| `LG` | Gauge |
| `AT` | Analyzer |
| anything else | Unknown |

### Background Processing (disabled)

`InMemoryProcessingQueue` and `DocumentProcessingWorker` implement an alternative asynchronous pipeline where document IDs are enqueued and processed by a hosted `BackgroundService`. This path is currently commented out in `Program.cs` and the synchronous `DocumentService` path is used instead.

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [MongoDB](https://www.mongodb.com/try/download/community) running locally (default: `mongodb://localhost:27017`)
- Tesseract English language data – the file `tessdata/eng.traineddata` is already included in `DocumentProcessingPipeline.API/tessdata/` and is copied to the output directory on build.

---

## Getting Started

```bash
# 1. Clone the repository
git clone https://github.com/HanzlaBabar/DocumentProcessingPipeline.git
cd DocumentProcessingPipeline

# 2. (Optional) Update MongoDB connection settings
#    Edit DocumentProcessingPipeline.API/appsettings.json
#    "MongoDb": { "ConnectionString": "mongodb://localhost:27017", "Database": "DocumentDb" }

# 3. Restore & run
dotnet run --project DocumentProcessingPipeline.API
```

Swagger UI is available at `https://localhost:{port}/swagger` when running in the Development environment.

---

## API Endpoints

### Upload a document

```
POST /api/documents/upload
Content-Type: multipart/form-data

file = <image or PDF>
```

**Response** – the created `Document` object:

```json
{
  "id": "3fa85f64-...",
  "fileName": "diagram.png",
  "filePath": "uploads/diagram.png",
  "status": "Completed",
  "tags": [
    { "label": "TI", "type": "Temperature" },
    { "label": "FT 101", "type": "Flow" }
  ]
}
```

### Get all documents

```
GET /api/documents
```

**Response** – array of `Document` objects.

---

## Project Status & Known Issues

- **Background worker disabled** – `InMemoryProcessingQueue` and `DocumentProcessingWorker` are registered but commented out in `Program.cs`. Re-enable them for async, non-blocking upload processing.
- **Core ↔ Infrastructure coupling** – `Document` and `Tag` entities import `MongoDB.Bson` attributes. Moving serialisation concerns out of the Core layer (e.g. with a separate persistence model) would make the domain truly infrastructure-agnostic.
- **Silent exception handling** – `DocumentService` catches all exceptions and only sets `Status = Failed` without logging the error details.
- **No input validation on file type** – the upload endpoint accepts any file. Adding MIME-type or extension validation would prevent unexpected Tesseract errors.
- **Single language OCR** – only English (`eng`) Tesseract data is bundled. Multi-language support requires additional `tessdata` files.
