namespace CPMS.BLL.Testing;

public interface ITestDataResetService
{
    Task ResetAsync(CancellationToken cancellationToken);
}
