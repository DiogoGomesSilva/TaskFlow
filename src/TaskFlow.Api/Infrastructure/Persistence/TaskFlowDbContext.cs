using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Domain.Entities;

namespace TaskFlow.Api.Infrastructure.Persistence;

public sealed class TaskFlowDbContext(DbContextOptions<TaskFlowDbContext> options)
    : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();

    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(TaskFlowDbContext).Assembly);
    }
}
