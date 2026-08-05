using HubPessoal.Domain.Entities;

namespace HubPessoal.Application.Interfaces;

public interface INoteFolderRepository
{
    Task<NoteFolder?> GetByIdAsync(Guid id);
    Task<List<NoteFolder>> GetAllAsync();
    Task<bool> HasSubfolderAsync(Guid folderId);
    Task<bool> ExistsAsync(Guid? parentFolderId, string name, Guid? excludeId = null);
    Task AddAsync(NoteFolder folder);
    void Remove(NoteFolder folder);
    Task SaveChangesAsync();
}