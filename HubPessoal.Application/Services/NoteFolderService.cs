using HubPessoal.Application.Interfaces;
using HubPessoal.Domain.Entities;

namespace HubPessoal.Application.Services;

public enum CreateFolderResult { Success, ParentNotFound, DuplicateName }
public enum RenameFolderResult { Success, NotFound, DuplicateName }
public enum MoveFolderResult { Success, NotFound, ParentNotFound, InvalidParent, DuplicateName }
public enum DeleteFolderResult { Success, NotFound, NotEmpty }

public class NoteFolderService
{
    private readonly INoteFolderRepository _folderRepository;
    private readonly INoteRepository _noteRepository;

    public NoteFolderService(INoteFolderRepository folderRepository, INoteRepository noteRepository)
    {
        _folderRepository = folderRepository;
        _noteRepository = noteRepository;
    }

    public Task<List<NoteFolder>> GetAllAsync() => _folderRepository.GetAllAsync();

    public Task<NoteFolder?> GetByIdAsync(Guid id) => _folderRepository.GetByIdAsync(id);

    public async Task<(CreateFolderResult Result, NoteFolder? Folder)> CreateAsync(string name, Guid? parentFolderId)
    {
        if (parentFolderId is not null && await _folderRepository.GetByIdAsync(parentFolderId.Value) is null)
        {
            return (CreateFolderResult.ParentNotFound, null);
        }

        if (await _folderRepository.ExistsAsync(parentFolderId, name))
        {
            return (CreateFolderResult.DuplicateName, null);
        }

        var folder = new NoteFolder(name, parentFolderId);
        await _folderRepository.AddAsync(folder);
        await _folderRepository.SaveChangesAsync();
        return (CreateFolderResult.Success, folder);
    }

    public async Task<(RenameFolderResult Result, NoteFolder? Folder)> RenameAsync(Guid id, string name)
    {
        var folder = await _folderRepository.GetByIdAsync(id);
        if (folder is null)
        {
            return (RenameFolderResult.NotFound, null);
        }

        if (await _folderRepository.ExistsAsync(folder.ParentFolderId, name, excludeId: id))
        {
            return (RenameFolderResult.DuplicateName, null);
        }

        folder.Rename(name);
        await _folderRepository.SaveChangesAsync();
        return (RenameFolderResult.Success, folder);
    }

    public async Task<MoveFolderResult> MoveAsync(Guid id, Guid? newParentFolderId)
    {
        var folder = await _folderRepository.GetByIdAsync(id);
        if (folder is null)
        {
            return MoveFolderResult.NotFound;
        }

        if (newParentFolderId == id)
        {
            return MoveFolderResult.InvalidParent;
        }

        if (newParentFolderId is not null)
        {
            var cursor = await _folderRepository.GetByIdAsync(newParentFolderId.Value);
            if (cursor is null)
            {
                return MoveFolderResult.ParentNotFound;
            }

            while (cursor is not null)
            {
                if (cursor.Id == id)
                {
                    return MoveFolderResult.InvalidParent;
                }

                cursor = cursor.ParentFolderId is null ? null : await _folderRepository.GetByIdAsync(cursor.ParentFolderId.Value);
            }
        }

        if (await _folderRepository.ExistsAsync(newParentFolderId, folder.Name, excludeId: id))
        {
            return MoveFolderResult.DuplicateName;
        }

        folder.MoveTo(newParentFolderId);
        await _folderRepository.SaveChangesAsync();
        return MoveFolderResult.Success;
    }

    public async Task<DeleteFolderResult> DeleteAsync(Guid id)
    {
        var folder = await _folderRepository.GetByIdAsync(id);
        if (folder is null)
        {
            return DeleteFolderResult.NotFound;
        }

        var hasSubfolders = await _folderRepository.HasSubfolderAsync(id);
        var hasNotes = await _noteRepository.HasNotesInFolderAsync(id);
        if (hasSubfolders || hasNotes)
        {
            return DeleteFolderResult.NotEmpty;
        }

        _folderRepository.Remove(folder);
        await _folderRepository.SaveChangesAsync();
        return DeleteFolderResult.Success;
    }
}