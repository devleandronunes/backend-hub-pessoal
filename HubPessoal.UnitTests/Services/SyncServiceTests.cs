using HubPessoal.Application.Interfaces;
using HubPessoal.Application.Models;
using HubPessoal.Application.Options;
using HubPessoal.Application.Services;
using HubPessoal.Domain.Entities;
using Microsoft.Extensions.Options;
using Moq;

namespace HubPessoal.UnitTests.Services;

public class SyncServiceTests : IDisposable
{
    private readonly Mock<IGitClient> _git = new();
    private readonly Mock<INoteRepository> _noteRepository = new();
    private readonly Mock<INoteFolderRepository> _folderRepository = new();
    private readonly Mock<ISyncCommitRepository> _commitRepository = new();
    private readonly string _workspace;
    private readonly SyncService _sut;

    public SyncServiceTests()
    {
        _workspace = Path.Combine(Path.GetTempPath(), $"hub-sync-tests-{Guid.NewGuid()}");

        _noteRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Note>());
        _folderRepository.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<NoteFolder>());

        var options = Options.Create(new GitOptions
        {
            RepositoryUrl = "https://github.com/example/repo.git",
            Token = "fake-token",
            Branch = "main",
            AuthorName = "tester",
            AuthorEmail = "tester@local",
            WorkingDirectory = _workspace,
        });

        _sut = new SyncService(
            _git.Object, _noteRepository.Object, _folderRepository.Object, _commitRepository.Object, options);
    }

    // Cada chamada de git dentro do SyncService (clone, config, add, diff, rev-list...) passa por
    // aqui — só as que a lógica de negócio realmente lê (diff --numstat/--name-status, rev-list)
    // recebem saída controlada; o resto (clone, config, add) só precisa "dar certo".
    private void SetupGit(string numstat, string nameStatus, int incomingCommits)
    {
        _git.Setup(g => g.RunAsync(It.IsAny<string>(), It.IsAny<string[]>()))
            .ReturnsAsync((string _, string[] args) => args[0] switch
            {
                "diff" when args.Contains("--numstat") => new GitResult(0, numstat, ""),
                "diff" when args.Contains("--name-status") => new GitResult(0, nameStatus, ""),
                "rev-list" => new GitResult(0, $"{incomingCommits}\n", ""),
                _ => new GitResult(0, "", ""),
            });
    }

    [Fact]
    public async Task PreviewAsync_WithOneFileAddedAndNothingIncoming_ReturnsLocalChangesWithSingularMessage()
    {
        SetupGit(numstat: "5\t0\tnotes/Nova.md\n", nameStatus: "A\tnotes/Nova.md\n", incomingCommits: 0);

        var plan = await _sut.PreviewAsync();

        Assert.Equal(SyncState.LocalChanges, plan.State);
        Assert.True(plan.WillPush);
        Assert.False(plan.WillPull);
        Assert.Equal("feat(note): cria \"Nova\"", plan.CommitMessage);
    }

    [Fact]
    public async Task PreviewAsync_WithNoLocalChangesAndCommitsBehind_ReturnsRemoteChanges()
    {
        SetupGit(numstat: "", nameStatus: "", incomingCommits: 3);

        var plan = await _sut.PreviewAsync();

        Assert.Equal(SyncState.RemoteChanges, plan.State);
        Assert.False(plan.WillPush);
        Assert.True(plan.WillPull);
    }

    [Fact]
    public async Task PreviewAsync_WithLocalChangesAndCommitsBehind_ReturnsDiverged()
    {
        SetupGit(numstat: "1\t0\tnotes/Nova.md\n", nameStatus: "A\tnotes/Nova.md\n", incomingCommits: 1);

        var plan = await _sut.PreviewAsync();

        Assert.Equal(SyncState.Diverged, plan.State);
    }

    [Fact]
    public async Task PreviewAsync_WithNothingChanged_ReturnsClean()
    {
        SetupGit(numstat: "", nameStatus: "", incomingCommits: 0);

        var plan = await _sut.PreviewAsync();

        Assert.Equal(SyncState.Clean, plan.State);
        Assert.False(plan.WillPull);
        Assert.False(plan.WillPush);
    }

    [Fact]
    public async Task PreviewAsync_WithMultipleFiles_BuildsPluralCommitMessage()
    {
        SetupGit(
            numstat: "2\t0\tnotes/A.md\n1\t1\tnotes/B.md\n",
            nameStatus: "A\tnotes/A.md\nM\tnotes/B.md\n",
            incomingCommits: 0);

        var plan = await _sut.PreviewAsync();

        Assert.StartsWith("chore(notes): sincroniza 2 alterações", plan.CommitMessage);
        Assert.Equal(2, plan.FilesChanged);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workspace))
        {
            Directory.Delete(_workspace, recursive: true);
        }
    }
}
