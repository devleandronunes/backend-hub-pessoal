using HubPessoal.Domain.Entities;

namespace HubPessoal.Api.Contracts.Notes;

public record NoteResponse(
    Guid ID,
    string Title,
    string Content,
    List<string> Tags,
    bool IsPinned,
    Guid? FolderId,
    DateTime CreatedAt,
    DateTime UpdatedAt)
{
    public static NoteResponse FromEntity(Note note) => new(
        note.Id,
        note.Title,
        note.Content,
        note.Tags,
        note.IsPinned,
        note.FolderId,
        note.CreatedAt,
        note.UpdatedAt);
}