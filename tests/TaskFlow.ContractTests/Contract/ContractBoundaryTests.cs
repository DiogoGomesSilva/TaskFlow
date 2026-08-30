using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace TaskFlow.ContractTests;

public sealed class ContractBoundaryTests(TaskFlowApiFactory factory)
    : IClassFixture<TaskFlowApiFactory>
{
    private const string NonCanonicalGuid = "550e8400e29b41d4a716446655440000";

    private readonly HttpClient _client = factory.CreateClient();
    private readonly OpenApiResponseValidator _validator = new();

    [Theory]
    [InlineData("{ \"Name\": \"Projeto\" }")]
    [InlineData("{ \"NAME\": \"Projeto\" }")]
    public async Task CreateProject_Returns400_WhenPropertyCasingDiffersFromSchema(
        string json)
    {
        using var response = await _client.PostAsync("/projetos", JsonRequest(json));

        await AssertValidationProblemAsync(response);
    }

    [Fact]
    public async Task CreateTask_Returns400_WhenPropertyCasingDiffersFromSchema()
    {
        var projectId = await CreateProjectAsync();

        using var response = await _client.PostAsync(
            $"/projetos/{projectId}/tarefas",
            JsonRequest("{ \"Title\": \"Tarefa\", \"Priority\": \"high\" }"));

        await AssertValidationProblemAsync(response);
    }

    [Theory]
    [InlineData("GET", "/projetos/550e8400e29b41d4a716446655440000", null)]
    [InlineData("PATCH", "/projetos/550e8400e29b41d4a716446655440000", "{ \"name\": \"Projeto\" }")]
    [InlineData("POST", "/projetos/550e8400e29b41d4a716446655440000/tarefas", "{ \"title\": \"Tarefa\", \"priority\": \"high\" }")]
    [InlineData("GET", "/projetos/550e8400e29b41d4a716446655440000/tarefas", null)]
    [InlineData("PATCH", "/tarefas/550e8400e29b41d4a716446655440000", "{ \"title\": \"Tarefa\" }")]
    [InlineData("DELETE", "/tarefas/550e8400e29b41d4a716446655440000", null)]
    public async Task Identifier_Returns400_WhenGuidIsNotCanonical(
        string method,
        string path,
        string? json)
    {
        Assert.Equal(32, NonCanonicalGuid.Length);
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = json is null ? null : JsonRequest(json)
        };

        using var response = await _client.SendAsync(request);

        await AssertValidationProblemAsync(response);
    }

    [Theory]
    [InlineData("POST", "/projetos", "/projetos", "text/json")]
    [InlineData("POST", "/projetos/550e8400-e29b-41d4-a716-446655440000/tarefas", "/projetos/{id}/tarefas", "application/vnd.taskflow+json")]
    [InlineData("PATCH", "/projetos/550e8400-e29b-41d4-a716-446655440000", "/projetos/{id}", "text/json")]
    [InlineData("PATCH", "/tarefas/550e8400-e29b-41d4-a716-446655440000", "/tarefas/{id}", "application/vnd.taskflow+json")]
    public async Task BodyEndpoint_Returns415_WhenMediaTypeIsNotDeclared(
        string method,
        string path,
        string pathTemplate,
        string mediaType)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = new StringContent("{}", Encoding.UTF8)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
        await _validator.AssertResponseAsync(
            pathTemplate,
            new HttpMethod(method),
            response);
    }

    [Fact]
    public async Task ProjectName_UsesJsonSchemaUnicodeLength()
    {
        var validName = string.Concat(Enumerable.Repeat("😀", 100));
        var invalidName = validName + "😀";

        using var validResponse = await _client.PostAsJsonAsync(
            "/projetos",
            new { name = validName });
        Assert.Equal(HttpStatusCode.Created, validResponse.StatusCode);

        using var invalidResponse = await _client.PostAsJsonAsync(
            "/projetos",
            new { name = invalidName });
        await AssertValidationProblemAsync(invalidResponse);
    }

    [Fact]
    public async Task RequiredText_AcceptsWhitespaceBecauseOpenApiOnlyDefinesMinLength()
    {
        using var projectResponse = await _client.PostAsJsonAsync(
            "/projetos",
            new { name = " " });
        Assert.Equal(HttpStatusCode.Created, projectResponse.StatusCode);

        var project = await projectResponse.Content.ReadFromJsonAsync<IdentifierResponse>();
        var projectId = Assert.IsType<Guid>(project?.Id);

        using var taskResponse = await _client.PostAsJsonAsync(
            $"/projetos/{projectId}/tarefas",
            new { title = " ", priority = "low" });
        Assert.Equal(HttpStatusCode.Created, taskResponse.StatusCode);
    }

    [Fact]
    public async Task TaskTitle_UsesJsonSchemaUnicodeLength()
    {
        var projectId = await CreateProjectAsync();
        var validTitle = string.Concat(Enumerable.Repeat("😀", 200));
        var invalidTitle = validTitle + "😀";

        using var validResponse = await _client.PostAsJsonAsync(
            $"/projetos/{projectId}/tarefas",
            new { title = validTitle, priority = "high" });
        Assert.Equal(HttpStatusCode.Created, validResponse.StatusCode);

        using var invalidResponse = await _client.PostAsJsonAsync(
            $"/projetos/{projectId}/tarefas",
            new { title = invalidTitle, priority = "high" });
        await AssertValidationProblemAsync(invalidResponse);
    }

    [Fact]
    public async Task Startup_AppliesInitialMigrationToEmptySqliteDatabase()
    {
        using var response = await _client.GetAsync("/projetos");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var applied = await dbContext.Database.GetAppliedMigrationsAsync();
            Assert.Contains(applied, migration => migration.EndsWith("_InitialCreate"));
            Assert.Empty(await dbContext.Database.GetPendingMigrationsAsync());
        });
    }

    private async Task<Guid> CreateProjectAsync()
    {
        using var response = await _client.PostAsJsonAsync(
            "/projetos",
            new { name = $"Projeto-{Guid.NewGuid()}" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<IdentifierResponse>();
        return Assert.IsType<Guid>(body?.Id);
    }

    private static StringContent JsonRequest(string json) =>
        new(json, Encoding.UTF8, "application/json");

    private static async Task AssertValidationProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    private sealed record IdentifierResponse(Guid Id);
}
