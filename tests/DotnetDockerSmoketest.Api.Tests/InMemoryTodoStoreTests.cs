using Microsoft.Extensions.Time.Testing;
using DotnetDockerSmoketest.Api.Todos;

namespace DotnetDockerSmoketest.Api.Tests;

public sealed class InMemoryTodoStoreTests
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly InMemoryTodoStore _store;

    public InMemoryTodoStoreTests() => _store = new InMemoryTodoStore(_time);

    [Fact]
    public void Add_trims_title_and_stamps_creation_time()
    {
        var todo = _store.Add("  Buy milk  ");

        Assert.Equal("Buy milk", todo.Title);
        Assert.False(todo.IsComplete);
        Assert.Equal(_time.GetUtcNow(), todo.CreatedAt);
        Assert.Same(todo, _store.Find(todo.Id));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Add_rejects_blank_titles(string title) =>
        Assert.Throws<ArgumentException>(() => _store.Add(title));

    [Fact]
    public void GetAll_returns_items_in_creation_order()
    {
        var first = _store.Add("first");
        _time.Advance(TimeSpan.FromMinutes(1));
        var second = _store.Add("second");

        Assert.Equal([first, second], _store.GetAll());
    }

    [Fact]
    public void Update_replaces_title_and_completion()
    {
        var todo = _store.Add("draft");

        var updated = _store.Update(todo.Id, "final", isComplete: true);

        Assert.NotNull(updated);
        Assert.Equal(todo with { Title = "final", IsComplete = true }, updated);
        Assert.Equal(updated, _store.Find(todo.Id));
    }

    [Fact]
    public void Update_returns_null_for_unknown_id() =>
        Assert.Null(_store.Update(Guid.NewGuid(), "x", isComplete: false));

    [Fact]
    public void Remove_deletes_once()
    {
        var todo = _store.Add("temp");

        Assert.True(_store.Remove(todo.Id));
        Assert.False(_store.Remove(todo.Id));
        Assert.Null(_store.Find(todo.Id));
    }
}
