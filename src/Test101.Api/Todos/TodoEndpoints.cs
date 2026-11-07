using Microsoft.AspNetCore.Http.HttpResults;

namespace Test101.Api.Todos;

public static class TodoEndpoints
{
    public static IEndpointRouteBuilder MapTodoEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/todos").WithTags("Todos");

        group.MapGet("/", GetAll).WithName("GetTodos");
        group.MapGet("/{id:guid}", GetById).WithName("GetTodo");
        group.MapPost("/", Create).WithName("CreateTodo");
        group.MapPut("/{id:guid}", Update).WithName("UpdateTodo");
        group.MapDelete("/{id:guid}", Delete).WithName("DeleteTodo");

        return app;
    }

    internal static Ok<IReadOnlyList<Todo>> GetAll(ITodoStore store) =>
        TypedResults.Ok(store.GetAll());

    internal static Results<Ok<Todo>, NotFound> GetById(Guid id, ITodoStore store) =>
        store.Find(id) is { } todo ? TypedResults.Ok(todo) : TypedResults.NotFound();

    internal static Created<Todo> Create(CreateTodoRequest request, ITodoStore store)
    {
        var todo = store.Add(request.Title);
        return TypedResults.Created($"/api/todos/{todo.Id}", todo);
    }

    internal static Results<Ok<Todo>, NotFound> Update(Guid id, UpdateTodoRequest request, ITodoStore store) =>
        store.Update(id, request.Title, request.IsComplete) is { } todo
            ? TypedResults.Ok(todo)
            : TypedResults.NotFound();

    internal static Results<NoContent, NotFound> Delete(Guid id, ITodoStore store) =>
        store.Remove(id) ? TypedResults.NoContent() : TypedResults.NotFound();
}
