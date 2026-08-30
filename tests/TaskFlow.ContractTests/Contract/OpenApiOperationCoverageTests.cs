using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace TaskFlow.ContractTests;

public sealed class OpenApiOperationCoverageTests(TaskFlowApiFactory factory)
    : IClassFixture<TaskFlowApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly OpenApiResponseValidator _validator = new();

    [Fact]
    public async Task OpenApiDocument_IsStructurallyValid()
    {
        await OpenApiResponseValidator.AssertDocumentIsValidAsync();
    }

    [Fact]
    public async Task CreateProject_ValidatesEveryDeclaredResponseSchema()
    {
        using var created = await _client.PostAsJsonAsync(
            "/projetos",
            new { name = $"Projeto-{Guid.NewGuid()}" });
        await AssertSchemaAsync("/projetos", HttpMethod.Post, HttpStatusCode.Created, created);

        using var invalid = await _client.PostAsJsonAsync("/projetos", new { });
        await AssertSchemaAsync("/projetos", HttpMethod.Post, HttpStatusCode.BadRequest, invalid);
    }

    [Fact]
    public async Task ListProjects_ValidatesEveryDeclaredResponseSchema()
    {
        using var success = await _client.GetAsync("/projetos?status=active");
        await AssertSchemaAsync("/projetos", HttpMethod.Get, HttpStatusCode.OK, success);

        using var invalid = await _client.GetAsync("/projetos?status=ACTIVE");
        await AssertSchemaAsync("/projetos", HttpMethod.Get, HttpStatusCode.BadRequest, invalid);
    }

    [Fact]
    public async Task GetProject_ValidatesEveryDeclaredResponseSchema()
    {
        var projectId = await CreateProjectAsync();

        using var success = await _client.GetAsync($"/projetos/{projectId}");
        await AssertSchemaAsync("/projetos/{id}", HttpMethod.Get, HttpStatusCode.OK, success);

        using var invalid = await _client.GetAsync("/projetos/uuid-invalido");
        await AssertSchemaAsync("/projetos/{id}", HttpMethod.Get, HttpStatusCode.BadRequest, invalid);

        using var missing = await _client.GetAsync($"/projetos/{Guid.NewGuid()}");
        await AssertSchemaAsync("/projetos/{id}", HttpMethod.Get, HttpStatusCode.NotFound, missing);
    }

    [Fact]
    public async Task UpdateProject_ValidatesEveryDeclaredResponseSchema()
    {
        var projectId = await CreateProjectAsync();

        using var success = await _client.PatchAsJsonAsync(
            $"/projetos/{projectId}",
            new { name = "Projeto atualizado" });
        await AssertSchemaAsync("/projetos/{id}", HttpMethod.Patch, HttpStatusCode.OK, success);

        using var invalid = await _client.PatchAsJsonAsync(
            $"/projetos/{projectId}",
            new { });
        await AssertSchemaAsync("/projetos/{id}", HttpMethod.Patch, HttpStatusCode.BadRequest, invalid);

        using var missing = await _client.PatchAsJsonAsync(
            $"/projetos/{Guid.NewGuid()}",
            new { name = "Projeto" });
        await AssertSchemaAsync("/projetos/{id}", HttpMethod.Patch, HttpStatusCode.NotFound, missing);

        var blockedProjectId = await CreateProjectAsync();
        var taskId = await CreateTaskAsync(blockedProjectId);
        using var start = await _client.PatchAsJsonAsync(
            $"/tarefas/{taskId}",
            new { status = "in_progress" });
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);

        using var blocked = await _client.PatchAsJsonAsync(
            $"/projetos/{blockedProjectId}",
            new { status = "archived" });
        await AssertSchemaAsync(
            "/projetos/{id}",
            HttpMethod.Patch,
            HttpStatusCode.UnprocessableEntity,
            blocked);
    }

    [Fact]
    public async Task CreateTask_ValidatesEveryDeclaredResponseSchema()
    {
        var projectId = await CreateProjectAsync();

        using var created = await _client.PostAsJsonAsync(
            $"/projetos/{projectId}/tarefas",
            ValidTask());
        await AssertSchemaAsync(
            "/projetos/{id}/tarefas",
            HttpMethod.Post,
            HttpStatusCode.Created,
            created);

        using var invalid = await _client.PostAsJsonAsync(
            $"/projetos/{projectId}/tarefas",
            new { title = "Sem prioridade" });
        await AssertSchemaAsync(
            "/projetos/{id}/tarefas",
            HttpMethod.Post,
            HttpStatusCode.BadRequest,
            invalid);

        using var missing = await _client.PostAsJsonAsync(
            $"/projetos/{Guid.NewGuid()}/tarefas",
            ValidTask());
        await AssertSchemaAsync(
            "/projetos/{id}/tarefas",
            HttpMethod.Post,
            HttpStatusCode.NotFound,
            missing);

        var archivedProjectId = await CreateProjectAsync();
        using var archive = await _client.PatchAsJsonAsync(
            $"/projetos/{archivedProjectId}",
            new { status = "archived" });
        Assert.Equal(HttpStatusCode.OK, archive.StatusCode);

        using var blocked = await _client.PostAsJsonAsync(
            $"/projetos/{archivedProjectId}/tarefas",
            ValidTask());
        await AssertSchemaAsync(
            "/projetos/{id}/tarefas",
            HttpMethod.Post,
            HttpStatusCode.UnprocessableEntity,
            blocked);
    }

    [Fact]
    public async Task ListTasks_ValidatesEveryDeclaredResponseSchema()
    {
        var projectId = await CreateProjectAsync();

        using var success = await _client.GetAsync(
            $"/projetos/{projectId}/tarefas?status=pending&priority=medium");
        await AssertSchemaAsync(
            "/projetos/{id}/tarefas",
            HttpMethod.Get,
            HttpStatusCode.OK,
            success);

        using var invalid = await _client.GetAsync(
            $"/projetos/{projectId}/tarefas?priority=URGENT");
        await AssertSchemaAsync(
            "/projetos/{id}/tarefas",
            HttpMethod.Get,
            HttpStatusCode.BadRequest,
            invalid);

        using var missing = await _client.GetAsync(
            $"/projetos/{Guid.NewGuid()}/tarefas");
        await AssertSchemaAsync(
            "/projetos/{id}/tarefas",
            HttpMethod.Get,
            HttpStatusCode.NotFound,
            missing);
    }

    [Fact]
    public async Task UpdateTask_ValidatesEveryDeclaredResponseSchema()
    {
        var projectId = await CreateProjectAsync();
        var taskId = await CreateTaskAsync(projectId);

        using var success = await _client.PatchAsJsonAsync(
            $"/tarefas/{taskId}",
            new { title = "Tarefa atualizada" });
        await AssertSchemaAsync("/tarefas/{id}", HttpMethod.Patch, HttpStatusCode.OK, success);

        using var invalid = await _client.PatchAsJsonAsync($"/tarefas/{taskId}", new { });
        await AssertSchemaAsync(
            "/tarefas/{id}",
            HttpMethod.Patch,
            HttpStatusCode.BadRequest,
            invalid);

        using var missing = await _client.PatchAsJsonAsync(
            $"/tarefas/{Guid.NewGuid()}",
            new { title = "Tarefa" });
        await AssertSchemaAsync(
            "/tarefas/{id}",
            HttpMethod.Patch,
            HttpStatusCode.NotFound,
            missing);

        var blockedTaskId = await CreateTaskAsync(projectId);
        using var blocked = await _client.PatchAsJsonAsync(
            $"/tarefas/{blockedTaskId}",
            new { status = "done" });
        await AssertSchemaAsync(
            "/tarefas/{id}",
            HttpMethod.Patch,
            HttpStatusCode.UnprocessableEntity,
            blocked);
    }

    [Fact]
    public async Task DeleteTask_ValidatesEveryDeclaredResponseSchema()
    {
        var projectId = await CreateProjectAsync();
        var pendingTaskId = await CreateTaskAsync(projectId);

        using var deleted = await _client.DeleteAsync($"/tarefas/{pendingTaskId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        await _validator.AssertEmptyResponseAsync(
            "/tarefas/{id}",
            HttpMethod.Delete,
            deleted);

        using var invalid = await _client.DeleteAsync("/tarefas/uuid-invalido");
        await AssertSchemaAsync(
            "/tarefas/{id}",
            HttpMethod.Delete,
            HttpStatusCode.BadRequest,
            invalid);

        using var missing = await _client.DeleteAsync($"/tarefas/{Guid.NewGuid()}");
        await AssertSchemaAsync(
            "/tarefas/{id}",
            HttpMethod.Delete,
            HttpStatusCode.NotFound,
            missing);

        var startedTaskId = await CreateTaskAsync(projectId);
        using var start = await _client.PatchAsJsonAsync(
            $"/tarefas/{startedTaskId}",
            new { status = "in_progress" });
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);

        using var blocked = await _client.DeleteAsync($"/tarefas/{startedTaskId}");
        await AssertSchemaAsync(
            "/tarefas/{id}",
            HttpMethod.Delete,
            HttpStatusCode.UnprocessableEntity,
            blocked);
    }

    private async Task AssertSchemaAsync(
        string path,
        HttpMethod method,
        HttpStatusCode expectedStatus,
        HttpResponseMessage response)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        await _validator.AssertResponseAsync(path, method, response);
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
            ValidTask());
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadIdAsync(response);
    }

    private static object ValidTask() => new
    {
        title = $"Tarefa-{Guid.NewGuid()}",
        priority = "medium"
    };

    private static async Task<Guid> ReadIdAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.GetProperty("id").GetGuid();
    }
}
