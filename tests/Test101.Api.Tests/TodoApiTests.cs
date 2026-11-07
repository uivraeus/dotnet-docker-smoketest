using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Test101.Api.Todos;

namespace Test101.Api.Tests;

/// <summary>End-to-end tests that host the real app in-memory via <see cref="WebApplicationFactory{TEntryPoint}"/>.</summary>
public sealed class TodoApiTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client = factory.CreateClient();
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        var body = await _client.GetStringAsync(new Uri("/healthz", UriKind.Relative), Ct);

        Assert.Equal("Healthy", body);
    }

    [Fact]
    public async Task Root_serves_the_static_landing_page()
    {
        var response = await _client.GetAsync(new Uri("/", UriKind.Relative), Ct);

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Crud_round_trip()
    {
        // Create
        var create = await _client.PostAsJsonAsync("/api/todos", new CreateTodoRequest("Write tests"), Ct);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<Todo>(Ct);
        Assert.NotNull(created);
        Assert.Equal($"/api/todos/{created.Id}", create.Headers.Location?.OriginalString);

        // Read
        var fetched = await _client.GetFromJsonAsync<Todo>($"/api/todos/{created.Id}", Ct);
        Assert.Equal(created, fetched);

        // Update
        var update = await _client.PutAsJsonAsync($"/api/todos/{created.Id}", new UpdateTodoRequest("Write more tests", true), Ct);
        update.EnsureSuccessStatusCode();
        var updated = await update.Content.ReadFromJsonAsync<Todo>(Ct);
        Assert.True(updated!.IsComplete);

        // Delete
        var delete = await _client.DeleteAsync(new Uri($"/api/todos/{created.Id}", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var gone = await _client.GetAsync(new Uri($"/api/todos/{created.Id}", UriKind.Relative), Ct);
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }

    [Fact]
    public async Task Create_with_empty_title_returns_validation_problem()
    {
        var response = await _client.PostAsJsonAsync("/api/todos", new { title = "" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(Ct);
        Assert.Contains("Title", problem!.Errors.Keys);
    }

    [Fact]
    public async Task Unknown_todo_returns_404() =>
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await _client.GetAsync(new Uri($"/api/todos/{Guid.NewGuid()}", UriKind.Relative), Ct)).StatusCode);
}
