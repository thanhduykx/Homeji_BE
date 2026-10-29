using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Swagger;

namespace Homeji.Api.IntegrationTests;

public sealed class VoiceUploadOpenApiTests : IClassFixture<HomejiApiFactory>
{
    private readonly HomejiApiFactory _factory;

    public VoiceUploadOpenApiTests(HomejiApiFactory factory) => _factory = factory;

    [Fact]
    public void VoiceUpload_HasMultipartContractAndDoesNotBreakApiDocument()
    {
        using var scope = _factory.Services.CreateScope();
        var swagger = scope.ServiceProvider.GetRequiredService<ISwaggerProvider>().GetSwagger("api");
        var path = swagger.Paths["/api/conversations/{conversationId}/messages/voice"];
        Assert.NotNull(path.Operations);
        var operation = Assert.Single(path.Operations!).Value;
        Assert.NotNull(operation.RequestBody);
        Assert.Contains("multipart/form-data", operation.RequestBody!.Content!.Keys);
    }
}
