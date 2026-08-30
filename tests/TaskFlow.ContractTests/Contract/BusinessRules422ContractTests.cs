using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TaskFlow.Api.Contracts.Projects;
using TaskFlow.Api.Contracts.Tasks;
using TaskFlow.Api.Domain.Enums;
using TaskFlow.Api.Domain.Errors;
using TaskFlow.Api.Infrastructure.Serialization;
using TaskStatus = TaskFlow.Api.Domain.Enums.TaskStatus;

namespace TaskFlow.ContractTests;

public sealed class BusinessRules422ContractTests(TaskFlowApiFactory factory)
    : IClassFixture<TaskFlowApiFactory>
{
    private const string ProjectHasInProgressTasksDetail =
        "O projeto não pode ser arquivado enquanto possuir tarefas em andamento.";

    private const string ProjectArchivedDetail =
        "Não é permitido criar novas tarefas em um projeto arquivado.";

    private const string TaskCannotBeDeletedDetail =
        "Somente tarefas com status pending podem ser excluídas.";

    private const string InvalidTaskStatusTransitionDetail =
        "A transição de status solicitada não é permitida.";

    private static readonly JsonSerializerOptions ResponseJsonOptions = CreateResponseJsonOptions();

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ArchiveProject_Returns422_WhenItContainsInProgressTask()
    {
        var project = await CreateProjectAsync();
        var task = await CreateTaskAsync(project.Id);
        await UpdateTaskStatusAsync(task.Id, "in_progress");

        using var response = await _client.PatchAsJsonAsync(
            $"/projetos/{project.Id}",
            new { status = "archived" });

        await AssertBusinessProblemAsync(
            response,
            ErrorCodes.ProjectHasInProgressTasks,
            ProjectHasInProgressTasksDetail);
    }

    [Fact]
    public async Task CreateTask_Returns422_WhenProjectIsArchived()
    {
        var project = await CreateProjectAsync();
        using var archiveResponse = await _client.PatchAsJsonAsync(
            $"/projetos/{project.Id}",
            new { status = "archived" });
        Assert.Equal(HttpStatusCode.OK, archiveResponse.StatusCode);

        using var response = await _client.PostAsJsonAsync(
            $"/projetos/{project.Id}/tarefas",
            ValidTaskRequest());

        await AssertBusinessProblemAsync(
            response,
            ErrorCodes.ProjectArchived,
            ProjectArchivedDetail);
    }

    [Fact]
    public async Task DeleteTask_Returns422_WhenTaskIsInProgress()
    {
        var project = await CreateProjectAsync();
        var task = await CreateTaskAsync(project.Id);
        await UpdateTaskStatusAsync(task.Id, "in_progress");

        using var response = await _client.DeleteAsync($"/tarefas/{task.Id}");

        await AssertBusinessProblemAsync(
            response,
            ErrorCodes.TaskCannotBeDeleted,
            TaskCannotBeDeletedDetail);
    }

    [Fact]
    public async Task DeleteTask_Returns422_WhenTaskIsDone()
    {
        var project = await CreateProjectAsync();
        var task = await CreateTaskAsync(project.Id);
        await UpdateTaskStatusAsync(task.Id, "in_progress");
        await UpdateTaskStatusAsync(task.Id, "done");

        using var response = await _client.DeleteAsync($"/tarefas/{task.Id}");

        await AssertBusinessProblemAsync(
            response,
            ErrorCodes.TaskCannotBeDeleted,
            TaskCannotBeDeletedDetail);
    }

    [Fact]
    public async Task UpdateTask_Returns422_ForPendingToDone()
    {
        var project = await CreateProjectAsync();
        var task = await CreateTaskAsync(project.Id);

        using var response = await _client.PatchAsJsonAsync(
            $"/tarefas/{task.Id}",
            new { status = "done" });

        await AssertInvalidTransitionAsync(response);
    }

    [Fact]
    public async Task UpdateTask_Returns422_ForInProgressToPending()
    {
        var project = await CreateProjectAsync();
        var task = await CreateTaskAsync(project.Id);
        await UpdateTaskStatusAsync(task.Id, "in_progress");

        using var response = await _client.PatchAsJsonAsync(
            $"/tarefas/{task.Id}",
            new { status = "pending" });

        await AssertInvalidTransitionAsync(response);
    }

    [Fact]
    public async Task UpdateTask_Returns422_ForDoneToInProgress()
    {
        var project = await CreateProjectAsync();
        var task = await CreateTaskAsync(project.Id);
        await UpdateTaskStatusAsync(task.Id, "in_progress");
        await UpdateTaskStatusAsync(task.Id, "done");

        using var response = await _client.PatchAsJsonAsync(
            $"/tarefas/{task.Id}",
            new { status = "in_progress" });

        await AssertInvalidTransitionAsync(response);
    }

    [Fact]
    public async Task UpdateTask_Returns422_ForDoneToPending()
    {
        var project = await CreateProjectAsync();
        var task = await CreateTaskAsync(project.Id);
        await UpdateTaskStatusAsync(task.Id, "in_progress");
        await UpdateTaskStatusAsync(task.Id, "done");

        using var response = await _client.PatchAsJsonAsync(
            $"/tarefas/{task.Id}",
            new { status = "pending" });

        await AssertInvalidTransitionAsync(response);
    }

    [Fact]
    public async Task UpdateTask_CompletesPendingToInProgressToDone_AndSetsCompletedAtOnlyOnDone()
    {
        var project = await CreateProjectAsync();
        var pending = await CreateTaskAsync(project.Id);
        Assert.Equal(TaskStatus.Pending, pending.Status);
        Assert.Null(pending.CompletedAt);

        var inProgress = await UpdateTaskStatusAsync(pending.Id, "in_progress");
        Assert.Equal(TaskStatus.InProgress, inProgress.Status);
        Assert.Null(inProgress.CompletedAt);

        var done = await UpdateTaskStatusAsync(pending.Id, "done");
        Assert.Equal(TaskStatus.Done, done.Status);
        Assert.Equal(TaskFlowApiFactory.UtcNow, done.CompletedAt);
        Assert.Equal(TimeSpan.Zero, done.CompletedAt?.Offset);
    }

    private async Task<ProjectResponse> CreateProjectAsync()
    {
        using var response = await _client.PostAsJsonAsync(
            "/projetos",
            new { name = $"Projeto-{Guid.NewGuid()}" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<ProjectResponse>(response);
    }

    private async Task<TaskResponse> CreateTaskAsync(Guid projectId)
    {
        using var response = await _client.PostAsJsonAsync(
            $"/projetos/{projectId}/tarefas",
            ValidTaskRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadAsync<TaskResponse>(response);
    }

    private async Task<TaskResponse> UpdateTaskStatusAsync(Guid taskId, string status)
    {
        using var response = await _client.PatchAsJsonAsync(
            $"/tarefas/{taskId}",
            new { status });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadAsync<TaskResponse>(response);
    }

    private static object ValidTaskRequest() => new
    {
        title = $"Tarefa-{Guid.NewGuid()}",
        priority = "medium"
    };

    private static Task AssertInvalidTransitionAsync(HttpResponseMessage response) =>
        AssertBusinessProblemAsync(
            response,
            ErrorCodes.InvalidTaskStatusTransition,
            InvalidTaskStatusTransitionDetail);

    private static async Task AssertBusinessProblemAsync(
        HttpResponseMessage response,
        string expectedCode,
        string expectedDetail)
    {
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal(
            "application/problem+json",
            response.Content.Headers.ContentType?.MediaType);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var problem = document.RootElement;

        Assert.Equal(422, problem.GetProperty("status").GetInt32());
        Assert.Equal(expectedCode, problem.GetProperty("code").GetString());
        Assert.Equal(expectedDetail, problem.GetProperty("detail").GetString());
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(ResponseJsonOptions);
        return Assert.IsType<T>(value);
    }

    private static JsonSerializerOptions CreateResponseJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new StrictSnakeCaseLowerEnumConverterFactory());
        return options;
    }
}
