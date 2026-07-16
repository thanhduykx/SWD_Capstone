namespace CPMS.API.Contracts.Academic;

public sealed record ImportGroupsRequest(
    IFormFile? File,
    [property: JsonRequired] int LecturerId);
