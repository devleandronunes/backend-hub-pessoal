using HubPessoal.Application.Notes;

namespace HubPessoal.UnitTests.Notes;

public class NoteFileFormatTests
{
    [Fact]
    public void Parse_RoundTripsSerializedContent()
    {
        var id = Guid.NewGuid();
        var createdAt = DateTime.UtcNow;
        var serialized = NoteFileFormat.Serialize(id, new List<string> { "a", "b" }, true, createdAt, "corpo da nota");

        var parsed = NoteFileFormat.Parse(serialized);

        Assert.Equal(id, parsed.Id);
        Assert.Equal(new List<string> { "a", "b" }, parsed.Tags);
        Assert.True(parsed.IsPinned);
        Assert.Equal("corpo da nota", parsed.Body.Trim());
    }

    [Fact]
    public void Parse_WithMalformedHeader_NeverThrows()
    {
        var parsed = NoteFileFormat.Parse("---\nisso não é yaml válido\ncontinua\n---\n\ncorpo");

        Assert.Null(parsed.Id);
    }

    [Fact]
    public void Parse_WithoutDelimiters_TreatsEntireContentAsBody()
    {
        var parsed = NoteFileFormat.Parse("apenas texto solto, sem front matter");

        Assert.Null(parsed.Id);
        Assert.Equal("apenas texto solto, sem front matter", parsed.Body);
    }

    [Fact]
    public void Sanitize_ReplacesInvalidCharactersButKeepsAccents()
    {
        Assert.Equal("Reunião- 01", NoteFileMapper.Sanitize("Reunião: 01"));
    }

    [Fact]
    public void Sanitize_WithOnlyWhitespace_FallsBackToUntitled()
    {
        Assert.Equal("untitled", NoteFileMapper.Sanitize("   "));
    }
}
