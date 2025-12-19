using System.Collections.Generic;

namespace StationeersIC10Editor;

public class CSharpFormatter : StaticFormatter
{
    static readonly Dictionary<string, uint> CSharpKeywords = new()
    {
        //
        // CONTROL FLOW
        //
        { "if",       15},
        { "else",     15},
        { "switch",   15},
        { "case",     15 },
        { "default",  15 },
        { "for",      15 },
        { "foreach",  15 },
        { "while",    15 },
        { "do",       15 },
        { "break",    15 },
        { "continue", 15 },
        { "return",   15 },
        { "goto",     15 },
        { "yield",    15 },

        //
        // DECLARATIONS
        //
        { "class",     15 },
        { "struct",    15 },
        { "interface", 15 },
        { "enum",      15 },
        { "namespace", 0 },
        { "using",     0 },

        //
        // MODIFIERS
        //
        { "public",     16 },
        { "private",    16 },
        { "protected",  16 },
        { "internal",   16 },
        { "static",     16 },
        { "readonly",   16 },
        { "const",      16 },
        { "virtual",    16 },
        { "override",   16 },
        { "abstract",   16 },
        { "sealed",     16 },
        { "partial",    16 },
        { "async",      16 },
        { "unsafe",     16 },
        { "extern",     16 },
        { "volatile",   16 },

        //
        // TYPES
        //
        { "void",       1 },
        { "object",     1 },
        { "string",     1 },
        { "bool",       1 },
        { "byte",       1 },
        { "sbyte",      1 },
        { "short",      1 },
        { "ushort",     1 },
        { "int",        1 },
        { "uint",       1 },
        { "long",       1 },
        { "ulong",      1 },
        { "float",      1 },
        { "double",     1 },
        { "decimal",    1 },
        { "char",       1 },

        //
        // CONTEXTUAL KEYWORDS
        //
        { "var",         15 },
        { "dynamic",     15 },
        { "await",       15 },
        { "nameof",      15 },
        { "checked",     15 },
        { "unchecked",   15 },
        { "lock",        15 },
        { "base",        15 },
        { "this",        15 },
        { "new",         15 },
        { "typeof",      15 },
        { "sizeof",      15 },
        { "stackalloc",  15 },
        { "ref",         15 },
        { "out",         15 },
        { "in",          15 },

        //
        // SPECIAL VALUES
        //
        { "true",   19},
        { "false",  19},
        { "null",   19},

        //
        // EXCEPTIONS & ERROR HANDLING
        //
        { "try",       15 },
        { "catch",     15 },
        { "finally",   15 },
        { "throw",     15 },

        //
        // OTHER LANGUAGE FEATURES
        //
        { "is",     15 },
        { "as",     15 },
        { "operator", 15 },
        { "delegate", 15 },
        { "event",    15 },
        { "params",   15 },
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
