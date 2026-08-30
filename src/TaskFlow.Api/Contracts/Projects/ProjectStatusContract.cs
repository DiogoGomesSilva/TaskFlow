using TaskFlow.Api.Domain.Enums;

namespace TaskFlow.Api.Contracts.Projects;

public static class ProjectStatusContract
{
    public static bool TryParse(string? value, out ProjectStatus status)
    {
        switch (value)
        {
            case "active":
                status = ProjectStatus.Active;
                return true;
            case "archived":
                status = ProjectStatus.Archived;
                return true;
            default:
                status = default;
                return false;
        }
    }
}
