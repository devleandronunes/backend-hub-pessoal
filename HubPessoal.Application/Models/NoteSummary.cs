namespace HubPessoal.Application.Models;

public record NoteSummary(Guid Id, string Title, bool IsPinned, Guid? FolderId);