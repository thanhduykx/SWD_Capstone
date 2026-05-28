namespace PresentationLayer.Models;

public sealed record DashboardViewModel(
    string ApplicationName,
    string DatabaseProvider,
    string DefaultUsername,
    string Architecture);
