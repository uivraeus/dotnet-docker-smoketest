using System.Collections.Concurrent;

namespace Test101.Api.Todos;

/// <summary>Thread-safe, process-local store. Good enough for a demo; swap for a database in real life.</summary>
public sealed class InMemoryTodoStore(TimeProvider timeProvider) : ITodoStore
{
    private readonly ConcurrentDictionary<Guid, Todo> _todos = new();

    public IReadOnlyList<Todo> GetAll() =>
        [.. _todos.Values.OrderBy(t => t.CreatedAt)];

    public Todo? Find(Guid id) => _todos.GetValueOrDefault(id);

    public Todo Add(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        var todo = new Todo(Guid.CreateVersion7(), title.Trim(), IsComplete: false, timeProvider.GetUtcNow());
        _todos[todo.Id] = todo;
        return todo;
    }

    public Todo? Update(Guid id, string title, bool isComplete)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        while (_todos.TryGetValue(id, out var existing))
        {
            var updated = existing with { Title = title.Trim(), IsComplete = isComplete };
            if (_todos.TryUpdate(id, updated, existing))
            {
                return updated;
            }
        }

        return null;
    }

    public bool Remove(Guid id) => _todos.TryRemove(id, out _);
}
