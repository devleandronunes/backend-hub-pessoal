using HubPessoal.Domain.Entities;

namespace HubPessoal.Application.Models;

public enum SyncState
{
    Clean = 0,
    LocalChanges = 1,
    RemoteChanges = 2,
    Diverged = 3
}

public enum SyncApplyResult
{
    Success = 0,
    NothingToDo = 1,
    PlanExpired = 2,
    Conflict = 3,
    GitFailure = 4
}

public record SyncFileChange(string Path, SyncChangeType ChangeType, int Insertions, int Deletions);

public record SyncPlan(
    SyncState State,
    bool WillPull,
    bool WillPush,
    string CommitMessage,
    List<string> Commands,
    List<SyncFileChange> OutgoingChanges,
    int IncomingCommits,
    int FilesChanged,
    int Insertions,
    int Deletions,
    string Fingerprint);

public record SyncOutcome(SyncApplyResult Result, string? CommitHash, string? Detail);
