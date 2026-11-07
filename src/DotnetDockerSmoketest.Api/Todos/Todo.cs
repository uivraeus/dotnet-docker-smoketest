using System.ComponentModel.DataAnnotations;

namespace DotnetDockerSmoketest.Api.Todos;

/// <summary>A single to-do item.</summary>
public sealed record Todo(Guid Id, string Title, bool IsComplete, DateTimeOffset CreatedAt);

/// <summary>Payload for creating a to-do item.</summary>
public sealed record CreateTodoRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Title);

/// <summary>Payload for updating a to-do item.</summary>
public sealed record UpdateTodoRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Title,
    bool IsComplete);
