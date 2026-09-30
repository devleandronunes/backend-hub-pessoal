namespace HubPessoal.Api.Contracts.Notes;

public record UpdateNoteRequest(string Title, string? Content, string? ContentBase64, List<string>? Tags)
{
    public string? ResolveContent() => NoteContentEnconding.Resolve(Content, ContentBase64);
}