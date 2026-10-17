using System.Text;

namespace ECommerce.Core.Services;

// "Home & Kitchen" -> "home-and-kitchen". Made on the server from the name, so slugs are always URL-safe
// and the client can't send one that clashes with the format.
public static class Slug
{
    public static string From(string text)
    {
        var builder = new StringBuilder();
        foreach (var c in text.Replace("&", " and ").ToLowerInvariant())
        {
            if (char.IsAsciiLetterOrDigit(c))
            {
                builder.Append(c);
            }
            // Any run of spaces or punctuation becomes one hyphen, never a leading one.
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        return builder.ToString().TrimEnd('-');
    }
}
