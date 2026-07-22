using Microsoft.AspNetCore.Http;

namespace CPMS.API.Contracts.Defense;

public sealed record SubmitDefenseCommentRequest(
    [property: JsonRequired] string Content);
public sealed class UploadDefenseEvidenceRequest
{
    public IFormFile? File { get; set; }
    public string? Note { get; set; }
}
public sealed record DefenseEvidenceResponse(
    int Id,
    int DefenseSessionId,
    int CapturedByLecturerId,
    string FileName,
    string FilePath,
    string ContentType,
    long FileSize,
    string? Note,
    DateTime CapturedAt);
