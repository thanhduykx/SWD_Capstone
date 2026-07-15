namespace CPMS.BLL.Common.Exceptions;

public sealed class ExternalServiceException(string message, Exception? innerException = null)
    : Exception(message, innerException);

public sealed class ExternalServiceUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
