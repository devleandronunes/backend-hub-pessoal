using HubPessoal.Application.Interfaces;
using HubPessoal.Application.Services;
using HubPessoal.Domain.Entities;
using Moq;

namespace HubPessoal.UnitTests.Services;

public class NoteServiceTests {

    private readonly Mock<INoteRepository> _noteRepository = new();
    private readonly Mock<INoteFolderRepository> _folderRepository = new();
    private readonly NoteService _sut;

    public NoteServiceTests()
    {
        _sut = new NoteService(_noteRepository.Object, _folderRepository.Object);
    }

    [Fact]
    public async Task CreateAsync_WithDuplicateTitleInSameFolder_ReturnDuplicateTitle()
    {
        _noteRepository.Setup(r => r.ExistsAsync(null, "Nota", null)).ReturnsAsync(true);

        var (result, note) = await _sut.CreateAsync("Nota", "conteúdo", null, new List<string>());

        Assert.Equal(CreateNoteResult.DuplicateTitle, result);
        Assert.Null(note);
        _noteRepository.Verify(r => r.AddAsync(It.IsAny<Note>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_WithNonexistentNote_ReturnsNotFound()
    {
        _noteRepository.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((Note?)null);

        var (result, note) = await _sut.UpdateAsync(Guid.NewGuid(), "Título", "conteúdo", new List<string>());

        Assert.Equal(UpdateNoteResult.NotFound, result);
        Assert.Null(note);
    }
}