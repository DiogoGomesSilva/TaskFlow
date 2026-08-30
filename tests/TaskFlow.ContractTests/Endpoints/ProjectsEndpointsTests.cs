using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Contracts.Projects;
using TaskFlow.Api.Domain.Entities;
using TaskFlow.Api.Domain.Enums;
using TaskFlow.Api.Domain.Errors;
using TaskFlow.Api.Infrastructure.Serialization;
using TaskStatus = TaskFlow.Api.Domain.Enums.TaskStatus;

namespace TaskFlow.ContractTests;

public sealed class ProjectsEndpointsTests(TaskFlowApiFactory factory)
    : IClassFixture<TaskFlowApiFactory>
{
    private static readonly JsonSerializerOptions ResponseJsonOptions = CreateResponseJsonOptions();

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Post_ReturnsCreatedProjectAndLocation()
    {
        using var response = await _client.PostAsJsonAsync(
            "/projetos",
            new
            {
                name = "Plataforma TaskFlow",
                description = "Projeto para gerenciamento colaborativo de tarefas."
            });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var project = await response.Content.ReadFromJsonAsync<ProjectResponse>(ResponseJsonOptions);
        Assert.NotNull(project);
        Assert.NotNull(response.Headers.Location);
        var locationPath = response.Headers.Location.IsAbsoluteUri
            ? response.Headers.Location.AbsolutePath
            : response.Headers.Location.OriginalString;
        Assert.Equal($"/projetos/{project.Id}", locationPath);
        Assert.Equal("Plataforma TaskFlow", project.Name);
        Assert.Equal(ProjectStatus.Active, project.Status);
        Assert.Equal(TaskFlowApiFactory.UtcNow, project.CreatedAt);

        await factory.ExecuteDbContextAsync(async dbContext =>
        {
            Assert.True(await dbContext.Projects.AnyAsync(item => item.Id == project.Id));
        });
    }

    [Fact]
    public async Task Post_ReturnsValidationProblem_WhenContractIsInvalid()
    {
        using var request = JsonRequest("""
            {
              "name": "",
              "status": "active"
            }
            """);

        using var response = await _client.PostAsync("/projetos", request);

        await AssertValidationProblemAsync(response);
    }

    [Fact]
    public async Task List_FiltersProjectsByStatus()
    {
        var archivedProject = Project.Create(
            $"Archived-{Guid.NewGuid()}",
            null,
            TaskFlowApiFactory.UtcNow);
        archivedProject.ChangeStatus(ProjectStatus.Archived);
        var activeProject = Project.Create(
            $"Active-{Guid.NewGuid()}",
            null,
            TaskFlowApiFactory.UtcNow);

        await factory.ExecuteDbContextAsync(async dbContext =>
        {
            dbContext.Projects.AddRange(archivedProject, activeProject);
            await dbContext.SaveChangesAsync();
        });

        using var response = await _client.GetAsync("/projetos?status=archived");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var projects = await response.Content.ReadFromJsonAsync<List<ProjectResponse>>(
            ResponseJsonOptions);
        Assert.NotNull(projects);
        Assert.Contains(projects, project => project.Id == archivedProject.Id);
        Assert.DoesNotContain(projects, project => project.Id == activeProject.Id);
        Assert.All(projects, project => Assert.Equal(ProjectStatus.Archived, project.Status));
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("ACTIVE")]
    [InlineData("0")]
    public async Task List_ReturnsValidationProblem_WhenStatusIsOutsideContract(string status)
    {
        using var response = await _client.GetAsync($"/projetos?status={status}");

        await AssertValidationProblemAsync(response);
    }

    [Fact]
    public async Task GetById_ReturnsProjectWithoutExposingEntityInternals()
    {
        var project = await SeedProjectAsync("Projeto consultado", "Descrição");

        using var response = await _client.GetAsync($"/projetos/{project.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        var root = document.RootElement;
        var propertyNames = root.EnumerateObject().Select(property => property.Name).ToArray();

        Assert.Equal(
            ["id", "name", "description", "status", "createdAt"],
            propertyNames);
        Assert.Equal(project.Id, root.GetProperty("id").GetGuid());
        Assert.Equal("active", root.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetById_ReturnsProjectNotFoundProblem()
    {
        var id = Guid.NewGuid();

        using var response = await _client.GetAsync($"/projetos/{id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = await ReadJsonAsync(response);
        Assert.Equal(ErrorCodes.ProjectNotFound, GetCode(problem.RootElement));
        Assert.Equal($"/projetos/{id}", problem.RootElement.GetProperty("instance").GetString());
    }

    [Fact]
    public async Task GetById_ReturnsValidationProblem_WhenIdIsMalformed()
    {
        using var response = await _client.GetAsync("/projetos/not-a-guid");

        await AssertValidationProblemAsync(response);
    }

    [Fact]
    public async Task Patch_DistinguishesOmittedDescriptionFromExplicitNull()
    {
        var project = await SeedProjectAsync("Nome original", "Descrição original");

        using var renameResponse = await _client.PatchAsync(
            $"/projetos/{project.Id}",
            JsonRequest("""{ "name": "Nome atualizado" }"""));

        Assert.Equal(HttpStatusCode.OK, renameResponse.StatusCode);
        var renamed = await renameResponse.Content.ReadFromJsonAsync<ProjectResponse>(
            ResponseJsonOptions);
        Assert.NotNull(renamed);
        Assert.Equal("Descrição original", renamed.Description);

        using var clearResponse = await _client.PatchAsync(
            $"/projetos/{project.Id}",
            JsonRequest("""{ "description": null }"""));

        Assert.Equal(HttpStatusCode.OK, clearResponse.StatusCode);
        var cleared = await clearResponse.Content.ReadFromJsonAsync<ProjectResponse>(
            ResponseJsonOptions);
        Assert.NotNull(cleared);
        Assert.Null(cleared.Description);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{ \"name\": null }")]
    [InlineData("{ \"status\": null }")]
    [InlineData("{ \"createdAt\": \"2026-08-29T18:00:00Z\" }")]
    [InlineData("{ \"status\": \"ACTIVE\" }")]
    public async Task Patch_ReturnsValidationProblem_WhenBodyViolatesContract(string json)
    {
        using var response = await _client.PatchAsync(
            $"/projetos/{Guid.NewGuid()}",
            JsonRequest(json));

        await AssertValidationProblemAsync(response);
    }

    [Fact]
    public async Task Patch_ReturnsProjectNotFound_WhenIdDoesNotExist()
    {
        var id = Guid.NewGuid();

        using var response = await _client.PatchAsync(
            $"/projetos/{id}",
            JsonRequest("""{ "name": "Novo nome" }"""));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var problem = await ReadJsonAsync(response);
        Assert.Equal(ErrorCodes.ProjectNotFound, GetCode(problem.RootElement));
    }

    [Fact]
    public async Task Patch_ReturnsBusinessProblem_WhenArchivingProjectWithInProgressTask()
    {
        var project = await SeedProjectAsync("Projeto com tarefa", null);
        var task = TaskItem.Create(
            project.Id,
            "Tarefa em andamento",
            null,
            TaskPriority.High,
            TaskFlowApiFactory.UtcNow);
        var transition = task.TransitionTo(TaskStatus.InProgress, TaskFlowApiFactory.UtcNow);
        Assert.True(transition.IsSuccess);

        await factory.ExecuteDbContextAsync(async dbContext =>
        {
            dbContext.Tasks.Add(task);
            await dbContext.SaveChangesAsync();
        });

        using var response = await _client.PatchAsync(
            $"/projetos/{project.Id}",
            JsonRequest("""{ "status": "archived" }"""));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = await ReadJsonAsync(response);
        Assert.Equal(
            ErrorCodes.ProjectHasInProgressTasks,
            GetCode(problem.RootElement));

        await factory.ExecuteDbContextAsync(async dbContext =>
        {
            var persistedStatus = await dbContext.Projects
                .Where(item => item.Id == project.Id)
                .Select(item => item.Status)
                .SingleAsync();
            Assert.Equal(ProjectStatus.Active, persistedStatus);
        });
    }

    [Fact]
    public async Task Patch_ArchivesProject_WhenThereAreNoInProgressTasks()
    {
        var project = await SeedProjectAsync("Projeto arquivável", null);

        using var response = await _client.PatchAsync(
            $"/projetos/{project.Id}",
            JsonRequest("""{ "status": "archived" }"""));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<ProjectResponse>(ResponseJsonOptions);
        Assert.NotNull(updated);
        Assert.Equal(ProjectStatus.Archived, updated.Status);
    }

    private async Task<Project> SeedProjectAsync(string name, string? description)
    {
        var project = Project.Create(name, description, TaskFlowApiFactory.UtcNow);

        await factory.ExecuteDbContextAsync(async dbContext =>
        {
            dbContext.Projects.Add(project);
            await dbContext.SaveChangesAsync();
        });

        return project;
    }

    private static StringContent JsonRequest(string json) =>
        new(json, Encoding.UTF8, "application/json");

    private static async Task AssertValidationProblemAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = await ReadJsonAsync(response);
        Assert.Equal(ErrorCodes.ValidationError, GetCode(problem.RootElement));
        Assert.Equal(400, problem.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(JsonValueKind.Object, problem.RootElement.GetProperty("errors").ValueKind);
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
