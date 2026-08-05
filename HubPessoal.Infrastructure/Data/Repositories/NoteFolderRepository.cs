using HubPessoal.Application.Interfaces;
using HubPessoal.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HubPessoal.Infrastructure.Data.Repositories;

public class NoteFolderRepository : INoteFolderRepository
{
    private readonly AppDbContext _context;

    public NoteFolderRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<NoteFolder?> GetByIdAsync(Guid id) =>
        _context.NoteFolders.FirstOrDefaultAsync(f => f.Id == id);

    public Task<List<NoteFolder>> GetAllAsync() =>
        _context.NoteFolders.ToListAsync();

    public Task<bool> HasSubfolderAsync(Guid folderId) =>
        _context.NoteFolders.AnyAsync(f => f.ParentFolderId == folderId);

    public Task<bool> ExistsAsync(Guid? parentFolderId, string name, Guid? excludeId = null) =>
        _context.NoteFolders.AnyAsync(f =>
            f.ParentFolderId == parentFolderId &&
            f.Name == name &&
            (excludeId == null || f.Id != excludeId));

    public async Task AddAsync(NoteFolder folder) => await _context.NoteFolders.AddAsync(folder);

    public void Remove(NoteFolder folder) => _context.NoteFolders.Remove(folder);

    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}
