using TaskFlow.Api.Contracts.Projects;
using TaskFlow.Api.Domain.Entities;
using TaskFlow.Api.Infrastructure.Persistence;

namespace TaskFlow.Api.Application.Projects;

public sealed class CreateProjectUseCase(
    TaskFlowDbContext dbContext,
    TimeProvider timeProvider)
{
    public async Task<ProjectResponse> ExecuteAsync(
        CreateProjectRequest request,
        CancellationToken cancellationToken)
    {
        var project = Project.Create(
            request.Name,
            request.Description,
            timeProvider.GetUtcNow());

        dbContext.Projects.Add(project);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ProjectResponse.FromEntity(project);
    }
}
