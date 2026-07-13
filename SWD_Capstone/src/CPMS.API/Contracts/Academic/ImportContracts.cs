namespace CPMS.API.Contracts.Academic;

public sealed record ImportGroupsRequest(IFormFile? File, int LecturerId);
