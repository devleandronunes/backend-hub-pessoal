using System.Text;

namespace HubPessoal.Api.Contracts.Notes;

public static class NoteContentEnconding
{
    public static string? Resolve(string? content, string? contentBase64)
    {
        if(contentBase64 is null)
        {
            return content;
        }

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(contentBase64));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}