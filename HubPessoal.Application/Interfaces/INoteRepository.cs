using HubPessoal.Application.Models;
using HubPessoal.Domain.Entities;

namespace HubPessoal.Application.Interfaces;

public interface INoteRepository
{
    Task<Note?> GetByIdAsync(Guid id);
    Task<List<Note>> GetAllAsync();
    Task<List<NoteSummary>> GetSummariesAsync();
    Task <bool> HasNotesInFolderAsync(Guid folderId);
    Task <bool> ExistsAsync(Guid? folderId, string title, Guid? excludeId = null);
    Task AddAsync(Note note);
    void Remove(Note note);
    Task SaveChangesAsync();
}