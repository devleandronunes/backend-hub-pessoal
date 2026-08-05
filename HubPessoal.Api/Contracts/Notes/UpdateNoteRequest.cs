namespace HubPessoal.Api.Contracts.Notes;

public record UpdateNoteRequest(string Title, string Content, List<string>? Tags);