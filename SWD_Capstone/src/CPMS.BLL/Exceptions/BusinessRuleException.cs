namespace CPMS.BLL.Exceptions;

public sealed class BusinessRuleException(string message) : Exception(message);
