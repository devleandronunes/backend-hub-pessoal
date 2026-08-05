namespace HubPessoal.Api.Contracts.Folders;

public record CreateFolderRequest(string Name, Guid? ParentFolderId);
