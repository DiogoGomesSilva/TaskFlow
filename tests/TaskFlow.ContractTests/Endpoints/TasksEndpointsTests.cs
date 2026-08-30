using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Contracts.Tasks;
using TaskFlow.Api.Domain.Entities;
using TaskFlow.Api.Domain.Enums;
using TaskFlow.Api.Domain.Errors;
using TaskFlow.Api.Infrastructure.Serialization;
using TaskStatus = TaskFlow.Api.Domain.Enums.TaskStatus;

namespace TaskFlow.ContractTests;

public sealed class TasksEndpointsTests(TaskFlowApiFactory factory)
    : IClassFixture<TaskFlowApiFactory>
{
    private static readonly JsonSerializerOptions ResponseJsonOptions = CreateResponseJsonOptions();

    private readonly HttpClient _client = factory.CreateClient();

    public static TheoryData<TaskStatus, string> BackwardTransitions => new()
    {
        { TaskStatus.InProgress, "pending" },
        { TaskStatus.Done, "pending" },
        { TaskStatus.Done, "in_progress" }
    };

    [Fact]
    public async Task Post_ReturnsPendingTaskAndLocation()
    {
        var project = await SeedProjectAsync();

        using var response = await _client.PostAsJsonAsync(
            $"/projetos/{project.Id}/tarefas",
            new
            {
                title = "Implementar autenticação",
                description = "Criar autenticação da aplicação.",
                priority = "high"
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var task = await response.Content.ReadFromJsonAsync<TaskResponse>(ResponseJsonOptions);
        Assert.NotNull(task);
        Assert.Equal($"/tarefas/{task.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal(project.Id, task.ProjectId);
        Assert.Equal(TaskStatus.Pending, task.Status);
        Assert.Equal(TaskPriority.High, task.Priority);
        Assert.Equal(TaskFlowApiFactory.UtcNow, task.CreatedAt);
        Assert.Null(task.CompletedAt);
    }

    [Fact]
    public async Task Post_ReturnsValidationProblem_WhenPriorityIsMissing()
    {
        var project = await SeedProjectAsync();

        using var response = await _client.PostAsync(
            $"/projetos/{project.Id}/tarefas",
            JsonRequest("""{ "title": "Tarefa" }"""));

        await AssertValidationProblemAsync(response);
    }

    [Fact]
    public async Task Post_ReturnsProjectNotFound_WhenProjectDoesNotExist()
    {
        var projectId = Guid.NewGuid();

        using var response = await _client.PostAsync(
            $"/projetos/{projectId}/tarefas",
            JsonRequest("""{ "title": "Tarefa", "priority": "medium" }"""));

        await AssertProblemAsync(
            response,
            HttpStatusCode.NotFound,
            ErrorCodes.ProjectNotFound);
    }

    [Fact]
    public async Task Post_ReturnsBusinessProblem_WhenProjectIsArchived()
    {
        var project = await SeedProjectAsync(ProjectStatus.Archived);

        using var response = await _client.PostAsync(
            $"/projetos/{project.Id}/tarefas",
            JsonRequest("""{ "title": "Tarefa", "priority": "medium" }"""));

        await AssertProblemAsync(
            response,
            HttpStatusCode.UnprocessableEntity,
            ErrorCodes.ProjectArchived);
    }

    [Fact]
    public async Task List_FiltersByStatusAndPriority()
    {
        var project = await SeedProjectAsync();
        var expected = await SeedTaskAsync(project.Id, TaskStatus.InProgress, TaskPriority.High);
        await SeedTaskAsync(project.Id, TaskStatus.Pending, TaskPriority.High);
        await SeedTaskAsync(project.Id, TaskStatus.InProgress, TaskPriority.Low);

        using var response = await _client.GetAsync(
            $"/projetos/{project.Id}/tarefas?status=in_progress&priority=high");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tasks = await response.Content.ReadFromJsonAsync<List<TaskResponse>>(
            ResponseJsonOptions);
        Assert.NotNull(tasks);
        var task = Assert.Single(tasks);
        Assert.Equal(expected.Id, task.Id);
        Assert.Equal(TaskStatus.InProgress, task.Status);
        Assert.Equal(TaskPriority.High, task.Priority);
    }

    [Theory]
    [InlineData("status=IN_PROGRESS")]
    [InlineData("status=0")]
    [InlineData("priority=HIGH")]
    [InlineData("priority=1")]
    public async Task List_ReturnsValidationProblem_WhenFilterIsOutsideContract(string query)
    {
        var project = await SeedProjectAsync();

        using var response = await _client.GetAsync(
            $"/projetos/{project.Id}/tarefas?{query}");

        await AssertValidationProblemAsync(response);
    }

    [Fact]
    public async Task List_ReturnsProjectNotFound_WhenProjectDoesNotExist()
    {
        var projectId = Guid.NewGuid();

        using var response = await _client.GetAsync($"/projetos/{projectId}/tarefas");

        await AssertProblemAsync(
            response,
            HttpStatusCode.NotFound,
            ErrorCodes.ProjectNotFound);
    }

    [Fact]
    public async Task Patch_AllowsPendingToInProgressAndInProgressToDone()
    {
        var project = await SeedProjectAsync();
        var task = await SeedTaskAsync(project.Id);

        using var startResponse = await _client.PatchAsync(
            $"/tarefas/{task.Id}",
            JsonRequest("""{ "status": "in_progress" }"""));

        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);
        var started = await startResponse.Content.ReadFromJsonAsync<TaskResponse>(
            ResponseJsonOptions);
        Assert.NotNull(started);
        Assert.Equal(TaskStatus.InProgress, started.Status);
        Assert.Null(started.CompletedAt);

        using var completeResponse = await _client.PatchAsync(
            $"/tarefas/{task.Id}",
            JsonRequest("""{ "status": "done" }"""));

        Assert.Equal(HttpStatusCode.OK, completeResponse.StatusCode);
        var completed = await completeResponse.Content.ReadFromJsonAsync<TaskResponse>(
            ResponseJsonOptions);
        Assert.NotNull(completed);
        Assert.Equal(TaskStatus.Done, completed.Status);
        Assert.Equal(TaskFlowApiFactory.UtcNow, completed.CompletedAt);
    }

    [Fact]
    public async Task Patch_PreservesCompletedAt_WhenTaskIsAlreadyDone()
    {
        var project = await SeedProjectAsync();
        var completedAt = TaskFlowApiFactory.UtcNow.AddHours(-2);
        var task = TaskItem.Create(
            project.Id,
            "Tarefa concluída",
            null,
            TaskPriority.Medium,
            TaskFlowApiFactory.UtcNow.AddHours(-3));
        Assert.True(task.TransitionTo(TaskStatus.InProgress, completedAt.AddHours(-1)).IsSuccess);
        Assert.True(task.TransitionTo(TaskStatus.Done, completedAt).IsSuccess);

        await factory.ExecuteDbContextAsync(async dbContext =>
        {
            dbContext.Tasks.Add(task);
            await dbContext.SaveChangesAsync();
        });

        using var response = await _client.PatchAsync(
            $"/tarefas/{task.Id}",
            JsonRequest("""{ "status": "done" }"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<TaskResponse>(
            ResponseJsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(completedAt, updated.CompletedAt);
    }

    [Fact]
    public async Task Patch_ReturnsBusinessProblem_ForPendingToDone()
    {
        var project = await SeedProjectAsync();
        var task = await SeedTaskAsync(project.Id);

        using var response = await _client.PatchAsync(
            $"/tarefas/{task.Id}",
            JsonRequest("""{ "status": "done" }"""));

        await AssertProblemAsync(
            response,
            HttpStatusCode.UnprocessableEntity,
            ErrorCodes.InvalidTaskStatusTransition);

        await AssertTaskStatusAsync(task.Id, TaskStatus.Pending);
    }

    [Theory]
    [MemberData(nameof(BackwardTransitions))]
    public async Task Patch_ReturnsBusinessProblem_ForBackwardTransition(
        TaskStatus currentStatus,
        string requestedStatus)
    {
        var project = await SeedProjectAsync();
        var task = await SeedTaskAsync(project.Id, currentStatus);

        using var response = await _client.PatchAsync(
            $"/tarefas/{task.Id}",
            JsonRequest($"{{ \"status\": \"{requestedStatus}\" }}"));

        await AssertProblemAsync(
            response,
            HttpStatusCode.UnprocessableEntity,
            ErrorCodes.InvalidTaskStatusTransition);

        await AssertTaskStatusAsync(task.Id, currentStatus);
    }

    [Fact]
    public async Task Patch_UpdatesFieldsAndClearsDescription()
    {
        var project = await SeedProjectAsync();
        var task = await SeedTaskAsync(project.Id, description: "Descrição original");

        using var response = await _client.PatchAsync(
            $"/tarefas/{task.Id}",
            JsonRequest("""
                {
                  "title": "Título atualizado",
                  "description": null,
                  "priority": "high"
                }
                """));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<TaskResponse>(ResponseJsonOptions);
        Assert.NotNull(updated);
        Assert.Equal("Título atualizado", updated.Title);
        Assert.Null(updated.Description);
        Assert.Equal(TaskPriority.High, updated.Priority);
        Assert.Equal(TaskStatus.Pending, updated.Status);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{ \"title\": null }")]
    [InlineData("{ \"status\": null }")]
    [InlineData("{ \"priority\": null }")]
    [InlineData("{ \"status\": \"InProgress\" }")]
    [InlineData("{ \"completedAt\": \"2026-08-29T20:30:00Z\" }")]
    [InlineData("{ \"projectId\": \"550e8400-e29b-41d4-a716-446655440000\" }")]
    public async Task Patch_ReturnsValidationProblem_WhenBodyViolatesContract(string json)
    {
        using var response = await _client.PatchAsync(
            $"/tarefas/{Guid.NewGuid()}",
            JsonRequest(json));

        await AssertValidationProblemAsync(response);
    }

    [Fact]
    public async Task Patch_ReturnsTaskNotFound_WhenTaskDoesNotExist()
    {
        var taskId = Guid.NewGuid();

        using var response = await _client.PatchAsync(
            $"/tarefas/{taskId}",
            JsonRequest("""{ "title": "Novo título" }"""));

        await AssertProblemAsync(
            response,
            HttpStatusCode.NotFound,
            ErrorCodes.TaskNotFound);
    }

    [Fact]
    public async Task Delete_RemovesPendingTaskAndReturnsEmptyNoContent()
    {
        var project = await SeedProjectAsync();
        var task = await SeedTaskAsync(project.Id);

        using var response = await _client.DeleteAsync($"/tarefas/{task.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());

        await factory.ExecuteDbContextAsync(async dbContext =>
        {
            Assert.False(await dbContext.Tasks.AnyAsync(item => item.Id == task.Id));
        });
    }

    [Theory]
    [InlineData(TaskStatus.InProgress)]
    [InlineData(TaskStatus.Done)]
    public async Task Delete_ReturnsBusinessProblem_WhenTaskIsNotPending(TaskStatus status)
    {
        var project = await SeedProjectAsync();
        var task = await SeedTaskAsync(project.Id, status);

        using var response = await _client.DeleteAsync($"/tarefas/{task.Id}");

        await AssertProblemAsync(
            response,
            HttpStatusCode.UnprocessableEntity,
            ErrorCodes.TaskCannotBeDeleted);

        await factory.ExecuteDbContextAsync(async dbContext =>
        {
            Assert.True(await dbContext.Tasks.AnyAsync(item => item.Id == task.Id));
        });
    }

    [Fact]
    public async Task Delete_ReturnsTaskNotFound_WhenTaskDoesNotExist()
    {
        using var response = await _client.DeleteAsync($"/tarefas/{Guid.NewGuid()}");

        await AssertProblemAsync(
            response,
            HttpStatusCode.NotFound,
            ErrorCodes.TaskNotFound);
    }

    private async Task<Project> SeedProjectAsync(
        ProjectStatus status = ProjectStatus.Active)
    {
        var project = Project.Create(
            $"Projeto-{Guid.NewGuid()}",
            null,
            TaskFlowApiFactory.UtcNow);
        project.ChangeStatus(status);

        await factory.ExecuteDbContextAsync(async dbContext =>
        {
            dbContext.Projects.Add(project);
            await dbContext.SaveChangesAsync();
        });

        return project;
    }

    private async Task<TaskItem> SeedTaskAsync(
        Guid projectId,
        TaskStatus status = TaskStatus.Pending,
        TaskPriority priority = TaskPriority.Medium,
        string? description = null)
    {
        var task = TaskItem.Create(
            projectId,
            $"Tarefa-{Guid.NewGuid()}",
            description,
            priority,
            TaskFlowApiFactory.UtcNow.AddHours(-1));

        if (status is TaskStatus.InProgress or TaskStatus.Done)
        {
            Assert.True(task.TransitionTo(TaskStatus.InProgress, TaskFlowApiFactory.UtcNow).IsSuccess);
        }

        if (status == TaskStatus.Done)
        {
            Assert.True(task.TransitionTo(TaskStatus.Done, TaskFlowApiFactory.UtcNow).IsSuccess);
        }

        await factory.ExecuteDbContextAsync(async dbContext =>
        {
            dbContext.Tasks.Add(task);
            await dbContext.SaveChangesAsync();
        });

        return task;
    }

    private async Task AssertTaskStatusAsync(Guid taskId, TaskStatus expectedStatus)
    {
        await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var status = await dbContext.Tasks
                .Where(task => task.Id == taskId)
                .Select(task => task.Status)
                .SingleAsync();
            Assert.Equal(expectedStatus, status);
        });
    }

    private static StringContent JsonRequest(string json) =>
        new(json, Encoding.UTF8, "application/json");

    private static async Task AssertValidationProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = await ReadJsonAsync(response);
        Assert.Equal(ErrorCodes.ValidationError, GetCode(problem.RootElement));
        Assert.Equal(JsonValueKind.Object, problem.RootElement.GetProperty("errors").ValueKind);
    }

    private static async Task AssertProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatus,
        string expectedCode)
    {
        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = await ReadJsonAsync(response);
        Assert.Equal(expectedCode, GetCode(problem.RootElement));
        Assert.Equal((int)expectedStatus, problem.RootElement.GetProperty("status").GetInt32());
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response)
    {
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonDocument.ParseAsync(stream);
    }

    private static string? GetCode(JsonElement problemDetails) =>
        problemDetails.GetProperty("code").GetString();

    private static JsonSerializerOptions CreateResponseJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new StrictSnakeCaseLowerEnumConverterFactory());
        return options;
    }
}
