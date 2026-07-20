# Project document workflow

## Production design

Documents are private resources. The browser never receives a Supabase service-role key and never accesses an object by a public URL.

1. A student sends `multipart/form-data` to the CPMS API.
2. The BLL verifies group membership, size, extension and file signature.
3. The DAL uploads the object to a private storage bucket and saves only its storage key in `documents.file_path`.
4. A student in the group, an assigned reviewer, the supervisor, or an administrator requests the file through the CPMS API.
5. The API checks authorization again and streams the object to the browser.

Supabase Storage is the production provider. Local storage is retained for development only.

```json
"DocumentStorage": {
  "Provider": "Supabase",
  "SupabaseUrl": "https://PROJECT.supabase.co",
  "ServiceRoleKey": "",
  "Bucket": "cpms-documents"
}
```

Create `cpms-documents` as a private bucket with a 50 MB limit and these MIME types:

- `application/pdf`
- `application/vnd.openxmlformats-officedocument.wordprocessingml.document`
- `application/zip`
- `text/plain`

On Render, configuration keys can be supplied as `DocumentStorage__Provider`, `DocumentStorage__SupabaseUrl`, `DocumentStorage__ServiceRoleKey`, and `DocumentStorage__Bucket`. Never expose the service-role key through Vite or any frontend variable.

The BE also accepts the conventional deployment names `SUPABASE_URL`, `SUPABASE_SERVICE_ROLE_KEY` (or `SUPABASE_SERVICE_KEY`), and `SUPABASE_STORAGE_BUCKET`; when a service key is present, the provider automatically switches to Supabase.

## Stable API contracts

### Upload

`POST /api/documents` with `multipart/form-data`:

- `groupId`: positive integer
- `docType`: `Proposal`, `Progress`, or `Final`
- `file`: PDF, DOCX, ZIP, or TXT; maximum 50 MB

The response is `201 Created`:

```json
{
  "id": 12,
  "groupId": 3,
  "docType": "Final",
  "fileName": "CPMS_Final_Report.pdf",
  "fileSize": 1845320,
  "versionNo": 2,
  "status": "Submitted",
  "uploadedAt": "2026-07-20T08:30:00Z",
  "uploadedById": 15
}
```

### List and download

- `GET /api/documents/group/{groupId}` returns an array using the same metadata shape.
- `GET /api/documents/{id}/download` returns the original binary file and filename.

### Lecturer AI suggestion

`POST /api/documents/{id}/suggestions` is Lecturer-only and uses the existing AI rate limit. PDF is sent to Gemini as a native document so diagrams, tables, and layout remain available. DOCX and TXT are converted to bounded text. ZIP is rejected for AI analysis.

The response deliberately matches `POST /api/project-suggestions/summary`:

```json
{
  "contentSummary": "...",
  "strengthsSummary": "...",
  "improvementSummary": "..."
}
```

No score, pass/fail verdict, or automatic final comment is produced. The lecturer must review and explicitly copy the suggestion into the official feedback.

## Operational notes

- Run malware scanning before opening uploads in a high-risk production environment.
- Keep historical versions; the current API increments `versionNo` per group and document type.
- Configure retention separately for source ZIP files and final reports.
- For files larger than 50 MB, add a signed resumable-upload flow instead of increasing the ASP.NET request limit.
