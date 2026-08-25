namespace HubPessoal.Domain.Entities;

public class SyncCommitFile
{
    public Guid Id { get; private set; }
    public Guid SyncCommitId { get; private set; }
    public string Path { get; private set; } = string.Empty;
    public SyncChangeType ChangeType { get; private set; }
    public Guid? NoteId { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public int Insertions { get; private set; }
    public int Deletions { get; private set; }

    private SyncCommitFile()
    {
    }

    public SyncCommitFile(
        string path, SyncChangeType changeType, Guid? noteId, string content, int insertions, int deletions)
    {
        Id = Guid.NewGuid();
        Path = path;
        ChangeType = changeType;
        NoteId = noteId;
        Content = content;
        Insertions = insertions;
        Deletions = deletions;
    }
}
