using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using TaskFlow.Api.Application.Projects;
using TaskFlow.Api.Application.Tasks;
using TaskFlow.Api.Domain.Errors;
using TaskFlow.Api.Infrastructure.Http;
using TaskFlow.Api.Infrastructure.ModelBinding;
using TaskFlow.Api.Infrastructure.Persistence;
using TaskFlow.Api.Infrastructure.Serialization;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("TaskFlow")
    ?? throw new InvalidOperationException("Connection string 'TaskFlow' was not configured.");

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
});
builder.Services.AddSingleton<TaskFlowProblemDetailsFactory>();
builder.Services
    .AddControllers()
    .AddMvcOptions(options =>
    {
        options.ModelBinderProviders.Insert(0, new CanonicalGuidModelBinderProvider());

        var jsonFormatter = options.InputFormatters
            .OfType<SystemTextJsonInputFormatter>()
            .Single();
        jsonFormatter.SupportedMediaTypes.Clear();
        jsonFormatter.SupportedMediaTypes.Add(
            MediaTypeHeaderValue.Parse("application/json"));
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = false;
        options.JsonSerializerOptions.UnmappedMemberHandling =
            JsonUnmappedMemberHandling.Disallow;
        options.JsonSerializerOptions.Converters.Add(
            new OptionalJsonConverterFactory());
        options.JsonSerializerOptions.Converters.Add(
            new StrictSnakeCaseLowerEnumConverterFactory());
        options.JsonSerializerOptions.Converters.Add(
            new Rfc3339DateTimeOffsetConverter());
    })
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
            context.HttpContext.RequestServices
                .GetRequiredService<TaskFlowProblemDetailsFactory>()
                .CreateValidation(context);
    });
builder.Services.AddDbContext<TaskFlowDbContext>(options =>
    options.UseSqlite(connectionString));
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddScoped<CreateProjectUseCase>();
builder.Services.AddScoped<ListProjectsUseCase>();
builder.Services.AddScoped<GetProjectByIdUseCase>();
builder.Services.AddScoped<UpdateProjectUseCase>();
builder.Services.AddScoped<CreateTaskUseCase>();
builder.Services.AddScoped<ListProjectTasksUseCase>();
builder.Services.AddScoped<UpdateTaskUseCase>();
builder.Services.AddScoped<DeleteTaskUseCase>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseRouting();

// O contrato aceita apenas `application/json` nos corpos de requisição. Um 415
// pode surgir do roteamento (media type incompatível) ou da seleção de input
// formatter (o MVC ainda considera o sufixo `+json` compatível), ambos sem
// corpo. Este par de verificações garante o 415 e o normaliza para o formato
// ProblemDetails do contrato.
static Task WriteUnsupportedMediaTypeAsync(HttpContext context) =>
    Results.Problem(
            statusCode: StatusCodes.Status415UnsupportedMediaType,
            title: "Unsupported Media Type",
            type: "https://tools.ietf.org/html/rfc9110#section-15.6.5",
            instance: context.Request.Path,
            extensions: new Dictionary<string, object?>
            {
                ["code"] = ErrorCodes.UnsupportedMediaType
            })
        .ExecuteAsync(context);

app.Use(async (context, next) =>
{
    var requiresExactJson = context.GetEndpoint()?
        .Metadata
        .GetMetadata<ConsumesAttribute>() is not null;

    var hasExactJsonMediaType =
        MediaTypeHeaderValue.TryParse(context.Request.ContentType, out var contentType) &&
        string.Equals(
            contentType.MediaType.Value,
            "application/json",
            StringComparison.OrdinalIgnoreCase);

    if (requiresExactJson && !hasExactJsonMediaType)
    {
        await WriteUnsupportedMediaTypeAsync(context);
        return;
    }

    await next(context);

    if (context.Response.StatusCode == StatusCodes.Status415UnsupportedMediaType &&
        !context.Response.HasStarted)
    {
        await WriteUnsupportedMediaTypeAsync(context);
    }
});

// Serve o próprio openapi.yaml (fonte da verdade) e o renderiza na Swagger UI.
// Não há geração de contrato a partir do código.
var openApiPath = Path.Combine(AppContext.BaseDirectory, "openapi.yaml");

// Cache-busting: o token muda sempre que o arquivo é alterado, forçando a
// Swagger UI a rebuscar a especificação em vez de usar a cópia em cache.
var openApiCacheToken = File.Exists(openApiPath)
    ? File.GetLastWriteTimeUtc(openApiPath).Ticks.ToString()
    : "0";

app.MapGet("/openapi.yaml", (HttpContext http) =>
{
    http.Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
    return Results.File(openApiPath, "application/yaml");
});

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint($"/openapi.yaml?v={openApiCacheToken}", "TaskFlow API v1");
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "TaskFlow API";
});

await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<TaskFlowDbContext>();
    await dbContext.Database.MigrateAsync();
}

app.MapControllers();

app.Run();

public partial class Program;
