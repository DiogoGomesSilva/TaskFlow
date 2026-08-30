using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Api.Domain.Entities;

namespace TaskFlow.Api.Infrastructure.Persistence.Configurations;

internal sealed class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        builder.ToTable("Tasks");

        builder.HasKey(task => task.Id);

        builder.Property(task => task.Id)
            .ValueGeneratedNever();

        builder.Property(task => task.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(task => task.Description)
            .IsRequired(false);

        builder.Property(task => task.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(task => task.Priority)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(task => task.CreatedAt)
            .IsRequired();

        builder.Property(task => task.CompletedAt)
            .IsRequired(false);

        builder.Property(task => task.ProjectId)
            .IsRequired();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(task => task.ProjectId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired();

        builder.HasIndex(task => new { task.ProjectId, task.Status, task.Priority })
            .HasDatabaseName("IX_Tasks_ProjectId_Status_Priority");

        builder.HasIndex(task => new { task.ProjectId, task.Priority })
            .HasDatabaseName("IX_Tasks_ProjectId_Priority");
    }
}
