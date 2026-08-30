using TaskFlow.Api.Domain.Enums;

namespace TaskFlow.Api.Domain.Entities;

public sealed class Project
{
    private const int MaxNameLength = 100;

    private Project()
    {
    }

    private Project(
        Guid id,
        string name,
        string? description,
        ProjectStatus status,
        DateTimeOffset createdAt)
    {
        Id = id;
        Name = name;
        Description = description;
        Status = status;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string? Description { get; private set; }

    public ProjectStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Project Create(
        string name,
        string? description,
        DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (name.EnumerateRunes().Count() > MaxNameLength)
        {
            throw new ArgumentException(
                $"Project name cannot exceed {MaxNameLength} characters.",
                nameof(name));
        }

        return new Project(
            Guid.NewGuid(),
            name,
            description,
            ProjectStatus.Active,
            createdAt.ToUniversalTime());
    }

    public void UpdateName(string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        if (name.EnumerateRunes().Count() > MaxNameLength)
        {
            throw new ArgumentException(
                $"Project name cannot exceed {MaxNameLength} characters.",
                nameof(name));
        }

        Name = name;
    }

    public void UpdateDescription(string? description)
    {
        Description = description;
    }

    public void ChangeStatus(ProjectStatus status)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown project status.");
        }

        Status = status;
    }
}
