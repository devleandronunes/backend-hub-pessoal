using HubPessoal.Domain.Entities;

namespace HubPessoal.Api.Contracts.Folders;

public record FolderResponse(Guid Id, string Name, Guid? ParentFolderId, DateTime CreatedAt, DateTime UpdatedAt)
{
    public static FolderResponse FromEntity(NoteFolder folder) => new(
        folder.Id,
        folder.Name,
        folder.ParentFolderId,
        folder.CreatedAt,
        folder.UpdatedAt);
}