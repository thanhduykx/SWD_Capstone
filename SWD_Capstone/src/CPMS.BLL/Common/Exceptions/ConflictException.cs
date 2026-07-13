namespace CPMS.BLL.Common.Exceptions;

public sealed class ConflictException(string message) : Exception(message);
