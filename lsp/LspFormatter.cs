using System;
using System.Collections.Generic;
using StationeersIC10Editor;

namespace ImGuiEditor.LSP;

public class LSPFormatter : ICodeFormatter
{
    protected VersionedTextDocumentIdentifier Identifier;
    protected LspClient LspClient;
    protected List<TextDocumentContentChangeEvent> _changes = new List<TextDocumentContentChangeEvent>();

    protected bool _isOpen = false;

    public static double MatchingScore(string input)
    {
        return 0;
    }

    public int Version
    {
        get { return Identifier.version; }
        set { Identifier.version = value; }
    }

    public LSPFormatter()
    {
        Identifier = new VersionedTextDocumentIdentifier
        {
            uri = "cache/livemod/Main.cs",
            version = 1
        };

        OnCodeChanged += SubmitChanges;

        LspClient = LspClient.StartPyrightLSPServer();
        // LspClient = new LspClientSocket("localhost", 9222);
        LspClient.OnInfo += (msg) => L.Info($"[LSP] {msg}");
        LspClient.OnError += (msg) => L.Error($"[LSP] {msg}");
        LspClient.OnDiagnostics += UpdateDiagnostics;
    }

    public int IncrementVersion()
    {
        Identifier.version += 1;
        return Identifier.version;
    }

    public void UpdateDiagnostics(PublishDiagnosticsParams p)
    {
        L.Info($"Received diagnostics for {p.textDocument.uri}, {p.diagnostics.Length} items");
        // if (p.textDocument.uri != Identifier.uri)
        //     return;

        foreach (var diag in p.diagnostics)
        {
            L.Info($"Diagnostic: {diag.message} at {diag.range.start.line},{diag.range.start.character} severity {diag.severity}");
            // if (diag.severity > 1)
            //     continue;

            L.Info($"Processing error diagnostic at {diag.range.start.line},{diag.range.start.character}: {diag.message}");

            var start = diag.range.start;
            var end = diag.range.end;
            var line = start.line;
            var column = start.character;
            var length = end.character - start.character;
            if (end.line > start.line)
                length = Lines[line].Text.Length - start.character;
            if (line < Lines.Count)
            {
                // Lines[line].Version = Identifier.version;
                L.Info($"Locating token at {line},{column}");
                var token = Lines[line].GetTokenAt(column);
                if (token == null)
                {
                    L.Info($"ERROR: No token Adding error token at {line},{column}: {diag.message}");
                    // Lines[line].AddToken(new SemanticToken(
                    //     line,
                    //     column,
                    //     length,
                    //     0,
                    //     data: diag.message,
                    //     isError: true
                    // ));
                }
                else
                {
                    L.Info($"Setting error on existing token at {line},{column}: {diag.message}");
                    token.Error = StyledText.ErrorText(diag.message);
                    token.Style.Color = ICodeFormatter.ColorError;
                }
            }
        }
    }

    public void SubmitChanges()
    {
        L.Info("Code changed, submitting changes to LSP");
        if (LspClient != null && _changes.Count > 0 && LspClient.IsInitialized)
        {
            L.Info($"Submitting {_changes.Count} changes to LSP (version {Identifier.version})");
            if (!_isOpen)
            {
                L.Info("Opening document in LSP");
                LspClient.OpenDocument(Identifier.uri, "csharp", Editor.Code);
                _isOpen = true;
            }
            else
            {
                L.Info("Sending changes to LSP");
                LspClient.ChangeDocument(Identifier, _changes.ToArray());
                // LspClient.ChangeDocumentFull(Identifier, Editor.Code);
            }

            L.Info("Changes submitted, request tokens");
            // request semantic tokens update
            UpdateTokens();
            _changes.Clear();
        }
    }

    public async void UpdateTokens()
    {
        var tokens = await LspClient.RequestSemanticTokens(Identifier.uri);
        L.Info($"Received {tokens.Count} semantic tokens from LSP");

        // for (var i = 0; i < Lines.Count; i++)
        // {
        //     Lines[i].Clear();
        // }
        //
        for (var i = 0; i < tokens.Count; i++)
        {
            var semToken = tokens[i];
            var line = semToken.Line;
            if( line < Lines.Count)
            {
                var token = Lines[line].GetTokenAt(semToken.Column);
                if (token != null)
                {
                    L.Info($"Updating token at {line},{semToken.Column} with type {semToken.Type}");
                    token.Style.Color = semToken.Style.Color;
                    // token.Style.Background = semToken.Style.Background;
                }
                else
                {
                    L.Info($"No token found at {line},{semToken.Column} to update");
                }

            }
            // var token = tokens[i];
            // if (token.Line < Lines.Count)
                // Lines[token.Line].Tokens.Add(token);
        }

        // var pos = _lastCaretPos;

        // // var hover = await LspClient.SendRequestAsync("textDocument/hover",
        // //         new
        // //         {
        // //             textDocument = new { uri = Identifier.uri },
        // //             position = new { line = pos.Line, character = pos.Col }
        // //         });
        // // L.Info($"Hover info at {pos.Line},{pos.Col}: {hover}");
        // // var diag = await LspClient.RequestDiagnostics(Identifier);
        // // L.Info($"Received {diag} diagnostics from manual request");
    }

