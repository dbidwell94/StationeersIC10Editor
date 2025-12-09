using System.Collections.Generic;


namespace StationeersIC10Editor;

public class PythonFormatter : StaticFormatter
{
    static readonly Dictionary<string, Style> PythonKeywords = new()
    {
        { "def",      Theme.DefaultTheme.Keyword },
        { "import",   Theme.DefaultTheme.Keyword },
        { "from",     Theme.DefaultTheme.Keyword },
        { "as",       Theme.DefaultTheme.Keyword },
        { "class",    Theme.DefaultTheme.Keyword },
        { "try",      Theme.DefaultTheme.Keyword },
        { "except",   Theme.DefaultTheme.Keyword },
        { "finally",  Theme.DefaultTheme.Keyword },
        { "with",     Theme.DefaultTheme.Keyword },
        { "lambda",   Theme.DefaultTheme.Keyword },
        { "in",       Theme.DefaultTheme.Keyword },
        { "is",       Theme.DefaultTheme.Keyword },
        { "not",      Theme.DefaultTheme.Keyword },
        { "and",      Theme.DefaultTheme.Keyword },
        { "or",       Theme.DefaultTheme.Keyword },

        // Python constants
        { "True",     Theme.DefaultTheme.NumberLiteral },
        { "False",    Theme.DefaultTheme.NumberLiteral },
        { "None",     Theme.DefaultTheme.NumberLiteral },

        // Types
        { "int",      Theme.DefaultTheme.TypeName },
        { "float",    Theme.DefaultTheme.TypeName },
        { "str",      Theme.DefaultTheme.TypeName },
        { "list",     Theme.DefaultTheme.TypeName },
        { "dict",     Theme.DefaultTheme.TypeName },
        { "set",      Theme.DefaultTheme.TypeName },
        { "tuple",    Theme.DefaultTheme.TypeName },

        { "return",   Theme.DefaultTheme.Control },
        { "if",       Theme.DefaultTheme.Control },
        { "else",     Theme.DefaultTheme.Control },
        { "elif",     Theme.DefaultTheme.Control },
        { "for",      Theme.DefaultTheme.Control },
        { "while",    Theme.DefaultTheme.Control },
        { "pass",     Theme.DefaultTheme.Control },
        { "break",    Theme.DefaultTheme.Control },
        { "continue", Theme.DefaultTheme.Control },
    };

    public PythonFormatter()
        : base(
            TokenSeparators: " \t()[]{}:.,+-*/%=<>!&|^~",
            StringDelimiters: "\"\'",
            CommentPrefix: "#",
            KeepWhitespaces: false,
            Keywords: PythonKeywords)
    {
        OnCodeChanged += () =>
        {
        };
    }
}
