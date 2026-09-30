namespace HubPessoal.Api.Contracts.Notes;

public record CreateNoteRequest(string Title, string? Content, string? ContentBase64, Guid? FolderId, List<string>? Tags)
{
    public string? ResolveContent() => NoteContentEnconding.Resolve(Content, ContentBase64);
}