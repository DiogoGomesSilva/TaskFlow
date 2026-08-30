using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TaskFlow.Api.Contracts.Projects;
using TaskFlow.Api.Contracts.Tasks;
using TaskFlow.Api.Domain.Enums;
using TaskFlow.Api.Infrastructure.Serialization;
using TaskStatus = TaskFlow.Api.Domain.Enums.TaskStatus;

namespace TaskFlow.ContractTests;

public sealed class HappyPathContractTests(TaskFlowApiFactory factory)
    : IClassFixture<TaskFlowApiFactory>
{
    private static readonly JsonSerializerOptions ResponseJsonOptions = CreateResponseJsonOptions();

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateProject_Returns201WithBodyAndLocation()
    {
        using var response = await _client.PostAsJsonAsync(
            "/projetos",
            new
            {
                name = "Projeto de integração",
                description = "Criado pela API real."
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        AssertJsonContentType(response);

        var project = await ReadProjectAsync(response);
        Assert.NotEqual(Guid.Empty, project.Id);
        Assert.Equal("Projeto de integração", project.Name);
        Assert.Equal("Criado pela API real.", project.Description);
        Assert.Equal(ProjectStatus.Active, project.Status);
        Assert.Equal(TaskFlowApiFactory.UtcNow, project.CreatedAt);
        AssertLocationPath(response, $"/projetos/{project.Id}");
    }

    [Fact]
    public async Task ListProjects_Returns200WithJsonArray()
    {
        var created = await CreateProjectAsync();

        using var response = await _client.GetAsync("/projetos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertJsonContentType(response);

        var projects = await response.Content.ReadFromJsonAsync<List<ProjectResponse>>(
            ResponseJsonOptions);
        Assert.NotNull(projects);
        Assert.Contains(projects, project => project.Id == created.Id);
    }

    [Fact]
    public async Task GetProject_Returns200WithRequestedProject()
    {
        var created = await CreateProjectAsync();

        using var response = await _client.GetAsync($"/projetos/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertJsonContentType(response);

        var project = await ReadProjectAsync(response);
        Assert.Equal(created, project);
    }

    [Fact]
    public async Task UpdateProject_Returns200WithUpdatedProject()
    {
        var created = await CreateProjectAsync();

        using var response = await _client.PatchAsJsonAsync(
            $"/projetos/{created.Id}",
            new { name = "Projeto atualizado" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertJsonContentType(response);

        var project = await ReadProjectAsync(response);
        Assert.Equal(created.Id, project.Id);
        Assert.Equal("Projeto atualizado", project.Name);
        Assert.Equal(created.Description, project.Description);
    }

    [Fact]
    public async Task CreateTask_Returns201WithBodyAndLocation()
    {
        var project = await CreateProjectAsync();

        using var response = await _client.PostAsJsonAsync(
            $"/projetos/{project.Id}/tarefas",
            new
            {
                title = "Tarefa de integração",
                description = "Criada pela API real.",
                priority = "high"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        AssertJsonContentType(response);

        var task = await ReadTaskAsync(response);
        Assert.NotEqual(Guid.Empty, task.Id);
        Assert.Equal(project.Id, task.ProjectId);
        Assert.Equal("Tarefa de integração", task.Title);
        Assert.Equal(TaskStatus.Pending, task.Status);
        Assert.Equal(TaskPriority.High, task.Priority);
        Assert.Null(task.CompletedAt);
        AssertLocationPath(response, $"/tarefas/{task.Id}");
    }

    [Fact]
    public async Task ListTasks_Returns200WithJsonArray()
    {
        var project = await CreateProjectAsync();
        var created = await CreateTaskAsync(project.Id);

        using var response = await _client.GetAsync($"/projetos/{project.Id}/tarefas");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertJsonContentType(response);

        var tasks = await response.Content.ReadFromJsonAsync<List<TaskResponse>>(
            ResponseJsonOptions);
        Assert.NotNull(tasks);
        Assert.Contains(tasks, task => task.Id == created.Id && task.ProjectId == project.Id);
    }

    [Fact]
    public async Task UpdateTask_Returns200WithUpdatedTask()
    {
        var project = await CreateProjectAsync();
        var created = await CreateTaskAsync(project.Id);

        using var response = await _client.PatchAsJsonAsync(
            $"/tarefas/{created.Id}",
            new
            {
                title = "Tarefa atualizada",
                status = "in_progress"
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertJsonContentType(response);

        var task = await ReadTaskAsync(response);
        Assert.Equal(created.Id, task.Id);
        Assert.Equal("Tarefa atualizada", task.Title);
        Assert.Equal(TaskStatus.InProgress, task.Status);
        Assert.Null(task.CompletedAt);
    }

    [Fact]
    public async Task DeletePendingTask_Returns204WithEmptyBody()
    {
        var project = await CreateProjectAsync();
        var task = await CreateTaskAsync(project.Id);

        using var response = await _client.DeleteAsync($"/tarefas/{task.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(response.Content.Headers.ContentType);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    private async Task<ProjectResponse> CreateProjectAsync()
    {
        using var response = await _client.PostAsJsonAsync(
            "/projetos",
            new
            {
                name = $"Projeto-{Guid.NewGuid()}",
                description = "Projeto auxiliar do teste."
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        AssertJsonContentType(response);
        Assert.NotNull(response.Headers.Location);

        return await ReadProjectAsync(response);
    }

    private async Task<TaskResponse> CreateTaskAsync(Guid projectId)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/projetos/{projectId}/tarefas",
            new
            {
                title = $"Tarefa-{Guid.NewGuid()}",
                description = "Tarefa auxiliar do teste.",
                priority = "medium"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        AssertJsonContentType(response);
        Assert.NotNull(response.Headers.Location);

        return await ReadTaskAsync(response);
    }

    private static async Task<ProjectResponse> ReadProjectAsync(HttpResponseMessage response)
    {
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(
            ResponseJsonOptions);
        return Assert.IsType<ProjectResponse>(project);
    }

    private static async Task<TaskResponse> ReadTaskAsync(HttpResponseMessage response)
    {
        var task = await response.Content.ReadFromJsonAsync<TaskResponse>(ResponseJsonOptions);
        return Assert.IsType<TaskResponse>(task);
    }

    private static void AssertJsonContentType(HttpResponseMessage response)
    {
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    private static void AssertLocationPath(HttpResponseMessage response, string expectedPath)
    {
        Assert.NotNull(response.Headers.Location);
        var path = response.Headers.Location.IsAbsoluteUri
            ? response.Headers.Location.AbsolutePath
            : response.Headers.Location.OriginalString;
        Assert.Equal(expectedPath, path);
    }

    private static JsonSerializerOptions CreateResponseJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new StrictSnakeCaseLowerEnumConverterFactory());
        return options;
    }
}
