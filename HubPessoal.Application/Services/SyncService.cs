using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HubPessoal.Application.Interfaces;
using HubPessoal.Application.Models;
using HubPessoal.Application.Notes;
using HubPessoal.Application.Options;
using HubPessoal.Domain.Entities;
using Microsoft.Extensions.Options;

namespace HubPessoal.Application.Services;

public class SyncService
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    private readonly IGitClient _git;
    private readonly INoteRepository _noteRepository;
    private readonly INoteFolderRepository _folderRepository;
    private readonly ISyncCommitRepository _commitRepository;
    private readonly GitOptions _options;

    public SyncService(
        IGitClient git,
        INoteRepository noteRepository,
        INoteFolderRepository folderRepository,
        ISyncCommitRepository commitRepository,
        IOptions<GitOptions> options)
    {
        _git = git;
        _noteRepository = noteRepository;
        _folderRepository = folderRepository;
        _commitRepository = commitRepository;
        _options = options.Value;
    }

    // ── Workspace ────────────────────────────────────────────────

    private async Task<string> EnsureWorkspaceAsync()
    {
        var path = _options.ResolvedWorkingDirectory;

        if (Directory.Exists(Path.Combine(path, ".git")))
        {
            var fetch = await _git.RunAsync(path, "fetch", "origin", _options.Branch);

            if (fetch.Success)
            {
                return path;
            }

            Directory.Delete(path, recursive: true);
        }

        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }

        var parent = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(parent);

        var clone = await _git.RunAsync(
            parent, "clone", "--depth", "1", "--branch", _options.Branch,
            _options.AuthenticatedUrl, path);

        if (!clone.Success)
        {
            throw new InvalidOperationException($"git clone failed: {Mask(clone.StandardError)}");
        }

        await _git.RunAsync(path, "config", "user.name", _options.AuthorName);
        await _git.RunAsync(path, "config", "user.email", _options.AuthorEmail);
        await _git.RunAsync(path, "config", "core.quotePath", "false");

        return path;
    }

    private string Mask(string text) =>
        string.IsNullOrEmpty(_options.Token) ? text : text.Replace(_options.Token, "***");

    // ── Materialização ───────────────────────────────────────────

    private async Task WriteDatabaseToWorkspaceAsync(string workspace)
    {
        var notes = await _noteRepository.GetAllAsync();
        var folders = await _folderRepository.GetAllAsync();
        var files = NoteFileMapper.Materialize(notes, folders);

        var notesRoot = Path.Combine(workspace, NoteFileMapper.NotesRoot);

        if (Directory.Exists(notesRoot))
        {
            Directory.Delete(notesRoot, recursive: true);
        }

        foreach (var file in files)
        {
            var absolute = Path.Combine(workspace, file.Path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            await File.WriteAllTextAsync(absolute, file.Content);
        }
    }

    // ── Plano ────────────────────────────────────────────────────

    public async Task<SyncPlan> PreviewAsync()
    {
        await Gate.WaitAsync();

        try
        {
            var workspace = await EnsureWorkspaceAsync();
            return await BuildPlanAsync(workspace);
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task<SyncPlan> BuildPlanAsync(string workspace)
    {
        await WriteDatabaseToWorkspaceAsync(workspace);
        await _git.RunAsync(workspace, "add", "-A");

        var numstat = await _git.RunAsync(workspace, "diff", "--cached", "--numstat");
        var nameStatus = await _git.RunAsync(workspace, "diff", "--cached", "--name-status");
        var behind = await _git.RunAsync(
            workspace, "rev-list", "--count", $"HEAD..origin/{_options.Branch}");

        var changes = ParseChanges(numstat.StandardOutput, nameStatus.StandardOutput);
        var incoming = int.TryParse(behind.StandardOutput.Trim(), out var count) ? count : 0;

        var willPush = changes.Count > 0;
        var willPull = incoming > 0;

        var state = (willPull, willPush) switch
        {
            (true, true) => SyncState.Diverged,
            (true, false) => SyncState.RemoteChanges,
            (false, true) => SyncState.LocalChanges,
            _ => SyncState.Clean
        };

        var message = BuildCommitMessage(changes);

        return new SyncPlan(
            State: state,
            WillPull: willPull,
            WillPush: willPush,
            CommitMessage: message,
            Commands: BuildCommands(willPull, willPush, message),
            OutgoingChanges: changes,
            IncomingCommits: incoming,
            FilesChanged: changes.Count,
            Insertions: changes.Sum(c => c.Insertions),
            Deletions: changes.Sum(c => c.Deletions),
            Fingerprint: Fingerprint(nameStatus.StandardOutput, incoming));
    }

    private List<string> BuildCommands(bool willPull, bool willPush, string message)
    {
        var commands = new List<string> { $"git fetch origin {_options.Branch}" };

        if (willPush)
        {
            commands.Add("git add -A");
            commands.Add($"git commit -m \"{message.Split('\n')[0]}\"");
        }

        if (willPull)
        {
            commands.Add($"git pull --no-rebase origin {_options.Branch}");
        }

        if (willPush || willPull)
        {
            commands.Add($"git push origin {_options.Branch}");
        }

        return commands;
    }

    private static string BuildCommitMessage(List<SyncFileChange> changes)
    {
        if (changes.Count == 0)
        {
            return string.Empty;
        }

        if (changes.Count == 1)
        {
            var change = changes[0];
            var title = Path.GetFileNameWithoutExtension(change.Path);

            return change.ChangeType switch
            {
                SyncChangeType.Added => $"feat(note): cria \"{title}\"",
                SyncChangeType.Deleted => $"chore(note): remove \"{title}\"",
                SyncChangeType.Renamed => $"refactor(note): move \"{title}\"",
                _ => $"docs(note): edita \"{title}\""
            };
        }

        var body = new StringBuilder();
        body.Append("chore(notes): sincroniza ").Append(changes.Count).Append(" alterações\n");

        foreach (var change in changes.OrderBy(c => c.Path))
        {
            var verb = change.ChangeType switch
            {
                SyncChangeType.Added => "cria",
                SyncChangeType.Deleted => "remove",
                SyncChangeType.Renamed => "move",
                _ => "edita"
            };

            body.Append("\n- ").Append(verb).Append(' ').Append(change.Path);
        }

        return body.ToString();
    }

    private static List<SyncFileChange> ParseChanges(string numstat, string nameStatus)
    {
        var types = new Dictionary<string, SyncChangeType>(StringComparer.Ordinal);

        foreach (var line in nameStatus.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t');

            if (parts.Length < 2)
            {
                continue;
            }

            var path = parts[^1];

            types[path] = parts[0][0] switch
            {
                'A' => SyncChangeType.Added,
                'D' => SyncChangeType.Deleted,
                'R' => SyncChangeType.Renamed,
                _ => SyncChangeType.Modified
            };
        }

        var changes = new List<SyncFileChange>();

        foreach (var line in numstat.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split('\t');

            if (parts.Length < 3)
            {
                continue;
            }

            var path = parts[^1];
            var insertions = int.TryParse(parts[0], out var i) ? i : 0;
            var deletions = int.TryParse(parts[1], out var d) ? d : 0;

            changes.Add(new SyncFileChange(
                path,
                types.TryGetValue(path, out var type) ? type : SyncChangeType.Modified,
                insertions,
                deletions));
        }

        return changes;
    }

    private static string Fingerprint(string nameStatus, int incoming)
    {
        var lines = nameStatus
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .OrderBy(l => l, StringComparer.Ordinal);

        var payload = $"{incoming}\n{string.Join('\n', lines)}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));

        return Convert.ToHexString(hash);
    }

    // ── Execução ─────────────────────────────────────────────────

    public async Task<SyncOutcome> ApplyAsync(string fingerprint)
    {
        await Gate.WaitAsync();

        try
        {
            var workspace = await EnsureWorkspaceAsync();
            var plan = await BuildPlanAsync(workspace);

            if (plan.Fingerprint != fingerprint)
            {
                return new SyncOutcome(SyncApplyResult.PlanExpired, null, "O repositório mudou desde o preview.");
            }

            if (plan.State == SyncState.Clean)
            {
                return new SyncOutcome(SyncApplyResult.NothingToDo, null, null);
            }

            if (plan.WillPush)
            {
                var commit = await _git.RunAsync(workspace, "commit", "-m", plan.CommitMessage);

                if (!commit.Success)
                {
                    return new SyncOutcome(SyncApplyResult.GitFailure, null, Mask(commit.StandardError));
                }
            }

            if (plan.WillPull)
            {
                var pull = await _git.RunAsync(
                    workspace, "pull", "--no-rebase", "origin", _options.Branch);

                if (!pull.Success)
                {
                    await _git.RunAsync(workspace, "merge", "--abort");

                    return new SyncOutcome(
                        SyncApplyResult.Conflict,
                        null,
                        "Conflito entre o hub e o repositório. Resolva no repositório e sincronize de novo.");
                }

                await ImportWorkspaceIntoDatabaseAsync(workspace);
            }

            var push = await _git.RunAsync(workspace, "push", "origin", _options.Branch);

            if (!push.Success)
            {
                return new SyncOutcome(SyncApplyResult.GitFailure, null, Mask(push.StandardError));
            }

            var head = await _git.RunAsync(workspace, "rev-parse", "HEAD");
            var hash = head.StandardOutput.Trim();

            if (plan.WillPush)
            {
                await RecordCommitAsync(workspace, hash, plan);
            }

            return new SyncOutcome(SyncApplyResult.Success, hash, null);
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task RecordCommitAsync(string workspace, string hash, SyncPlan plan)
    {
        if (await _commitRepository.ExistsAsync(hash))
        {
            return;
        }

        var show = await _git.RunAsync(
            workspace, "show", "-s", "--format=%an%n%aI%n%B", hash);

        var lines = show.StandardOutput.Split('\n');
        var author = lines.Length > 0 ? lines[0].Trim() : _options.AuthorName;
        var committedAt = lines.Length > 1 && DateTime.TryParse(
            lines[1].Trim(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed.ToUniversalTime()
            : DateTime.UtcNow;

        var files = new List<SyncCommitFile>();

        foreach (var change in plan.OutgoingChanges)
        {
            var absolute = Path.Combine(workspace, change.Path.Replace('/', Path.DirectorySeparatorChar));
            var content = change.ChangeType == SyncChangeType.Deleted || !File.Exists(absolute)
                ? string.Empty
                : await File.ReadAllTextAsync(absolute);

            var noteId = content.Length == 0 ? null : NoteFileFormat.Parse(content).Id;

            files.Add(new SyncCommitFile(
                change.Path, change.ChangeType, noteId, content, change.Insertions, change.Deletions));
        }

        await _commitRepository.AddAsync(new SyncCommit(
            hash, plan.CommitMessage, author, committedAt, SyncDirection.Push, files));

        await _commitRepository.SaveChangesAsync();
    }

    // ── Importação (árvore → banco) ─────────────────────────────

    private async Task ImportWorkspaceIntoDatabaseAsync(string workspace)
    {
        var existingFolders = await _folderRepository.GetAllAsync();
        var existingNotes = await _noteRepository.GetAllAsync();

        var pathToFolderId = await ImportFoldersAsync(workspace, existingFolders);
        await ImportNotesAsync(workspace, existingNotes, pathToFolderId);

        await _noteRepository.SaveChangesAsync();
    }

    private async Task<Dictionary<string, Guid?>> ImportFoldersAsync(
        string workspace, List<NoteFolder> existingFolders)
    {
        var byParentAndName = existingFolders
            .ToDictionary(f => (f.ParentFolderId, f.Name), f => f);

        var pathToFolderId = new Dictionary<string, Guid?> { [string.Empty] = null };
        var seenFolderIds = new HashSet<Guid>();

        foreach (var path in NoteImporter.ReadFolders(workspace))
        {
            var lastSlash = path.LastIndexOf('/');
            var name = Truncate(lastSlash < 0 ? path : path[(lastSlash + 1)..], 100);
            var parentPath = lastSlash < 0 ? string.Empty : path[..lastSlash];
            var parentId = pathToFolderId.TryGetValue(parentPath, out var resolvedParentId)
                ? resolvedParentId
                : null;

            if (byParentAndName.TryGetValue((parentId, name), out var existingFolder))
            {
                pathToFolderId[path] = existingFolder.Id;
                seenFolderIds.Add(existingFolder.Id);
            }
            else
            {
                var newFolder = new NoteFolder(name, parentId);
                await _folderRepository.AddAsync(newFolder);
                byParentAndName[(parentId, name)] = newFolder;
                pathToFolderId[path] = newFolder.Id;
                seenFolderIds.Add(newFolder.Id);
            }
        }

        foreach (var folder in existingFolders)
        {
            if (!seenFolderIds.Contains(folder.Id))
            {
                _folderRepository.Remove(folder);
            }
        }

        return pathToFolderId;
    }

    private async Task ImportNotesAsync(
        string workspace, List<Note> existingNotes, Dictionary<string, Guid?> pathToFolderId)
    {
        var notesById = existingNotes.ToDictionary(n => n.Id);
        var seenNoteIds = new HashSet<Guid>();

        foreach (var imported in NoteImporter.Read(workspace))
        {
            var folderId = pathToFolderId.TryGetValue(imported.FolderPath, out var resolvedFolderId)
                ? resolvedFolderId
                : null;

            var title = Truncate(imported.Title, 200);
            var content = imported.Content.Body;
            var tags = imported.Content.Tags;

            if (imported.Id is { } noteId && notesById.TryGetValue(noteId, out var existingNote))
            {
                var uniqueTitle = await ResolveUniqueTitleAsync(title, folderId, excludeId: noteId);
                existingNote.Update(uniqueTitle, content, tags);
                existingNote.MoveTo(folderId);

                if (existingNote.IsPinned != imported.Content.IsPinned)
                {
                    existingNote.TogglePin();
                }

                seenNoteIds.Add(noteId);
            }
            else
            {
                var uniqueTitle = await ResolveUniqueTitleAsync(title, folderId, excludeId: null);
                var newNote = new Note(uniqueTitle, content, folderId, tags);

                if (imported.Content.IsPinned)
                {
                    newNote.TogglePin();
                }

                await _noteRepository.AddAsync(newNote);
                seenNoteIds.Add(newNote.Id);
            }
        }

        foreach (var note in existingNotes)
        {
            if (!seenNoteIds.Contains(note.Id))
            {
                _noteRepository.Remove(note);
            }
        }
    }

    private async Task<string> ResolveUniqueTitleAsync(string title, Guid? folderId, Guid? excludeId)
    {
        var candidate = title;
        var attempt = 2;

        while (await _noteRepository.ExistsAsync(folderId, candidate, excludeId))
        {
            candidate = $"{title} ({attempt})";
            attempt++;
        }

        return candidate;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}
