namespace ImGuiEditor.LSP;

public class TextDocumentIdentifier
{
    public string uri { get; set; } = string.Empty;
}

public class DidOpenTextDocumentParams
{
    public TextDocumentItem textDocument { get; set; } = new();
}

public class TextDocumentItem
{
    public string uri { get; set; } = string.Empty;
    public string languageId { get; set; } = string.Empty;
    public int version { get; set; }
    public string text { get; set; } = string.Empty;
}

public class DidChangeTextDocumentParams
{
    public VersionedTextDocumentIdentifier textDocument { get; set; } = new();
    public TextDocumentContentChangeEvent[] contentChanges { get; set; } = [];
}

public class VersionedTextDocumentIdentifier : TextDocumentIdentifier
{
    public int version { get; set; }
}

public class Range
{
    public Position start { get; set; }
    public Position end { get; set; }
}

public class Position
{
    public int line { get; set; }
    public int character { get; set; }
}

public class TextDocumentContentChangeEvent
{
    public string text { get; set; } = string.Empty;

    public Range? range { get; set; }
}

public class DidCloseTextDocumentParams
{
    public TextDocumentIdentifier textDocument { get; set; } = new();
}


public class SemanticTokensLegend
{
    public string[] tokenTypes { get; set; } = [];
    public string[] tokenModifiers { get; set; } = [];
}

public class SemanticTokensParams
{
    public TextDocumentIdentifier textDocument { get; set; } = new();
}

public class SemanticTokens
{
    public string resultId { get; set; } = string.Empty;
    public int[] data { get; set; } = [];
}

public class Diagnostic
{
    public Range range { get; set; } = new();
    public int severity { get; set; }
    public string code { get; set; } = string.Empty;
    public string source { get; set; } = string.Empty;
    public string message { get; set; } = string.Empty;
}

public class PublishDiagnosticsParams
{
    public TextDocumentIdentifier textDocument { get; set; } = new();
    public Diagnostic[] diagnostics { get; set; } = [];
}


public class DocumentDiagnosticParams
{
    public TextDocumentIdentifier textDocument { get; set; } = new();
}
