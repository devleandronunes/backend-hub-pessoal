namespace HubPessoal.Domain.Entities;

public class NoteFolder
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public Guid? ParentFolderId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private NoteFolder()
    {
    }

    public NoteFolder(string name, Guid? parentFolderId)
    {
        Id = Guid.NewGuid();
        Name = name;
        ParentFolderId = parentFolderId;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public void Rename(string name)
    {
        Name = name;
        UpdatedAt = DateTime.UtcNow;
    }

    public void MoveTo(Guid? parentFolderId)
    {
        ParentFolderId = parentFolderId;
        UpdatedAt = DateTime.UtcNow;
    }
}