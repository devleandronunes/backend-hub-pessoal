using HubPessoal.Application.Models;
using HubPessoal.Domain.Entities;

namespace HubPessoal.Application.Services;

public static class NoteTreeBuilder
{
    public static List<NoteTreeNode> Build(List<NoteFolder> folders, List<NoteSummary> notes, Guid? parentFolderId = null)
    {
        var folderNodes = folders
            .Where(f => f.ParentFolderId == parentFolderId)
            .OrderBy(f => f.Name)
            .Select(f => new NoteTreeNode(f.Id, f.Name, "folder", false, Build(folders, notes, f.Id)));

        var noteNodes = notes
            .Where(n => n.FolderId == parentFolderId)
            .OrderByDescending(n => n.IsPinned)
            .ThenBy(n => n.Title)
            .Select(n => new NoteTreeNode(n.Id, n.Title, "note", n.IsPinned, new List<NoteTreeNode>()));

        return folderNodes.Concat(noteNodes).ToList();
    }
}