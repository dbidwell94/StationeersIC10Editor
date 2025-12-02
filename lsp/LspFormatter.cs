// using System;
// using System.Collections.Generic;
// using StationeersIC10Editor;
//
// namespace ImGuiEditor.LSP;
//
// public class LSPFormatter : ICodeFormatter
// {
//     protected VersionedTextDocumentIdentifier Identifier;
//     protected LspClient LspClient;
//     protected List<TextDocumentContentChangeEvent> _changes = new List<TextDocumentContentChangeEvent>();
//
//     protected bool _isOpen = false;
//
//     public static double MatchingScore(string input)
//     {
//         return 0;
//     }
//
//     public int Version
//     {
//         get { return Identifier.version; }
//         set { Identifier.version = value; }
//     }
//
//     public LSPFormatter()
//     {
//         Identifier = new VersionedTextDocumentIdentifier
//         {
//             uri = "cache/livemod/Main.cs",
//             version = 1
//         };
//
//         OnCodeChanged += SubmitChanges;
//
//         // LspClient = LspClient.StartPyrightLSPServer();
//         LspClient = new LspClientSocket("localhost", 9222);
//         LspClient.OnInfo += (msg) => L.Info($"[LSP] {msg}");
//         LspClient.OnError += (msg) => L.Error($"[LSP] {msg}");
//         LspClient.OnDiagnostics += UpdateDiagnostics;
//     }
//
//     public int IncrementVersion()
//     {
//         Identifier.version += 1;
//         return Identifier.version;
//     }
//
//     public void UpdateDiagnostics(PublishDiagnosticsParams p)
//     {
//         L.Info($"Received diagnostics for {p.textDocument.uri}, {p.diagnostics.Length} items");
//         // if (p.textDocument.uri != Identifier.uri)
//         //     return;
//
//         foreach (var diag in p.diagnostics)
//         {
//             L.Info($"Diagnostic: {diag.message} at {diag.range.start.line},{diag.range.start.character} severity {diag.severity}");
//             // if (diag.severity > 1)
//             //     continue;
//
//             L.Info($"Processing error diagnostic at {diag.range.start.line},{diag.range.start.character}: {diag.message}");
//
//             var start = diag.range.start;
//             var end = diag.range.end;
//             var line = start.line;
//             var column = start.character;
//             var length = end.character - start.character;
//             if (end.line > start.line)
//                 length = Lines[line].Text.Length - start.character;
//             if (line < Lines.Count)
//             {
//                 Lines[line].Version = Identifier.version;
//                 L.Info($"Locating token at {line},{column}");
//                 var token = Lines[line].GetTokenAt(column);
//                 if (token == null)
//                 {
//                     L.Info($"Adding error token at {line},{column}: {diag.message}");
//                     Lines[line].AddToken(new SemanticToken(
//                         line,
//                         column,
//                         length,
//                         0,
//                         data: diag.message,
//                         isError: true
//                     ));
//                 }
//                 else
//                 {
//                     L.Info($"Setting error on existing token at {line},{column}: {diag.message}");
//                     token.SetError(diag.message);
//                 }
//             }
//         }
//     }
//
//     public void SubmitChanges()
//     {
//         if (LspClient != null && _changes.Count > 0 && LspClient.IsInitialized)
//         {
//             L.Info($"Submitting {_changes.Count} changes to LSP (version {Identifier.version})");
//             if (!_isOpen)
//             {
//                 L.Info("Opening document in LSP");
//                 LspClient.OpenDocument(Identifier.uri, "csharp", Editor.Code);
//                 _isOpen = true;
//             }
//             else
//             {
//                 L.Info("Sending changes to LSP");
//                 // LspClient.ChangeDocument(Identifier, _changes.ToArray());
//                 LspClient.ChangeDocumentFull(Identifier, Editor.Code);
//             }
//
//             L.Info("Changes submitted, request tokens");
//             // request semantic tokens update
//             UpdateTokens();
//             _changes.Clear();
//         }
//     }
//
//     public async void UpdateTokens()
//     {
//         var tokens = await LspClient.RequestSemanticTokens(Identifier.uri);
//
//         for (var i = 0; i < Lines.Count; i++)
//         {
//             Lines[i].Tokens.Clear();
//         }
//
//         for (var i = 0; i < tokens.Count; i++)
//         {
//             var token = tokens[i];
//             if (token.Line < Lines.Count)
//                 Lines[token.Line].Tokens.Add(token);
//         }
//
//         // var pos = _lastCaretPos;
//
//         // var hover = await LspClient.SendRequestAsync("textDocument/hover",
//         //         new
//         //         {
//         //             textDocument = new { uri = Identifier.uri },
//         //             position = new { line = pos.Line, character = pos.Col }
//         //         });
//         // L.Info($"Hover info at {pos.Line},{pos.Col}: {hover}");
//         // var diag = await LspClient.RequestDiagnostics(Identifier);
//         // L.Info($"Received {diag} diagnostics from manual request");
//     }
//
//
//     public override Line ParseLine(string line)
//     {
//         return new Line(line);
//     }
//
//     public override void ReplaceLine(int index, string newLine)
//     {
//         IncrementVersion();
//         _changes.Add(new TextDocumentContentChangeEvent
//         {
//             range = new Range
//             {
//                 start = new Position { line = index, character = 0 },
//                 end = new Position { line = index, character = Lines[index].Text.Length }
//             },
//             text = newLine
//         });
//         Lines[index] = ParseLine(newLine);
//     }
//
//     public override void AppendLine(string line)
//     {
//         IncrementVersion();
//         var iLine = Lines.Count > 0 ? Lines.Count - 1 : 0;
//         var iChar = Lines.Count > 0 ? Lines[iLine].Text.Length : 0;
//         _changes.Add(new TextDocumentContentChangeEvent
//         {
//             range = new Range
//             {
//                 start = new Position { line = iLine, character = iChar },
//                 end = new Position { line = iLine, character = iChar }
//             },
//             text = Lines.Count > 0 ? "\n" + line : line,
//         });
//         Lines.Add(ParseLine(line));
//     }
//
//     public override void InsertLine(int index, string line)
//     {
//         IncrementVersion();
//         _changes.Add(new TextDocumentContentChangeEvent
//         {
//             range = new Range
//             {
//                 start = new Position { line = index, character = 0 },
//                 end = new Position { line = index, character = 0 }
//             },
//             text = line + "\n"
//         });
//         Lines.Insert(index, ParseLine(line));
//     }
//
//     public override void RemoveLine(int index)
//     {
//         IncrementVersion();
//         _changes.Add(new TextDocumentContentChangeEvent
//         {
//             range = new Range
//             {
//                 start = new Position { line = index, character = 0 },
//                 end = new Position { line = index + 1, character = 0 }
//             },
//             text = ""
//         });
//         Lines.RemoveAt(index);
//     }
//
//     public override void ResetCode(string code)
//     {
//         IncrementVersion();
//         _changes.Add(new TextDocumentContentChangeEvent
//         {
//             range = new Range
//             {
//                 start = new Position { line = 0, character = 0 },
//                 end = new Position { line = Lines.Count, character = 0 }
//             },
//             text = code
//         });
//         var lines = code.Split('\n');
//         Lines.Clear();
//         foreach (var line in lines)
//             AppendLine(line);
//         OnCodeChanged();
//     }
//     // public void Dispose()
//     // {
//     // L.Info("Disposing LSP Formatter and closing document");
//     // LspClient.CloseDocument(Identifier.uri);
//     // }
// }
