using HubPessoal.Domain.Entities;

namespace HubPessoal.Application.Notes;

public record MaterializedFile(string Path, string Content);

public static class NoteFileMapper
{
    public const string NotesRoot = "notes";
    public const string KeepFileName = ".gitkeep";

    private static readonly char[] InvalidCharacters = { '/', '\\', ':', '*', '?', '"', '<', '>', '|' };

    public static List<MaterializedFile> Materialize(List<Note> notes, List<NoteFolder> folders)
    {
        var foldersById = folders.ToDictionary(f => f.Id);
        var files = new List<MaterializedFile>();

        foreach (var note in notes)
        {
            var directory = DirectoryFor(note.FolderId, foldersById);
            var path = Join(directory, $"{Sanitize(note.Title)}.md");
            var content = NoteFileFormat.Serialize(
                note.Id, note.Tags, note.IsPinned, note.CreatedAt, note.Content);

            files.Add(new MaterializedFile(path, content));
        }

        var directoriesWithNotes = files
            .Select(f => f.Path[..f.Path.LastIndexOf('/')])
            .ToHashSet();

        foreach (var directory in AllDirectories(folders, foldersById))
        {
            if (!directoriesWithNotes.Contains(directory))
            {
                files.Add(new MaterializedFile(Join(directory, KeepFileName), string.Empty));
            }
        }

        return files;
    }

    public static string DirectoryFor(Guid? folderId, IReadOnlyDictionary<Guid, NoteFolder> foldersById)
    {
        var segments = new List<string>();
        var current = folderId;

        while (current is not null && foldersById.TryGetValue(current.Value, out var folder))
        {
            segments.Insert(0, Sanitize(folder.Name));
            current = folder.ParentFolderId;
        }

        segments.Insert(0, NotesRoot);
        return string.Join('/', segments);
    }

    public static string Sanitize(string name)
    {
        var sanitized = new string(name
            .Select(c => InvalidCharacters.Contains(c) || char.IsControl(c) ? '-' : c)
            .ToArray())
            .Trim()
            .TrimEnd('.');

        return string.IsNullOrEmpty(sanitized) ? "untitled" : sanitized;
    }

    private static IEnumerable<string> AllDirectories(
        List<NoteFolder> folders, IReadOnlyDictionary<Guid, NoteFolder> foldersById)
    {
        yield return NotesRoot;

        foreach (var folder in folders)
        {
            yield return DirectoryFor(folder.Id, foldersById);
        }
    }

    private static string Join(string directory, string fileName) => $"{directory}/{fileName}";
}
