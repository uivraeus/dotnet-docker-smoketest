using Scalar.AspNetCore;
using Test101.Api.Todos;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddValidation();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ITodoStore, InMemoryTodoStore>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                 // /openapi/v1.json
    app.MapScalarApiReference();      // /scalar
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api/info", (IHostEnvironment env) => TypedResults.Ok(new AppInfo(
        env.ApplicationName,
        env.EnvironmentName,
        System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
        Environment.MachineName)))
    .WithName("GetInfo")
    .WithTags("Info");

app.MapTodoEndpoints();

app.MapHealthChecks("/healthz");

await app.RunAsync();

internal sealed record AppInfo(string Application, string Environment, string Framework, string Host);

/// <summary>Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can reach it from the test project.</summary>
public partial class Program;
