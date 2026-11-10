using System.Text;

namespace GameHub.Core;

// Valve KeyValues is hierarchical, not JSON. Preserve duplicate names and escapes.
public sealed record VdfNode(string Name, string? Value, IReadOnlyList<VdfNode> Children)
{
    public string? Get(string name) => Children.FirstOrDefault(n => n.Name.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;
    public IEnumerable<VdfNode> Descendants() => Children.SelectMany(c => new[] { c }.Concat(c.Descendants()));
}

public static class Vdf
{
    public static VdfNode Parse(string text)
    {
        var tokens = Tokenize(text).ToArray();
        int position = 0;
        return new("root", null, Read(false));

        List<VdfNode> Read(bool nested)
        {
            var nodes = new List<VdfNode>();
            while (position < tokens.Length)
            {
                string name = tokens[position++];
                if (name == "}")
                {
                    if (!nested) throw new FormatException("Unexpected closing brace.");
                    return nodes;
                }
                if (name == "{" || position >= tokens.Length)
                    throw new FormatException("Missing KeyValues value.");
                string value = tokens[position++];
                if (value == "}") throw new FormatException("Missing KeyValues value.");
                nodes.Add(value == "{" ? new(name, null, Read(true)) : new(name, value, []));
            }
            if (nested) throw new FormatException("Unclosed KeyValues object.");
            return nodes;
        }
    }

    private static IEnumerable<string> Tokenize(string text)
    {
        int i = 0;
        while (i < text.Length)
        {
            char c = text[i];
            if (char.IsWhiteSpace(c) || c == '\uFEFF') { i++; continue; }
            if (c == '/' && i + 1 < text.Length && text[i + 1] == '/')
            { while (i < text.Length && text[i] != '\n') i++; continue; }
            if (c is '{' or '}') { yield return text[i++].ToString(); continue; }
            var word = new StringBuilder();
            if (c == '"')
            {
                i++;
                bool closed = false;
                while (i < text.Length)
                {
                    c = text[i++];
                    if (c == '"') { closed = true; break; }
                    if (c == '\\' && i < text.Length && text[i] is '\\' or '"') c = text[i++];
                    word.Append(c);
                }
                if (!closed) throw new FormatException("Unclosed KeyValues string.");
            }
            else
                while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] is not '{' and not '}') word.Append(text[i++]);
            yield return word.ToString();
        }
    }
}
