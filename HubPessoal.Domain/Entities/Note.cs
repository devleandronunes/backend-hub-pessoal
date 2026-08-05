namespace HubPessoal.Domain.Entities;

public class Note
{
    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public List<string> Tags { get; private set; } = new();
    public bool IsPinned { get; private set; }
    public Guid? FolderId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Note()
    {
    }

    public Note(string title, string content, Guid? folderId, List<string>? tags = null)
    {
        Id = Guid.NewGuid();
        Title = title;
        Content = content;
        FolderId = folderId;
        Tags = tags ?? new List<string>();
        IsPinned = false;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public void Update(string title, string content, List<string> tags)
    {
        Title = title;
        Content = content;
        Tags = tags;
        UpdatedAt = DateTime.UtcNow;
    }

    public void TogglePin()
    {
        IsPinned = !IsPinned;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MoveTo(Guid? folderId)
    {
        FolderId = folderId;
        UpdatedAt = DateTime.UtcNow;
    }

    public Note Duplicate()
    {
        return new Note($"{Title} (copy)", Content, FolderId, new List<string>(Tags));
    }
}