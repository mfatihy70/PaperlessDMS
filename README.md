# PaperlessDMS
A Document management system for archiving documents in a FileStore, with automatic OCR (queue for OC-recognition), automatic summary generation (using Gen-AI), tagging and full text search (ElasticSearch).

The current backend implementation plan and rating-matrix review are maintained in
[the backend roadmap](docs/backend-roadmap.md).

## Backend architecture

The backend follows Clean Architecture with compile-time dependencies directed toward the application core:

| Project | Responsibilities | Project dependencies |
| --- | --- | --- |
| `Paperless.Domain` | Framework-independent document and category entities | None |
| `Paperless.Application` | Document/category use cases, application validation, mapping, DTOs and repository contracts | Domain |
| `Paperless.Infrastructure` | EF Core, PostgreSQL, repository implementations and database initialization | Application, Domain |
| `Paperless.REST` | HTTP controllers, request validation, Swagger, exception translation and dependency wiring | Application, Infrastructure |

```mermaid
flowchart LR
    REST[Paperless.REST] --> Application[Paperless.Application]
    REST -->|composition root only| Infrastructure[Paperless.Infrastructure]
    Infrastructure --> Application
    Infrastructure --> Domain[Paperless.Domain]
    Application --> Domain
```

`Program.cs` is the composition root: it registers application services and infrastructure implementations.
Controllers depend on `IDocumentService` or `ICategoryService`; they do not access repositories or EF Core.
Infrastructure implements repository interfaces declared by Application. Domain and Application have no
NuGet, ASP.NET Core or EF Core dependencies. Splitting the code into projects does not add extra runtime services;
the backend still runs as one REST process.

### Request and upload flow

1. REST binds and validates HTTP input. `CreateCategoryDto` and `UpdateCategoryDto` remain HTTP request types.
2. The controller converts `IFormFile` to a framework-independent `DocumentUpload` containing file metadata and a stream.
   The controller owns and disposes that stream after the application service completes.
3. Application validates use-case input, reads uploads with a bounded 25 MiB buffer limit, maps metadata and
   calls repository interfaces. It also supports callers outside HTTP, such as future workers.
4. Infrastructure persists entities through EF Core. Response DTOs return through REST with the existing JSON contract.
5. `ApplicationValidationException` identifies rejected fields without depending on HTTP. The REST exception handler
   translates it to a 400 validation problem. Unexpected failures retain the standard 500 error response.

Validation of request syntax belongs to REST; use-case rules are also checked in Application so another caller
cannot bypass them. Database constraints remain configured in Infrastructure, rather than through EF attributes on entities.
Response DTO XML documentation is generated in Application and loaded alongside REST documentation by Swagger.

### Persistence and build decisions

- File bytes remain in PostgreSQL for this refactor. API routes, multipart fields and JSON response properties are preserved.
  A future storage adapter can move bytes to MinIO without introducing MinIO dependencies into Application.
- The existing `EnsureCreated` and legacy `FileContent` upgrade SQL now belong to
  `Paperless.Infrastructure/Persistence/DatabaseInitializer.cs`. Their behavior is unchanged; versioned EF migrations
  are a separate follow-up. This refactor does not recreate or reset databases.
- Mapping stays explicit inside application services. No new mapping library is introduced as part of the architecture change.
- Existing tests are adapted to the new namespaces and stream-based application boundary. This refactor does not add
  OCR, queues, GenAI or new frontend behavior.
- The REST Docker build context is the repository root so all four backend projects are available. Compose and the
  container-publishing workflow use that context. The root `.dockerignore` excludes local build outputs and unrelated caches.

Build from the repository root:

```sh
dotnet build Paperless.slnx
docker build -f src/Paperless.REST/dockerfile -t paperless-rest:local .
```

## API documentation (Swagger UI)

Swagger UI is generated from the backend controllers and DTOs using Swashbuckle.
It lists every current API operation, request and response fields, and response codes.
Use **Try it out** to send requests to the running API. These requests operate on the application's database.

### Docker

Start the application with the existing Compose configuration:

```sh
docker compose up --build
```

- Dashboard: http://localhost/
- Swagger UI through Nginx: http://localhost/swagger/
- Direct backend Swagger UI: http://localhost:8081/swagger/
- OpenAPI JSON: http://localhost/swagger/v1/swagger.json

Nginx forwards `/api/` and `/swagger/` to the backend, including the Swagger UI assets and OpenAPI JSON.
`/swagger` redirects to `/swagger/`. The UI loads its OpenAPI document using a relative URL;
API requests use the same origin, so no additional CORS configuration is needed.

### Local development

The backend requires the .NET 10 SDK and a reachable PostgreSQL database. For example,
start only the development database using `docker compose up -d db`, then run from the repository root in PowerShell:

```powershell
$env:ConnectionStrings__DefaultConnection = "Host=localhost;Port=5432;Database=paperless_db;Username=paperless_user;Password=paperless_password"
dotnet run --project src/Paperless.REST --launch-profile http
```

