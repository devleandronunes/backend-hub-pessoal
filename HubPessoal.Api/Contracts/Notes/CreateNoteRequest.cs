namespace HubPessoal.Api.Contracts.Notes;

public record CreateNoteRequest(string Title, string Content, Guid? FolderId, List<string>? Tags);