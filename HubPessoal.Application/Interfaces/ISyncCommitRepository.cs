using HubPessoal.Domain.Entities;

namespace HubPessoal.Application.Interfaces;

public interface ISyncCommitRepository
{
    Task<List<SyncCommit>> GetRecentAsync(int take);
    Task<SyncCommit?> GetByHashAsync(string commitHash);
    Task<bool> ExistsAsync(string commitHash);
    Task AddAsync(SyncCommit commit);
    Task SaveChangesAsync();
}
