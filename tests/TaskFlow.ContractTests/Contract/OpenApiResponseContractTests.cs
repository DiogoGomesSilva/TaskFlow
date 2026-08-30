using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace TaskFlow.ContractTests;

public sealed class OpenApiResponseContractTests(TaskFlowApiFactory factory)
    : IClassFixture<TaskFlowApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly OpenApiResponseValidator _validator = new();

    [Fact]
    public async Task GetProject_Response200_MatchesOpenApiSchema()
    {
        var projectId = await CreateProjectAsync();

        using var response = await _client.GetAsync($"/projetos/{projectId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await _validator.AssertResponseAsync(
            "/projetos/{id}",
            HttpMethod.Get,
            response);
    }

    [Fact]
    public async Task CreateProject_Response201_MatchesOpenApiSchema()
    {
        using var response = await _client.PostAsJsonAsync(
            "/projetos",
            new { name = $"Projeto-{Guid.NewGuid()}" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await _validator.AssertResponseAsync("/projetos", HttpMethod.Post, response);
    }

    [Fact]
    public async Task CreateProject_Response400_MatchesOpenApiSchema()
    {
        using var response = await _client.PostAsJsonAsync("/projetos", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await _validator.AssertResponseAsync("/projetos", HttpMethod.Post, response);
    }

    [Fact]
    public async Task GetProject_Response404_MatchesOpenApiSchema()
    {
        using var response = await _client.GetAsync($"/projetos/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await _validator.AssertResponseAsync(
            "/projetos/{id}",
            HttpMethod.Get,
            response);
    }

    [Fact]
    public async Task CreateTask_Response422_MatchesOpenApiSchema()
    {
        var projectId = await CreateProjectAsync();
        using var archiveResponse = await _client.PatchAsJsonAsync(
            $"/projetos/{projectId}",
            new { status = "archived" });
        Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);

        using var response = await _client.PostAsJsonAsync(
            $"/projetos/{projectId}/tarefas",
            new { title = "Tarefa", priority = "medium" });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        await _validator.AssertResponseAsync(
            "/projetos/{id}/tarefas",
            HttpMethod.Post,
            response);
    }

    [Fact]
    public async Task DeleteTask_Response204_HasNoBodyAsDefinedByOpenApi()
    {
        var projectId = await CreateProjectAsync();
        var taskId = await CreateTaskAsync(projectId);

        using var response = await _client.DeleteAsync($"/tarefas/{taskId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await _validator.AssertEmptyResponseAsync(
            "/tarefas/{id}",
            HttpMethod.Delete,
            response);
    }

    private async Task<Guid> CreateProjectAsync()
    {
        using var response = await _client.PostAsJsonAsync(
            "/projetos",
            new { name = $"Projeto-{Guid.NewGuid()}" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await ReadIdAsync(response);
    }

    private async Task<Guid> CreateTaskAsync(Guid projectId)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/projetos/{projectId}/tarefas",
            new
            {
                title = $"Tarefa-{Guid.NewGuid()}",
                priority = "medium"
            });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return await ReadIdAsync(response);
    }

    private static async Task<Guid> ReadIdAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.GetProperty("id").GetGuid();
    }
}
