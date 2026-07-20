using System.ComponentModel.DataAnnotations;
using CPMS.BLL.Enums;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CPMS.API.Contracts.Documents;

public sealed class UploadProjectDocumentRequest
{
    [Range(1, int.MaxValue)] public int GroupId { get; init; }
    [BindRequired] public DocumentType DocType { get; init; }
    [Required] public required IFormFile File { get; init; }
}

public sealed record ProjectDocumentResponse(
    int Id,
    int GroupId,
    string DocType,
    string FileName,
    long FileSize,
    int VersionNo,
    string Status,
    DateTime UploadedAt,
    int UploadedById);
