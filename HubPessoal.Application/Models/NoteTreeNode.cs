namespace HubPessoal.Application.Models;

public record NoteTreeNode(Guid Id, string Name, string Type, bool IsPinned, List<NoteTreeNode> Children);