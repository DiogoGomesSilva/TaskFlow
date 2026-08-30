using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TaskFlow.Api.Contracts.Projects;
using TaskFlow.Api.Contracts.Tasks;
using TaskFlow.Api.Domain.Errors;
using TaskFlow.Api.Infrastructure.Serialization;

namespace TaskFlow.ContractTests;

public sealed class BadRequestAndNotFoundContractTests(TaskFlowApiFactory factory)
    : IClassFixture<TaskFlowApiFactory>
{
    private const string ValidationType =
        "https://tools.ietf.org/html/rfc9110#section-15.5.1";

    private const string ValidationTitle = "One or more validation errors occurred.";
    private const string ValidationDetail = "Um ou mais campos são inválidos.";

    private const string ProjectNotFoundType =
        "https://taskflow/errors/project-not-found";

    private const string ProjectNotFoundTitle = "Project not found";
    private const string ProjectNotFoundDetail =
        "O projeto informado não foi encontrado.";

    private const string TaskNotFoundType = "https://taskflow/errors/task-not-found";
    private const string TaskNotFoundTitle = "Task not found";
    private const string TaskNotFoundDetail =
        "A tarefa informada não foi encontrada.";

    private static readonly JsonSerializerOptions ResponseJsonOptions =
        CreateResponseJsonOptions();

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task CreateProject_Returns400_WhenNameIsMissing()
    {
        using var response = await _client.PostAsJsonAsync("/projetos", new { });

        await AssertValidationProblemAsync(response, "/projetos");
    }

    [Fact]
    public async Task CreateProject_Returns400_WhenNameExceeds100Characters()
    {
        using var response = await _client.PostAsJsonAsync(
            "/projetos",
            new { name = new string('a', 101) });

        await AssertValidationProblemAsync(response, "/projetos");
    }

    [Fact]
    public async Task CreateTask_Returns400_WhenTitleIsMissing()
    {
        var project = await CreateProjectAsync();

        using var response = await _client.PostAsJsonAsync(
            $"/projetos/{project.Id}/tarefas",
            new { priority = "medium" });

        await AssertValidationProblemAsync(
            response,
            $"/projetos/{project.Id}/tarefas");
    }

    [Fact]
    public async Task CreateTask_Returns400_WhenTitleExceeds200Characters()
    {
        var project = await CreateProjectAsync();

        using var response = await _client.PostAsJsonAsync(
            $"/projetos/{project.Id}/tarefas",
            new { title = new string('a', 201), priority = "medium" });

        await AssertValidationProblemAsync(
            response,
            $"/projetos/{project.Id}/tarefas");
    }

    [Fact]
    public async Task CreateTask_Returns400_WhenPriorityIsMissing()
    {
        var project = await CreateProjectAsync();

        using var response = await _client.PostAsJsonAsync(
            $"/projetos/{project.Id}/tarefas",
            new { title = "Tarefa sem prioridade" });

        await AssertValidationProblemAsync(
            response,
            $"/projetos/{project.Id}/tarefas");
    }

    [Fact]
    public async Task CreateTask_Returns400_WhenEnumIsInvalid()
    {
        var project = await CreateProjectAsync();

        using var response = await _client.PostAsJsonAsync(
            $"/projetos/{project.Id}/tarefas",
            new { title = "Tarefa", priority = "urgent" });

        await AssertValidationProblemAsync(
            response,
            $"/projetos/{project.Id}/tarefas");
    }

    [Fact]
    public async Task GetProject_Returns400_WhenUuidIsInvalid()
    {
        using var response = await _client.GetAsync("/projetos/uuid-invalido");

        await AssertValidationProblemAsync(response, "/projetos/uuid-invalido");
    }

    [Fact]
    public async Task CreateProject_Returns400_WhenBodyIsMalformed()
    {
        using var body = new StringContent("{", Encoding.UTF8, "application/json");
        using var response = await _client.PostAsync("/projetos", body);

        await AssertValidationProblemAsync(response, "/projetos");
    }

    [Fact]
    public async Task UpdateProject_Returns400_WhenPatchIsEmpty()
    {
        var project = await CreateProjectAsync();

        using var response = await _client.PatchAsJsonAsync(
            $"/projetos/{project.Id}",
            new { });

        await AssertValidationProblemAsync(response, $"/projetos/{project.Id}");
    }

    [Fact]
    public async Task UpdateTask_Returns400_WhenCompletedAtIsSent()
    {
        var project = await CreateProjectAsync();
        var task = await CreateTaskAsync(project.Id);

        using var response = await _client.PatchAsJsonAsync(
            $"/tarefas/{task.Id}",
            new { completedAt = "2026-08-29T20:30:00Z" });

        await AssertValidationProblemAsync(response, $"/tarefas/{task.Id}");
    }

    [Fact]
    public async Task GetProject_Returns404_WhenProjectDoesNotExist()
    {
        var projectId = Guid.NewGuid();

        using var response = await _client.GetAsync($"/projetos/{projectId}");

        await AssertProjectNotFoundAsync(response, $"/projetos/{projectId}");
    }

    [Fact]
    public async Task UpdateProject_Returns404_WhenProjectDoesNotExist()
    {
        var projectId = Guid.NewGuid();

        using var response = await _client.PatchAsJsonAsync(
            $"/projetos/{projectId}",
            new { name = "Projeto atualizado" });

        await AssertProjectNotFoundAsync(response, $"/projetos/{projectId}");
    }

    [Fact]
    public async Task CreateTask_Returns404_WhenProjectDoesNotExist()
    {
        var projectId = Guid.NewGuid();

        using var response = await _client.PostAsJsonAsync(
            $"/projetos/{projectId}/tarefas",
            new { title = "Tarefa", priority = "medium" });

        await AssertProjectNotFoundAsync(
            response,
            $"/projetos/{projectId}/tarefas");
    }

    [Fact]
    public async Task ListTasks_Returns404_WhenProjectDoesNotExist()
    {
        var projectId = Guid.NewGuid();

        using var response = await _client.GetAsync($"/projetos/{projectId}/tarefas");

        await AssertProjectNotFoundAsync(
            response,
            $"/projetos/{projectId}/tarefas");
    }

    [Fact]
    public async Task UpdateTask_Returns404_WhenTaskDoesNotExist()
    {
        var taskId = Guid.NewGuid();

        using var response = await _client.PatchAsJsonAsync(
            $"/tarefas/{taskId}",
            new { title = "Tarefa atualizada" });

        await AssertTaskNotFoundAsync(response, $"/tarefas/{taskId}");
    }

    [Fact]
    public async Task DeleteTask_Returns404_WhenTaskDoesNotExist()
    {
        var taskId = Guid.NewGuid();

        using var response = await _client.DeleteAsync($"/tarefas/{taskId}");

        await AssertTaskNotFoundAsync(response, $"/tarefas/{taskId}");
    }

    private async Task<ProjectResponse> CreateProjectAsync()
    {
        using var response = await _client.PostAsJsonAsync(
            "/projetos",
            new { name = $"Projeto-{Guid.NewGuid()}" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(
            ResponseJsonOptions);
        return Assert.IsType<ProjectResponse>(project);
    }

    private async Task<TaskResponse> CreateTaskAsync(Guid projectId)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/projetos/{projectId}/tarefas",
            new
            {
                title = $"Tarefa-{Guid.NewGuid()}",
                priority = "medium"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var task = await response.Content.ReadFromJsonAsync<TaskResponse>(ResponseJsonOptions);
        return Assert.IsType<TaskResponse>(task);
    }

    private static async Task AssertValidationProblemAsync(
        HttpResponseMessage response,
        string expectedInstance)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertProblemContentType(response);

        using var document = await ReadProblemAsync(response);
        var problem = document.RootElement;

        Assert.Equal(ValidationType, problem.GetProperty("type").GetString());
        Assert.Equal(ValidationTitle, problem.GetProperty("title").GetString());
        Assert.Equal(400, problem.GetProperty("status").GetInt32());
        Assert.Equal(ValidationDetail, problem.GetProperty("detail").GetString());
        Assert.Equal(expectedInstance, problem.GetProperty("instance").GetString());
        Assert.Equal(ErrorCodes.ValidationError, problem.GetProperty("code").GetString());

        var errors = problem.GetProperty("errors");
        Assert.Equal(JsonValueKind.Object, errors.ValueKind);
        Assert.True(errors.EnumerateObject().Any());
        Assert.All(
            errors.EnumerateObject(),
            error => Assert.All(
                error.Value.EnumerateArray(),
                message => Assert.False(string.IsNullOrWhiteSpace(message.GetString()))));
    }

    private static Task AssertProjectNotFoundAsync(
        HttpResponseMessage response,
        string expectedInstance) =>
        AssertNotFoundProblemAsync(
            response,
            expectedInstance,
            ProjectNotFoundType,
            ProjectNotFoundTitle,
            ProjectNotFoundDetail,
            ErrorCodes.ProjectNotFound);

    private static Task AssertTaskNotFoundAsync(
        HttpResponseMessage response,
        string expectedInstance) =>
        AssertNotFoundProblemAsync(
            response,
            expectedInstance,
            TaskNotFoundType,
            TaskNotFoundTitle,
            TaskNotFoundDetail,
            ErrorCodes.TaskNotFound);

    private static async Task AssertNotFoundProblemAsync(
        HttpResponseMessage response,
        string expectedInstance,
        string expectedType,
        string expectedTitle,
        string expectedDetail,
        string expectedCode)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertProblemContentType(response);

        using var document = await ReadProblemAsync(response);
        var problem = document.RootElement;

        Assert.Equal(expectedType, problem.GetProperty("type").GetString());
        Assert.Equal(expectedTitle, problem.GetProperty("title").GetString());
        Assert.Equal(404, problem.GetProperty("status").GetInt32());
        Assert.Equal(expectedDetail, problem.GetProperty("detail").GetString());
        Assert.Equal(expectedInstance, problem.GetProperty("instance").GetString());
        Assert.Equal(expectedCode, problem.GetProperty("code").GetString());
    }

    private static void AssertProblemContentType(HttpResponseMessage response)
    {
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);
    }

    private static async Task<JsonDocument> ReadProblemAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private static JsonSerializerOptions CreateResponseJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new StrictSnakeCaseLowerEnumConverterFactory());
        return options;
    }
}
