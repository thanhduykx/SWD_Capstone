namespace CPMS.BLL.Abstractions.Persistence;

public interface IDatabaseLifecycleService
{
    Task InitializeAsync(bool seedDevelopmentData, CancellationToken cancellationToken);
    Task EnsureMobileLecturerDatasetAsync(CancellationToken cancellationToken);
    Task<bool> CanConnectAsync(CancellationToken cancellationToken);
}
