using Heimdall.Example.Raw.Features.Notes;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

public sealed class NoteService
{
    private readonly ConcurrentDictionary<string, Note> _store = new();

    public async Task<List<Note>> GetAllAsync()
    {
        await Task.Delay(1);
        return _store.Values.OrderByDescending(n => n.CreatedAt).ToList();
    }

    public async IAsyncEnumerable<Note> GetStreamAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        var snapshot = _store.Values
            .OrderByDescending(n => n.CreatedAt)
            .ToList();

        foreach (var note in snapshot)
        {
            ct.ThrowIfCancellationRequested();

            yield return note;

            await Task.Yield();
        }
    }
    public async Task<List<Note>> GetPageAsync(int offset, int size)
    {
        await Task.Delay(1);
        return  _store.Values
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Skip(offset)
            .Take(size)
            .ToList();
    }
    public async Task<Note?> GetByIdAsync(string id)
    {
        await Task.Delay(1);
        _store.TryGetValue(id, out var note);
        return note;
    }

    public async Task<Note> CreateAsync(string title, string body)
    {
        await Task.Delay(1);
        var note = new Note
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            Body = body,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _store[note.Id] = note;
        return note;
    }

    public async Task<bool> UpdateAsync(string id, string title, string body)
    {
        await Task.Delay(1);

        if (!_store.TryGetValue(id, out var existing))
            return false;

        existing.Title = title;
        existing.Body = body;
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        return true;
    }

    public async Task<bool> DeleteAsync(string id)
    {
        await Task.Delay(1);
        return _store.TryRemove(id, out _);
    }
}
