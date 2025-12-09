using System.Collections.Generic;

namespace StationeersIC10Editor;

public class CSharpFormatter : StaticFormatter
{
    static readonly Dictionary<string, Style> CSharpKeywords = new()
    {
        //
        // CONTROL FLOW
        //
        { "if",       Theme.DefaultTheme.Keyword },
        { "else",     Theme.DefaultTheme.Keyword },
        { "switch",   Theme.DefaultTheme.Keyword },
        { "case",     Theme.DefaultTheme.Keyword },
        { "default",  Theme.DefaultTheme.Keyword },
        { "for",      Theme.DefaultTheme.Keyword },
        { "foreach",  Theme.DefaultTheme.Keyword },
        { "while",    Theme.DefaultTheme.Keyword },
        { "do",       Theme.DefaultTheme.Keyword },
        { "break",    Theme.DefaultTheme.Keyword },
        { "continue", Theme.DefaultTheme.Keyword },
        { "return",   Theme.DefaultTheme.Keyword },
        { "goto",     Theme.DefaultTheme.Keyword },
        { "yield",    Theme.DefaultTheme.Keyword },

        //
        // DECLARATIONS
        //
        { "class",     Theme.DefaultTheme.Keyword },
        { "struct",    Theme.DefaultTheme.Keyword },
        { "interface", Theme.DefaultTheme.Keyword },
        { "enum",      Theme.DefaultTheme.Keyword },
        { "namespace", Theme.DefaultTheme.Namespace },
        { "using",     Theme.DefaultTheme.Namespace },

        //
        // MODIFIERS
        //
        { "public",     Theme.DefaultTheme.Modifier },
        { "private",    Theme.DefaultTheme.Modifier },
        { "protected",  Theme.DefaultTheme.Modifier },
        { "internal",   Theme.DefaultTheme.Modifier },
        { "static",     Theme.DefaultTheme.Modifier },
        { "readonly",   Theme.DefaultTheme.Modifier },
        { "const",      Theme.DefaultTheme.Modifier },
        { "virtual",    Theme.DefaultTheme.Modifier },
        { "override",   Theme.DefaultTheme.Modifier },
        { "abstract",   Theme.DefaultTheme.Modifier },
        { "sealed",     Theme.DefaultTheme.Modifier },
        { "partial",    Theme.DefaultTheme.Modifier },
        { "async",      Theme.DefaultTheme.Modifier },
        { "unsafe",     Theme.DefaultTheme.Modifier },
        { "extern",     Theme.DefaultTheme.Modifier },
        { "volatile",   Theme.DefaultTheme.Modifier },

        //
        // TYPES
        //
        { "void",       Theme.DefaultTheme.TypeName },
        { "object",     Theme.DefaultTheme.TypeName },
        { "string",     Theme.DefaultTheme.TypeName },
        { "bool",       Theme.DefaultTheme.TypeName },
        { "byte",       Theme.DefaultTheme.TypeName },
        { "sbyte",      Theme.DefaultTheme.TypeName },
        { "short",      Theme.DefaultTheme.TypeName },
        { "ushort",     Theme.DefaultTheme.TypeName },
        { "int",        Theme.DefaultTheme.TypeName },
        { "uint",       Theme.DefaultTheme.TypeName },
        { "long",       Theme.DefaultTheme.TypeName },
        { "ulong",      Theme.DefaultTheme.TypeName },
        { "float",      Theme.DefaultTheme.TypeName },
        { "double",     Theme.DefaultTheme.TypeName },
        { "decimal",    Theme.DefaultTheme.TypeName },
        { "char",       Theme.DefaultTheme.TypeName },

        //
        // CONTEXTUAL KEYWORDS
        //
        { "var",         Theme.DefaultTheme.Keyword },
        { "dynamic",     Theme.DefaultTheme.Keyword },
        { "await",       Theme.DefaultTheme.Keyword },
        { "nameof",      Theme.DefaultTheme.Keyword },
        { "checked",     Theme.DefaultTheme.Keyword },
        { "unchecked",   Theme.DefaultTheme.Keyword },
        { "lock",        Theme.DefaultTheme.Keyword },
        { "base",        Theme.DefaultTheme.Keyword },
        { "this",        Theme.DefaultTheme.Keyword },
        { "new",         Theme.DefaultTheme.Keyword },
        { "typeof",      Theme.DefaultTheme.Keyword },
        { "sizeof",      Theme.DefaultTheme.Keyword },
        { "stackalloc",  Theme.DefaultTheme.Keyword },
        { "ref",         Theme.DefaultTheme.Keyword },
        { "out",         Theme.DefaultTheme.Keyword },
        { "in",          Theme.DefaultTheme.Keyword },

        //
        // SPECIAL VALUES
        //
        { "true",   Theme.DefaultTheme.Constant },
        { "false",  Theme.DefaultTheme.Constant },
        { "null",   Theme.DefaultTheme.Constant },

        //
        // EXCEPTIONS & ERROR HANDLING
        //
        { "try",       Theme.DefaultTheme.Keyword },
        { "catch",     Theme.DefaultTheme.Keyword },
        { "finally",   Theme.DefaultTheme.Keyword },
        { "throw",     Theme.DefaultTheme.Keyword },

        //
        // OTHER LANGUAGE FEATURES
        //
        { "is",     Theme.DefaultTheme.Keyword },
        { "as",     Theme.DefaultTheme.Keyword },
        { "operator", Theme.DefaultTheme.Keyword },
        { "delegate", Theme.DefaultTheme.Keyword },
        { "event",    Theme.DefaultTheme.Keyword },
        { "params",   Theme.DefaultTheme.Keyword },
    };

    public CSharpFormatter()
        : base(
            TokenSeparators: " \t(){}[];.,+-=*/%!<>|&^~?:",
            StringDelimiters: "\"\'",
            CommentPrefix: "//",
            KeepWhitespaces: false,
            Keywords: CSharpKeywords
            )
    {
        OnCodeChanged += () =>
        {
        };
    }
}
