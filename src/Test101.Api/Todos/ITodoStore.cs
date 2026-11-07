namespace Test101.Api.Todos;

public interface ITodoStore
{
    IReadOnlyList<Todo> GetAll();
    Todo? Find(Guid id);
    Todo Add(string title);
    Todo? Update(Guid id, string title, bool isComplete);
    bool Remove(Guid id);
}
