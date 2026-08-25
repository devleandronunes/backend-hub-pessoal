namespace HubPessoal.Application.Notes;

public record ImportedNote(Guid? Id, string Title, string FolderPath, NoteFileContent Content);

public static class NoteImporter
{
    public static List<ImportedNote> Read(string repositoryRoot)
    {
        var notesRoot = Path.Combine(repositoryRoot, NoteFileMapper.NotesRoot);

        if (!Directory.Exists(notesRoot))
        {
            return new List<ImportedNote>();
        }

        var imported = new List<ImportedNote>();

        foreach (var file in Directory.EnumerateFiles(notesRoot, "*.md", SearchOption.AllDirectories))
        {
            var parsed = NoteFileFormat.Parse(File.ReadAllText(file));
            var relative = Path.GetRelativePath(notesRoot, file).Replace('\\', '/');
            var lastSlash = relative.LastIndexOf('/');

            imported.Add(new ImportedNote(
                parsed.Id,
                Path.GetFileNameWithoutExtension(file),
                lastSlash < 0 ? string.Empty : relative[..lastSlash],
                parsed));
        }

        return imported;
    }

    public static List<string> ReadFolders(string repositoryRoot)
    {
        var notesRoot = Path.Combine(repositoryRoot, NoteFileMapper.NotesRoot);

        if (!Directory.Exists(notesRoot))
        {
            return new List<string>();
        }

        return Directory.EnumerateDirectories(notesRoot, "*", SearchOption.AllDirectories)
            .Select(d => Path.GetRelativePath(notesRoot, d).Replace('\\', '/'))
            .OrderBy(d => d.Count(c => c == '/'))
            .ToList();
    }
}
