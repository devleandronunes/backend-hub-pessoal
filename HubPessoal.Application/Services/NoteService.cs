using HubPessoal.Application.Interfaces;
using HubPessoal.Application.Models;
using HubPessoal.Domain.Entities;

namespace HubPessoal.Application.Services;

public enum CreateNoteResult { Success, FolderNotFound, DuplicateTitle }
public enum UpdateNoteResult { Success, NotFound, DuplicateTitle }
public enum MoveNoteResult { Success, NotFound, FolderNotFound, DuplicateTitle }

public class NoteService
{
    private readonly INoteRepository _noteRepository;
    private readonly INoteFolderRepository _folderRepository;

    public NoteService(INoteRepository noteRepository, INoteFolderRepository folderRepository)
    {
        _noteRepository = noteRepository;
        _folderRepository = folderRepository;
    }

    public Task<List<Note>> GetAllAsync() => _noteRepository.GetAllAsync();

    public Task<Note?> GetByIdAsync(Guid id) => _noteRepository.GetByIdAsync(id);

    public async Task<List<NoteTreeNode>> GetTreeAsync()
    {
        var folders = await _folderRepository.GetAllAsync();
        var notes = await _noteRepository.GetSummariesAsync();
        return NoteTreeBuilder.Build(folders, notes);
    }

    public async Task<(CreateNoteResult Result, Note? Note)> CreateAsync(string title, string content, Guid? folderId, List<string> tags)
    {
        if (folderId is not null && await _folderRepository.GetByIdAsync(folderId.Value) is null)
        {
            return (CreateNoteResult.FolderNotFound, null);
        }

        if (await _noteRepository.ExistsAsync(folderId, title))
        {
            return (CreateNoteResult.DuplicateTitle, null);
        }

        var note = new Note(title, content, folderId, tags);
        await _noteRepository.AddAsync(note);
        await _noteRepository.SaveChangesAsync();
        return (CreateNoteResult.Success, note);
    }

    public async Task<(UpdateNoteResult Result, Note? Note)> UpdateAsync(Guid id, string title, string content, List<string> tags)
    {
        var note = await _noteRepository.GetByIdAsync(id);
        if (note is null)
        {
            return (UpdateNoteResult.DuplicateTitle, null);
        }

        if (await _noteRepository.ExistsAsync(note.FolderId, title, excludeId: id))
        {
            return (UpdateNoteResult.DuplicateTitle, null);
        }

        note.Update(title, content, tags);
        await _noteRepository.SaveChangesAsync();
        return (UpdateNoteResult.Success, note);
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        var note = await _noteRepository.GetByIdAsync(id);
        if (note is null)
        {
            return false;
        }

        _noteRepository.Remove(note);
        await _noteRepository.SaveChangesAsync();
        return true;
    }

    public async Task<Note?> TogglePinAsync(Guid id)
    {
        var note = await _noteRepository.GetByIdAsync(id);
        if (note is null)
        {
            return null;
        }

        note.TogglePin();
        await _noteRepository.SaveChangesAsync();
        return note;
    }

    public async Task<Note?> DuplicateAsync(Guid id)
    {
        var note = await _noteRepository.GetByIdAsync(id);
        if (note is null)
        {
            return null;
        }

        var copy = note.Duplicate();
        var candidateTitle = copy.Title;
        var attempt = 2;
        while (await _noteRepository.ExistsAsync(copy.FolderId, candidateTitle))
        {
            candidateTitle = $"{note.Title} (copy {attempt})";
            attempt++;
        }

        if (candidateTitle != copy.Title)
        {
            copy.Update(candidateTitle, copy.Content, copy.Tags);
        }

        await _noteRepository.AddAsync(copy);
        await _noteRepository.SaveChangesAsync();
        return copy;
    }

    public async Task<MoveNoteResult> MoveAsync(Guid id, Guid? folderId)
    {
        var note = await _noteRepository.GetByIdAsync(id);
        if (note is null)
        {
            return MoveNoteResult.NotFound;
        }

        if (folderId is not null && await _folderRepository.GetByIdAsync(folderId.Value) is null)
        {
            return MoveNoteResult.FolderNotFound;
        }

        if (await _noteRepository.ExistsAsync(folderId, note.Title, excludeId: id))
        {
            return MoveNoteResult.DuplicateTitle;
        }

        note.MoveTo(folderId);
        await _noteRepository.SaveChangesAsync();
        return MoveNoteResult.Success;
    }
}