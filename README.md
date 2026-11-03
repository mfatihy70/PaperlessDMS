# PaperlessDMS
A Document management system for archiving documents in a FileStore, with automatic OCR (queue for OC-recognition), automatic summary generation (using Gen-AI), tagging and full text search (ElasticSearch).

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
- Swagger UI through Nginx: http://localhost/swagger/ (also linked as **API-Dokumentation** in the dashboard)
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
(including its subpaths) to http://localhost:5252. The documentation link therefore also works locally.
Restart `npm start` after changing `proxy.conf.json`.

### Current endpoints

| Method | Path | Purpose | Success response |
| --- | --- | --- | --- |
| GET | `/api/Documents` | List document metadata | 200 |
| GET | `/api/Documents/{id}` | Get document metadata | 200 |
| POST | `/api/Documents` | Create document metadata | 201 |
| PUT | `/api/Documents/{id}` | Update title and category | 204 |
| DELETE | `/api/Documents/{id}` | Delete document metadata | 204 |
| GET | `/api/Categories` | List categories | 200 |
| POST | `/api/Categories` | Create a category | 201 |

Document operations by ID return 404 when no matching document exists.
Malformed request bodies, invalid ID values and missing required DTO values return 400 validation problems.
Business validation is not comprehensive yet: for example, whitespace-only document titles and nonexistent
category IDs are not currently mapped to 400 responses.

The current implementation manages metadata only. File upload/download, OCR, AI summaries,
tagging and full-text search do not have endpoints yet. New controller actions are automatically
included in Swagger; add XML comments and `ProducesResponseType` attributes to describe them.

### Verification

```sh
dotnet test Paperless.slnx
```

The Swagger integration tests start the actual application with an in-memory HTTP test server and mock
only database initialization. They check all seven operations, response codes, DTO descriptions,
Swagger UI routes/assets and malformed request responses without requiring PostgreSQL.
Existing controller and service tests cover the application behavior separately.

To check the deployed proxy, open http://localhost/swagger/, expand an endpoint and select **Try it out**.
For a full workflow, create a category, create a document with the returned category ID,
retrieve/update the document and delete it. Swagger currently remains enabled in every environment,
matching the existing backend configuration.