Open http://localhost:5252/swagger/. The backend initializes its database before serving requests,
so Swagger is available only after the database connection succeeds.

To use the Angular dashboard locally, run in a second terminal:

```sh
cd src/Paperless.UI
npm ci
npm start
```

Open http://localhost:4200/. The Angular development proxy forwards `/api/**` and `/swagger`
(including its subpaths) to http://localhost:5252. Swagger is available at http://localhost:4200/swagger/.
Restart `npm start` after changing `proxy.conf.json`.

### Current endpoints

| Method | Path | Purpose | Success response |
| --- | --- | --- | --- |
| GET | `/api/Documents` | List document metadata | 200 |
| GET | `/api/Documents/{id}` | Get document metadata | 200 |
| POST | `/api/Documents` | Upload an original file and assign an optional category | 201 |
| GET | `/api/Documents/{id}/file` | Download the original file | 200 |
| PUT | `/api/Documents/{id}` | Replace the file and/or update its category | 204 |
| DELETE | `/api/Documents/{id}` | Delete the document and its file | 204 |
| GET | `/api/Categories` | List categories | 200 |
| GET | `/api/Categories/{id}` | Get a category | 200 |
| POST | `/api/Categories` | Create a category | 201 |
| PUT | `/api/Categories/{id}` | Replace the category name and description | 204 |
| DELETE | `/api/Categories/{id}` | Delete the category, preserving its documents | 204 |

### Uploads and downloads

Document POST and PUT requests use **multipart/form-data**, not JSON:

- `file`: required on POST; optional on PUT. All file types are currently accepted up to **25 MiB** per file.
- `categoryId`: an existing positive category ID. Omit it or send an empty value to leave the document unassigned.
- On PUT, omitting `file` preserves the existing file. Omitting `categoryId` or sending an empty value **removes**
  the current category assignment.

Title, file name, media type and size are derived from the uploaded file. There is no separate editable title field.
Original bytes are currently stored in PostgreSQL. GET `/api/Documents/{id}/file` returns those bytes,
the stored media type and an attachment file name. Legacy records without stored bytes return 404 for this endpoint.
The metadata response includes `hasFile` to indicate file availability.

The backend and Nginx `/api/` proxy allow **27 MiB for the entire request**, including multipart overhead.
A file above 25 MiB within that request limit returns 400; an entire request above 27 MiB returns 413.
When Nginx rejects a request before it reaches ASP.NET Core, its 413 response uses Nginx's default response format.

### Categories and errors

Category POST and PUT requests use JSON, for example:

```json
{
  "name": "Invoices",
  "description": "Incoming invoices"
}
```

Category names must contain non-whitespace characters and be at most 100 characters long.
Names are trimmed before storage. A null description removes the existing description on PUT.
Deleting a category sets `categoryId` to null on its documents; the documents and file bytes remain stored.
Successful POST requests return 201 and a `Location` header pointing to the created resource's individual endpoint.

- **400**: malformed input, missing/invalid fields, nonpositive IDs, invalid files or an unknown category assignment.
  ASP.NET Core returns `application/problem+json` with `status`, `title` and an `errors` object keyed by field name.
- **404**: a positive ID does not identify an existing resource, or the document has no stored file.
- **413**: the entire upload request is too large.
- **415**: the endpoint does not support the request's content type.
- **500**: an unexpected server error. ASP.NET Core returns Problem Details without exposing exception messages to clients.

Request-specific `traceId` values in MVC problem responses help correlate failures with backend logs.
OCR, AI summaries, tagging and full-text search are not implemented yet.
New controller actions are automatically included in Swagger; add XML comments and `ProducesResponseType`
attributes to describe them. The OpenAPI document is generated as JSON; no separate OpenAPI YAML is maintained.

### Verification

```sh
dotnet test Paperless.slnx
```

The existing backend suite includes controller/service checks and Swagger integration checks.
The Swagger checks start the actual application with an in-memory HTTP test server and bypass database
initialization, so PostgreSQL is not required for them. This does not verify PostgreSQL persistence or Nginx.

To verify the actual application manually, start the backend and database, open Swagger, and use **Try it out**:

1. POST a category. Follow its `Location` header and confirm the category is returned.
2. POST a document using the file picker and the returned category ID. Download it through the file endpoint.
3. PUT the category with a new name; confirm the document metadata shows the new category name.
4. PUT the document with an optional replacement file; download and compare the result.
5. DELETE the category. Confirm the document still exists, its category is null and its file remains downloadable.
6. DELETE the document; subsequent metadata/file GET requests should return 404.
7. Try a blank category name, a name longer than 100 characters, an unknown category assignment,
   and IDs such as 0, -1 or `abc`. Confirm these produce 400 validation problems rather than 500 errors.

Use http://localhost/swagger/ to include Nginx in the check, or http://localhost:8081/swagger/
to access the containerized backend directly. Swagger remains enabled in every environment,
matching the existing configuration.
