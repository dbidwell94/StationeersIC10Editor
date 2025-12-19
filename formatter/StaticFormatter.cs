using System.Collections.Generic;

namespace StationeersIC10Editor;

public class StaticFormatter : ICodeFormatter
{
    protected HashSet<char> TokenSeparators = null;
    protected HashSet<char> StringDelimiters = null;
    protected string CommentPrefix;
    protected Dictionary<string, uint> Keywords = null;
    protected bool KeepWhitespaces = true;

    public StaticFormatter(string TokenSeparators, string StringDelimiters, string CommentPrefix, Dictionary<string, uint> Keywords = null, bool KeepWhitespaces = true)
    {
        this.TokenSeparators = new HashSet<char>(TokenSeparators.ToCharArray());
        this.StringDelimiters = new HashSet<char>(StringDelimiters.ToCharArray());
        this.CommentPrefix = CommentPrefix;
        this.Keywords = Keywords == null ? new Dictionary<string, uint>() : Keywords;
        this.KeepWhitespaces = KeepWhitespaces;
    }

    public List<Token> TokenizeLine(string lineText)
    {
        if (string.IsNullOrEmpty(lineText))
            return new List<Token>();

        var tokens = new List<Token>();
        var comment = string.Empty;

        if (lineText.Contains(CommentPrefix))
        {
            int commentIndex = lineText.IndexOf(CommentPrefix);
            comment = lineText.Substring(commentIndex);
            lineText = lineText.Substring(0, commentIndex);
        }

        int col = 0;

        var incrementCol = () =>
        {
            if (StringDelimiters.Contains(lineText[col]))
            {
                char delim = lineText[col];
                int endCol = lineText.IndexOf(delim, col + 1);
                if (endCol == -1)
                    endCol = lineText.Length - 1;
                col = endCol + 1;
            }
            else
                col++;
        };

        var addToken = (int start, int end) =>
        {
            if (start >= end || start < 0 || end > lineText.Length)
                return;
            var t = lineText.Substring(start, end - start);

            if (!string.IsNullOrWhiteSpace(t) || KeepWhitespaces)
            {
                var token = new Token(start, t, ICodeFormatter.ColorDefault);
                if (StringDelimiters.Contains(t[0]) && t.Length >= 2 && t[t.Length - 1] == t[0])
                    token.Style = LSPUtils.ColorMap[18]; // String color
                tokens.Add(token);
            }
        };

        string word = string.Empty;
        while (col < lineText.Length)
        {
            if (TokenSeparators.Contains(lineText[col]))
            {
                addToken(col, col + 1);
                col++;
                continue;
            }

            int startCol = col;
            while (col < lineText.Length && !TokenSeparators.Contains(lineText[col]))
                incrementCol();

            addToken(startCol, col);
        }

        if (!string.IsNullOrEmpty(comment))
            tokens.Add(new Token(col, comment, ICodeFormatter.ColorComment));

        return tokens;
    }

    public T TParseLine<T>(string lineText) where T : StyledLine, new()
    {
        var tokens = TokenizeLine(lineText);

        var styledLine = new T();
        styledLine.Text = lineText;

        foreach (var token in tokens)
        {
            if (token.Text.StartsWith(CommentPrefix))
                token.Style = new Style { Color = ICodeFormatter.ColorComment };
            else if (Keywords.ContainsKey(token.Text))
                token.Style = LSPUtils.ColorMap[Keywords[token.Text]];
        }
        styledLine.AddRange(tokens);

        return styledLine;
    }

    public override StyledLine ParseLine(string lineText)
    {
        return TParseLine<StyledLine>(lineText);
    }
}