    protected static HashSet<char> splitSet = new HashSet<char>(new char[] { ' ', '\t', '(', ')', ':', '.', ',', ';', '+', '-', '*', '/', '=', '<', '>', '!', '[', ']', '{', '}' });
    protected static Dictionary<string, uint> keywordColors = new Dictionary<string, uint>
    {
        { "def", 0xFF569CD6 },
        { "class", 0xFF569CD6 },
        { "import", 0xFF569CD6 },
        { "from", 0xFF569CD6 },
        { "if", 0xFFC586C0 },
        { "else", 0xFFC586C0 },
        { "elif", 0xFFC586C0 },
        { "while", 0xFFC586C0 },
        { "for", 0xFFC586C0 },
        { "in", 0xFFC586C0 },
        { "return", 0xFF569CD6 },
        { "try", 0xFFC586C0 },
        { "except", 0xFFC586C0 },
        { "with", 0xFFC586C0 },
        { "as", 0xFFC586C0 },
        { "pass", 0xFF569CD6 },
        { "break", 0xFF569CD6 },
        { "continue", 0xFF569CD6 },
        { "True", 0xFF569CD6 },
        { "False", 0xFF569CD6 },
        { "None", 0xFF569CD6 },
    };

    public override StyledLine ParseLine(string line)
    {
        L.Info($"Parsing line: '{line}'");
        {
            var styledLine = new StyledLine(line);
            styledLine.Clear();
            int i = 0;

            while (i < line.Length)
            {
                int start = i;

                if (splitSet.Contains(line[i]))
                {
                    if (!char.IsWhiteSpace(line[i]))
                        styledLine.Add(new Token
                        (
                            i,
                            line[i].ToString(),
                            ColorDefault
                        ));
                    i++;
                }
                else
                {
                    var sb = new System.Text.StringBuilder();

                    while (i < line.Length && !splitSet.Contains(line[i]))
                    {
                        sb.Append(line[i]);
                        i++;
                    }
                    L.Info($"Parsed token: '{sb}' at {start}");

                    var tokenText = sb.ToString();
                    uint color = 0;
                    keywordColors.TryGetValue(tokenText, out color);
                    color = color == 0 ? ColorDefault : color;

                    styledLine.Add(new Token
                    (
                        start,
                        tokenText,
                        color
                    ));
                }
            }

            return styledLine;
        }

        // // split python code into tokens
        // var tokens = line.Split(' ', '\t', '(', ')', ':', '.', ',', ';');
        // var styledLine = new StyledLine(line);
        // styledLine.Clear();
        // int column = 0;
        // foreach (var token in tokens)
        // {
        //     if (string.IsNullOrWhiteSpace(token))
        //     {
        //         column += token.Length;
        //         continue;
        //     }
        //
        //     var txt = line.Substring(column, token.Length);
        //     var tok = new Token(
        //         column,
        //         txt
        //     );
        //     if (txt == "while")
        //         tok.Style.Color = ICodeFormatter.ColorNumber;
        //     styledLine.Add(tok);
        //     column += token.Length + 1; // +1 for the delimiter
        // }
        // return new StyledLine(line);
    }

    public override void ReplaceLine(int index, string newLine)
    {
        IncrementVersion();
        _changes.Add(new TextDocumentContentChangeEvent
        {
            range = new Range
            {
                start = new Position { line = index, character = 0 },
                end = new Position { line = index, character = Lines[index].Text.Length }
            },
            text = newLine
        });
        Lines[index] = ParseLine(newLine);
    }

    public override void AppendLine(string line)
    {
        IncrementVersion();
        var iLine = Lines.Count > 0 ? Lines.Count - 1 : 0;
        var iChar = Lines.Count > 0 ? Lines[iLine].Text.Length : 0;
        _changes.Add(new TextDocumentContentChangeEvent
        {
            range = new Range
            {
                start = new Position { line = iLine, character = iChar },
                end = new Position { line = iLine, character = iChar }
            },
            text = Lines.Count > 0 ? "\n" + line : line,
        });
        Lines.Add(ParseLine(line));
    }

    public override void InsertLine(int index, string line)
    {
        IncrementVersion();
        _changes.Add(new TextDocumentContentChangeEvent
        {
            range = new Range
            {
                start = new Position { line = index, character = 0 },
                end = new Position { line = index, character = 0 }
            },
            text = line + "\n"
        });
        Lines.Insert(index, ParseLine(line));
    }

    public override void RemoveLine(int index)
    {
        IncrementVersion();
        _changes.Add(new TextDocumentContentChangeEvent
        {
            range = new Range
            {
                start = new Position { line = index, character = 0 },
                end = new Position { line = index + 1, character = 0 }
            },
            text = ""
        });
        Lines.RemoveAt(index);
    }

    public override void ResetCode(string code)
    {
        IncrementVersion();
        _changes.Add(new TextDocumentContentChangeEvent
        {
            range = new Range
            {
                start = new Position { line = 0, character = 0 },
                end = new Position { line = Lines.Count, character = 0 }
            },
            text = code
        });
        var lines = code.Split('\n');
        Lines.Clear();
        foreach (var line in lines)
            AppendLine(line);
        OnCodeChanged();
    }
    // public void Dispose()
    // {
    // L.Info("Disposing LSP Formatter and closing document");
    // LspClient.CloseDocument(Identifier.uri);
    // }
}
