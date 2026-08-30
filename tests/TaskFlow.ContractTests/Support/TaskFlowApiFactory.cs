using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TaskFlow.Api.Infrastructure.Persistence;

namespace TaskFlow.ContractTests;

public sealed class TaskFlowApiFactory : WebApplicationFactory<Program>
{
    public static readonly DateTimeOffset UtcNow =
        DateTimeOffset.Parse("2026-08-29T18:00:00Z");

    private readonly string _connectionString =
        $"Data Source=TaskFlowTests-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Default Timeout=10";

    private readonly SqliteConnection _anchorConnection;

    public TaskFlowApiFactory()
    {
        _anchorConnection = new SqliteConnection(_connectionString);
        _anchorConnection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureLogging(logging => logging.ClearProviders());

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<TaskFlowDbContext>>();
            services.AddDbContext<TaskFlowDbContext>(options =>
                options.UseSqlite(_connectionString));

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(UtcNow));
        });
    }

    public async Task ExecuteDbContextAsync(
        Func<TaskFlowDbContext, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TaskFlowDbContext>();
        await action(dbContext);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            _anchorConnection.Dispose();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
