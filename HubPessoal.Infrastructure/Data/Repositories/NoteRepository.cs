using HubPessoal.Application.Interfaces;
using HubPessoal.Application.Models;
using HubPessoal.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HubPessoal.Infrastructure.Data.Repositories;

public class NoteRepository : INoteRepository
{
    private readonly AppDbContext _context;

    public NoteRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Note?> GetByIdAsync(Guid id) => _context.Notes.FirstOrDefaultAsync(f => f.Id == id);

    public Task<List<Note>> GetAllAsync() =>
        _context.Notes
            .OrderByDescending(n => n.IsPinned)
            .ThenByDescending(n => n.UpdatedAt)
            .ToListAsync();

    public Task<List<NoteSummary>> GetSummariesAsync() =>
        _context.Notes
            .Select(n => new NoteSummary(n.Id, n.Title, n.IsPinned, n.FolderId))
            .ToListAsync();

    public Task<bool> HasNotesInFolderAsync(Guid folderId) =>
        _context.Notes.AnyAsync(n => n.FolderId == folderId);

    public Task<bool> ExistsAsync(Guid? folderId, string title, Guid? excludeId = null) =>
        _context.Notes.AnyAsync(n =>
            n.FolderId == folderId &&
            n.Title == title &&
            (excludeId == null || n.Id != excludeId));

    public async Task AddAsync(Note note) => await _context.Notes.AddAsync(note);

    public void Remove(Note note) => _context.Notes.Remove(note);

    public Task SaveChangesAsync() => _context.SaveChangesAsync();
}
