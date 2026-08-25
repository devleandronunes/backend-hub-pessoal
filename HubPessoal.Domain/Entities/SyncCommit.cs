namespace HubPessoal.Domain.Entities;

public enum SyncDirection
{
    Push = 0,
    Pull = 1
}

public enum SyncChangeType
{
    Added = 0,
    Modified = 1,
    Deleted = 2,
    Renamed = 3
}

public class SyncCommit
{
    public Guid Id { get; private set; }
    public string CommitHash { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string AuthorName { get; private set; } = string.Empty;
    public DateTime CommittedAt { get; private set; }
    public SyncDirection Direction { get; private set; }
    public int FilesChanged { get; private set; }
    public int Insertions { get; private set; }
    public int Deletions { get; private set; }
    public DateTime RecordedAt { get; private set; }
    public List<SyncCommitFile> Files { get; private set; } = new();

    private SyncCommit()
    {
    }

    public SyncCommit(
        string commitHash,
        string message,
        string authorName,
        DateTime committedAt,
        SyncDirection direction,
        List<SyncCommitFile> files)
    {
        Id = Guid.NewGuid();
        CommitHash = commitHash;
        Message = message;
        AuthorName = authorName;
        CommittedAt = committedAt;
        Direction = direction;
        Files = files;
        FilesChanged = files.Count;
        Insertions = files.Sum(f => f.Insertions);
        Deletions = files.Sum(f => f.Deletions);
        RecordedAt = DateTime.UtcNow;
    }
}
