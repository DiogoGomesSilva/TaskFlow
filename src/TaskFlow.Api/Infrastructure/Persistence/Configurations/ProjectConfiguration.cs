using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Api.Domain.Entities;

namespace TaskFlow.Api.Infrastructure.Persistence.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");

        builder.HasKey(project => project.Id);

        builder.Property(project => project.Id)
            .ValueGeneratedNever();

        builder.Property(project => project.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(project => project.Description)
            .IsRequired(false);

        builder.Property(project => project.Status)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(project => project.CreatedAt)
            .IsRequired();

        builder.HasIndex(project => project.Status)
            .HasDatabaseName("IX_Projects_Status");
    }
}
