using System.Globalization;
using System.Text;

namespace HubPessoal.Application.Notes;

public record NoteFileContent(Guid? Id, List<string> Tags, bool IsPinned, DateTime? CreatedAt, string Body);

public static class NoteFileFormat
{
    private const string Delimiter = "---";

    public static string Serialize(Guid id, List<string> tags, bool isPinned, DateTime createdAt, string body)
    {
        var builder = new StringBuilder();

        builder.Append(Delimiter).Append('\n');
        builder.Append("id: ").Append(id).Append('\n');
        builder.Append("tags: [").Append(string.Join(", ", tags)).Append("]\n");
        builder.Append("pinned: ").Append(isPinned ? "true" : "false").Append('\n');
        builder.Append("createdAt: ").Append(createdAt.ToString("O", CultureInfo.InvariantCulture)).Append('\n');
        builder.Append(Delimiter).Append("\n\n");
        builder.Append(body.ReplaceLineEndings("\n"));

        if (!body.EndsWith('\n'))
        {
            builder.Append('\n');
        }

        return builder.ToString();
    }

    public static NoteFileContent Parse(string fileContent)
    {
        var normalized = fileContent.ReplaceLineEndings("\n");

        if (!normalized.StartsWith(Delimiter + "\n", StringComparison.Ordinal))
        {
            return new NoteFileContent(null, new List<string>(), false, null, normalized);
        }
        var end = normalized.IndexOf("\n" + Delimiter, Delimiter.Length, StringComparison.Ordinal);

        if (end < 0)
        {
            return new NoteFileContent(null, new List<string>(), false, null, normalized);
        }

        var header = normalized[(Delimiter.Length + 1)..end];
        var body = normalized[(end + Delimiter.Length + 1)..].TrimStart('\n');

        Guid? id = null;
        var tags = new List<string>();
        var isPinned = false;
        DateTime? createdAt = null;

        foreach (var line in header.Split('\n'))
        {
            var separator = line.IndexOf(':');

            if (separator < 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();

            switch (key)
            {
                case "id" when Guid.TryParse(value, out var parseId):
                    id = parseId;
                    break;
                case "tags":
                    tags = ParseTags(value);
                    break;
                case "pinned":
                    isPinned = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                    break;
                case "createdAt" when DateTime.TryParse(
                    value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedDate):
                    createdAt = parsedDate;
                    break;
            }
        }

        return new NoteFileContent(id, tags, isPinned, createdAt, body);
    }

    private static List<string> ParseTags(string value) =>
        value.Trim('[', ']')
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
}