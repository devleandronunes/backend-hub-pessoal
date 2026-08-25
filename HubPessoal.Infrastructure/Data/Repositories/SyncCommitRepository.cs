using HubPessoal.Application.Interfaces;
using HubPessoal.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HubPessoal.Infrastructure.Data.Repositories;

public class SyncCommitRepository : ISyncCommitRepository
{
    private readonly AppDbContext _context;

    public SyncCommitRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<List<SyncCommit>> GetRecentAsync(int take) =>
        _context.SyncCommits
            .OrderByDescending(c => c.CommittedAt)
            .Take(take)
            .ToListAsync();

    public Task<SyncCommit?> GetByHashAsync(string commitHash) =>
        _context.SyncCommits
            .Include(c => c.Files)
            .FirstOrDefaultAsync(c => c.CommitHash == commitHash);

    public Task<bool> ExistsAsync(string commitHash) =>
        _context.SyncCommits.AnyAsync(c => c.CommitHash == commitHash);

    public async Task AddAsync(SyncCommit commit) => await _context.SyncCommits.AddAsync(commit);

    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}
