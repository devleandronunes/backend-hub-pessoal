using HubPessoal.Application.Models;
using HubPessoal.Domain.Entities;

namespace HubPessoal.Api.Contracts.Sync;

public record SyncStatusResponse(string State, int LocalChanges, int IncomingCommits);

public record SyncFileChangeResponse(string Path, string ChangeType, int Insertions, int Deletions);

public record SyncPlanResponse(
    string State,
    bool WillPull,
    bool WillPush,
    string CommitMessage,
    List<string> Commands,
    List<SyncFileChangeResponse> Changes,
    int IncomingCommits,
    int FilesChanged,
    int Insertions,
    int Deletions,
    string Fingerprint)
{
    public static SyncPlanResponse FromPlan(SyncPlan plan) => new(
        plan.State.ToString(),
        plan.WillPull,
        plan.WillPush,
        plan.CommitMessage,
        plan.Commands,
        plan.OutgoingChanges
            .Select(c => new SyncFileChangeResponse(
                c.Path, c.ChangeType.ToString(), c.Insertions, c.Deletions))
            .ToList(),
        plan.IncomingCommits,
        plan.FilesChanged,
        plan.Insertions,
        plan.Deletions,
        plan.Fingerprint);
}

public record ApplySyncRequest(string Fingerprint);

public record SyncCommitSummaryResponse(
    string CommitHash, string Message, DateTime CommittedAt, int FilesChanged, int Insertions, int Deletions);

public record SyncCommitFileDetailResponse(
    string Path, string ChangeType, Guid? NoteId, string Content, int Insertions, int Deletions)
{
    public static SyncCommitFileDetailResponse FromEntity(SyncCommitFile file) => new(
        file.Path, file.ChangeType.ToString(), file.NoteId, file.Content, file.Insertions, file.Deletions);
}

public record SyncCommitDetailResponse(
    string CommitHash,
    string Message,
    string AuthorName,
    DateTime CommittedAt,
    int FilesChanged,
    int Insertions,
    int Deletions,
    List<SyncCommitFileDetailResponse> Files)
{
    public static SyncCommitDetailResponse FromEntity(SyncCommit commit) => new(
        commit.CommitHash,
        commit.Message,
        commit.AuthorName,
        commit.CommittedAt,
        commit.FilesChanged,
        commit.Insertions,
        commit.Deletions,
        commit.Files.Select(SyncCommitFileDetailResponse.FromEntity).ToList());
}

public record ApplySyncErrorResponse(string Reason, string Detail);
