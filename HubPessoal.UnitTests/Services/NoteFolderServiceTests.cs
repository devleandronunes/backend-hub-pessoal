using HubPessoal.Application.Interfaces;
using HubPessoal.Application.Services;
using HubPessoal.Domain.Entities;
using Moq;

namespace HubPessoal.UnitTests.Services;

public class NoteFolderServiceTests
{
    private readonly Mock<INoteFolderRepository> _folderRepository = new();
    private readonly Mock<INoteRepository> _noteRepository = new();
    private readonly NoteFolderService _sut;

    public NoteFolderServiceTests()
    {
        _sut = new NoteFolderService(_folderRepository.Object, _noteRepository.Object);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateNameInSameParent_ReturnsDuplicateName()
    {
        _folderRepository.Setup(r => r.ExistsAsync(null, "Pasta", null)).ReturnsAsync(true);

        var (result, folder) = await _sut.CreateAsync("Pasta", null);

        Assert.Equal(CreateFolderResult.DuplicateName, result);
        Assert.Null(folder);
        _folderRepository.Verify(r => r.AddAsync(It.IsAny<NoteFolder>()), Times.Never);
    }

    [Fact]
    public async Task MoveAsync_IntoItself_ReturnsInvalidParent()
    {
        var folder = new NoteFolder("Pasta", null);
        _folderRepository.Setup(r => r.GetByIdAsync(folder.Id)).ReturnsAsync(folder);

        var result = await _sut.MoveAsync(folder.Id, folder.Id);

        Assert.Equal(MoveFolderResult.InvalidParent, result);
    }

    [Fact]
    public async Task MoveAsync_IntoOwnDescendant_ReturnsInvalidParent()
    {
        var root = new NoteFolder("Raiz", null);
        var child = new NoteFolder("Filha", root.Id);

        _folderRepository.Setup(r => r.GetByIdAsync(root.Id)).ReturnsAsync(root);
        _folderRepository.Setup(r => r.GetByIdAsync(child.Id)).ReturnsAsync(child);

        var result = await _sut.MoveAsync(root.Id, child.Id);

        Assert.Equal(MoveFolderResult.InvalidParent, result);
    }

    [Fact]
    public async Task DeleteAsync_WithNotesInside_ReturnsNotEmpty()
    {
        var folder = new NoteFolder("Pasta", null);
        _folderRepository.Setup(r => r.GetByIdAsync(folder.Id)).ReturnsAsync(folder);
        _folderRepository.Setup(r => r.HasSubfolderAsync(folder.Id)).ReturnsAsync(false);
        _noteRepository.Setup(r => r.HasNotesInFolderAsync(folder.Id)).ReturnsAsync(true);

        var result = await _sut.DeleteAsync(folder.Id);

        Assert.Equal(DeleteFolderResult.NotEmpty, result);
        _folderRepository.Verify(r => r.Remove(It.IsAny<NoteFolder>()), Times.Never);
    }
}
