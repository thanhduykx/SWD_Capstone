using CPMS.API.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Swashbuckle.AspNetCore.Swagger;

namespace CPMS.IntegrationTests;

public sealed class ProjectDocumentsOpenApiTests
{
    [Fact]
    public void Swagger_DescribesMultipartUploadAndStructuredAiResponse()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var environment = new TestEnvironment();
        services.AddSingleton<IWebHostEnvironment>(environment);
        services.AddSingleton<IHostEnvironment>(environment);
        services.AddControllers().AddApplicationPart(typeof(ProjectDocumentsController).Assembly);
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.CustomSchemaIds(type => type.FullName?.Replace('+', '.') ?? type.Name);
            options.SwaggerDoc("v1", new() { Title = "CPMS", Version = "v1" });
        });
        using ServiceProvider provider = services.BuildServiceProvider();

        var document = provider.GetRequiredService<ISwaggerProvider>().GetSwagger("v1");
        var upload = document.Paths["/api/documents"].Operations[Microsoft.OpenApi.Models.OperationType.Post];
        Assert.Contains("multipart/form-data", upload.RequestBody.Content.Keys);

        var suggestions = document.Paths["/api/documents/{id}/suggestions"]
            .Operations[Microsoft.OpenApi.Models.OperationType.Post];
        var schema = suggestions.Responses["200"].Content["application/json"].Schema;
        var responseSchema = schema.Reference is null ? schema : document.Components.Schemas[schema.Reference.Id];
        Assert.Equal(
            ["contentSummary", "improvementSummary", "strengthsSummary"],
            responseSchema.Properties.Keys.Order(StringComparer.Ordinal).ToArray());
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "CPMS.IntegrationTests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = AppContext.BaseDirectory;
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
