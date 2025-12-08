using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Cysharp.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using StationeersIC10Editor;

namespace ImGuiEditor.LSP;

public class LspClient
{
    private int _nextId = 0;

    private readonly ConcurrentDictionary<int, TaskCompletionSource<JToken>> _pendingRequests =
        new ConcurrentDictionary<int, TaskCompletionSource<JToken>>();

    protected readonly CancellationTokenSource _cts = new CancellationTokenSource();
    protected readonly ManualResetEvent _isInitialized = new ManualResetEvent(false);
    protected object _sendLock = new object();
    protected Stream _lspInputStream = null;
    protected Stream _lspOutputStream = null;

    public Action<string> OnError = (string msg) => { };
    public Action<string> OnInfo = (string msg) => { };
    public Action<PublishDiagnosticsParams> OnDiagnostics = (PublishDiagnosticsParams msg) => { };

    public bool IsInitialized => _isInitialized.WaitOne(0);

    public LspClient() { }

    // this should be called when the streams are ready
    protected void Init(Stream inputStream, Stream outputStream)
    {
        _lspInputStream = inputStream;
        _lspOutputStream = outputStream;

        UniTask.RunOnThreadPool(() => ReadLoopAsync());
        UniTask.RunOnThreadPool(async () =>
        {
            try
            {
                await InitializeAsync("memory://");
            }
            catch (Exception ex)
            {
                OnError("LSP initialization error: " + ex);
            }
        });

    }

    private async UniTask<JToken> InitializeAsync(string rootUri)
    {
        var initParams = new
        {
            processId = (int?)null,
            rootUri = rootUri,

            initializationOptions = new { },

            capabilities = new
            {
                workspace = new
                {
                    workspaceFolders = true
                },
                textDocument = new
                {
                    synchronization = new
                    {
                        didSave = true,
                        willSave = false,
                        willSaveWaitUntil = false,
                        dynamicRegistration = false
                    },
                    completion = new
                    {
                        completionItem = new
                        {
                            snippetSupport = false
                        }
                    },
                    publishDiagnostics = new
                    {
                        relatedInformation = true,
                        versionSupport = true
                    }
                }
            },

            workspaceFolders = new[]
    {
        new { uri = rootUri, name = "IC10Workspace" }
    }
        };


        var result = await SendRequestAsync("initialize", initParams, true);
        await SendNotificationAsync("initialized", new { }, true);

        OnInfo("LSP server initialized.");
        OnInfo("Server info:\n" + result.ToString(Formatting.None));

        var caps = result["capabilities"];
        if (caps != null)
            OnInfo("Server capabilities:\n" + caps.ToString(Formatting.None));

        _isInitialized.Set();
        L.Info("LSP Client is initialized.");

        return result;
    }

    protected async UniTask SendMessageAsync(string json, bool ignoreInitialized = false)
    {

        if (!ignoreInitialized)
        {
            await UniTask.SwitchToThreadPool();
            _isInitialized.WaitOne();
        }

        if (_lspInputStream == null)
            throw new InvalidOperationException("Server not started.");

        var bytes = Encoding.UTF8.GetBytes(json);
        var header = string.Format("Content-Length: {0}\r\n\r\n", bytes.Length);

        L.Info("LSP Sending Message:\n" + header + json);

        var msgBuffer = new byte[header.Length + bytes.Length];
        Array.Copy(Encoding.ASCII.GetBytes(header), 0, msgBuffer, 0, header.Length);
        Array.Copy(bytes, 0, msgBuffer, header.Length, bytes.Length);

        lock (_sendLock)
        {
            _lspInputStream.Write(msgBuffer, 0, msgBuffer.Length);
            _lspInputStream.Flush();
        }
    }

    public UniTask SendNotificationAsync(string method, object @params, bool ignoreInitialized = false)
    {
        var json = JsonConvert.SerializeObject(new
        {
            jsonrpc = "2.0",
            method = method,
            @params = @params
        });

        return SendMessageAsync(json, ignoreInitialized);
    }

    public Task<JToken> SendRequestAsync(string method, object @params, bool ignoreInitialized = false)
    {
        var id = Interlocked.Increment(ref _nextId);

        var tcs = new TaskCompletionSource<JToken>();
        _pendingRequests[id] = tcs;

        var json = JsonConvert.SerializeObject(new
        {
            jsonrpc = "2.0",
            id = id,
            method = method,
            @params = @params
        });

        // Fire and forget (errors are surfaced via tcs / read loop)
        Task.Run(() => SendMessageAsync(json, ignoreInitialized));

        return tcs.Task;
    }

    private readonly List<byte> _recvBuffer = new List<byte>();

    private async Task ReadLoopAsync()
    {
        var stream = _lspOutputStream;
        var temp = new byte[8192];

        while (!_cts.IsCancellationRequested)
        {
            int read = await stream.ReadAsync(temp, 0, temp.Length, _cts.Token);
            if (read == 0)
                break;

            for (int i = 0; i < read; i++)
                _recvBuffer.Add(temp[i]);

            while (true)
            {
                int headerEnd = IndexOfHeaderEnd(_recvBuffer);
                if (headerEnd < 0)
                    break; // need more data

                string headerText = Encoding.ASCII.GetString(_recvBuffer.Take(headerEnd).ToArray());
                int contentLength = ParseContentLength(headerText);
                if (contentLength <= 0)
                {
                    L.Error("[LSP] Invalid header:\n" + headerText);
                    _recvBuffer.Clear();
                    break;
                }

                int fullLen = headerEnd + 4 + contentLength;
                if (_recvBuffer.Count < fullLen)
                    break; // body not fully received yet

                byte[] bodyBytes = _recvBuffer.Skip(headerEnd + 4).Take(contentLength).ToArray();
                _recvBuffer.RemoveRange(0, fullLen);

                string msg = Encoding.UTF8.GetString(bodyBytes, 0, contentLength);
                // L.Info($"LSP Message Body: |{msg}|");
                HandleIncomingMessage(msg);
            }
        }
    }

    private static int IndexOfHeaderEnd(List<byte> buffer)
    {
        // look for "\r\n\r\n" in bytes
        for (int i = 0; i <= buffer.Count - 4; i++)
        {
            if (buffer[i] == (byte)'\r' &&
                buffer[i + 1] == (byte)'\n' &&
                buffer[i + 2] == (byte)'\r' &&
                buffer[i + 3] == (byte)'\n')
            {
                return i;
            }
        }
        return -1;
    }

    private static int ParseContentLength(string headerText)
    {
        var lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(new[] { ':' }, 2);
                if (parts.Length == 2)
                {
                    if (int.TryParse(parts[1].Trim(), out int len))
                        return len;
                }
            }
        }
        return 0;
    }

    private void HandleIncomingMessage(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;
        L.Info("LSP Received Message:\n" + json);
        JObject root;

        try
        {
            root = JObject.Parse(json);
        }
        catch (Exception ex)
        {
            OnError("Failed to parse LSP message: " + ex);
            return;
        }

        JToken idToken = root["id"];

        if (idToken != null)
        {
            // Response or server request with id
            JToken resultToken = root["result"];
            JToken errorToken = root["error"];

            if (resultToken != null)
            {
                int id;
                if (idToken.Type == JTokenType.Integer &&
                    (id = idToken.Value<int>()) != 0 &&
                    _pendingRequests.TryRemove(id, out var tcs))
                {
                    tcs.TrySetResult(resultToken);
                }
            }
            else if (errorToken != null)
            {
                int id;
                if (idToken.Type == JTokenType.Integer &&
                    (id = idToken.Value<int>()) != 0 &&
                    _pendingRequests.TryRemove(id, out var tcs))
                {
                    tcs.TrySetException(
                        new Exception("LSP error: " + errorToken.ToString(Formatting.None)));
                }
            }
            else if (root["method"] != null)
            {
                OnInfo("Server request: " + root["method"]);
                // You could handle server-initiated requests here
            }
        }
        else if (root["method"] != null)
        {
            // Notification
            var notifMethod = (string)root["method"];
            L.Info("LSP Notification Method: " + notifMethod);
            if (notifMethod == "textDocument/publishDiagnostics")
            {
                L.Info("LSP Received Diagnostics Notification.");
                var diagParams = root["params"].ToObject<PublishDiagnosticsParams>();
                OnDiagnostics(diagParams);
                return;
            }
            // OnInfo("Server notification: " + notifMethod + root);
        }
    }

    public async Task<JToken> RequestDiagnostics(VersionedTextDocumentIdentifier identifier)
    {
        var diagnosticsParam = new DocumentDiagnosticParams
        {
            textDocument = identifier
        };

        return await SendRequestAsync("textDocument/diagnostic", diagnosticsParam);
    }


    public async void OpenDocument(string uri, string languageId, string text)
    {
        var openParams = new DidOpenTextDocumentParams
        {
            textDocument = new TextDocumentItem
            {
                uri = uri,
                languageId = languageId,
                version = 1,
                text = text
            }
        };

        await SendNotificationAsync("textDocument/didOpen", openParams);
    }

    public async void CloseDocument(string uri)
    {
        var closeParams = new DidCloseTextDocumentParams
        {
            textDocument = new TextDocumentIdentifier
            {
                uri = uri
            }
        };

        await SendNotificationAsync("textDocument/didClose", closeParams);
    }

    public async void ChangeDocument(VersionedTextDocumentIdentifier identifier, TextDocumentContentChangeEvent[] changes)
    {
        L.Info($"LSP Changing document {identifier.uri} to version {identifier.version} with {changes.Length} changes.");
        var changeParams = new DidChangeTextDocumentParams
        {
            textDocument = identifier,
            contentChanges = changes,
        };

        await SendNotificationAsync("textDocument/didChange", changeParams);
    }

    public async void ChangeDocumentFull(VersionedTextDocumentIdentifier identifier, string newText)
    {
        var changes = new TextDocumentContentChangeEvent[]
        {
            new TextDocumentContentChangeEvent
            {
                text = newText
            }
        };

        var @params = new
        {
            textDocument = identifier,
            contentChanges = changes
        };

        await SendNotificationAsync("textDocument/didChange", @params);
    }

    public async UniTask<List<SemanticToken>> RequestSemanticTokens(string uri)
    {
        var @params = new
        {
            textDocument = new TextDocumentIdentifier
            {
                uri = uri
            }
        };

        try
        {
            var result = (await SendRequestAsync("textDocument/semanticTokens/full", @params)).ToObject<SemanticTokens>();

            var tokens = new List<SemanticToken>();

            var prevLine = 0;
            var prevCol = 0;

            var colorMap = new uint[]
            {
                ICodeFormatter.ColorFromHTML("#FFFFFF"),
                ICodeFormatter.ColorFromHTML("#00FF00"),
                ICodeFormatter.ColorFromHTML("#AFAF00"),
                ICodeFormatter.ColorFromHTML("#FF8800"),
                ICodeFormatter.ColorFromHTML("#00FFFF"),
                ICodeFormatter.ColorFromHTML("#FF00FF"),
                ICodeFormatter.ColorFromHTML("#8888FF"),
                ICodeFormatter.ColorFromHTML("#FF0000"),
                ICodeFormatter.ColorFromHTML("#88FF88"),
            };

            var nColors = colorMap.Length;

            for (var i = 0; i < result.data.Length; i += 5)
            {
                if (result.data[i] > 0)
                    prevCol = 0;

                tokens.Add(new SemanticToken
                (
                    line: prevLine + result.data[i],
                    column: prevCol + result.data[i + 1],
                    length: result.data[i + 2],
                    type: (uint)result.data[i + 3],
                    style: colorMap[result.data[i + 3] % nColors]
                ));

                prevLine = tokens[tokens.Count - 1].Line;
                prevCol = tokens[tokens.Count - 1].Column;
            }

            for (var i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                OnInfo($"Token: Line {token.Line}, Col {token.Column}, Len {token.Length}, Type {token.Type}");
                // if(token.Type == 6)
                //     token.Color = 0xFFFF00FF; // Function - Blue

            }

            return tokens;

        }
        catch (Exception ex)
        {
            OnError("Error requesting semantic tokens: " + ex);
        }
        return new List<SemanticToken>();
    }



    public static LspClient StartPyrightLSPServer()
    {
        string SiteDir = Path.Combine(BepInEx.Paths.CachePath, "pytrapic", "venv", "site-packages");
        string NodeExe = Path.Combine(SiteDir, "nodejs_wheel", "node.exe");
        string PyRightJS = Path.Combine(SiteDir, "basedpyright", "langserver.index.js");
        string Args = "--no-warnings --title \"abc\" langserver.index.js --stdio";
        var startInfo = new ProcessStartInfo
        {
            FileName = NodeExe,
            Arguments = Args,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(PyRightJS)
        };

        startInfo.EnvironmentVariables["UV_THREADPOOL_SIZE"] = "1";
        startInfo.EnvironmentVariables["NODE_DISABLE_COLORS"] = "1";
        startInfo.EnvironmentVariables["UV_PROCESS_TITLE"] = "0";

        return new LspClientStdio(startInfo);
    }

}

class LspClientStdio : LspClient, IDisposable
{
    private Process _process;

    public LspClientStdio(ProcessStartInfo startInfo) : base()
    {
        StationeersIC10Editor.L.Info("Starting LSP server...");
        _process = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true
        };

        if (!_process.Start())
        {
            L.Info("Failed to start LSP server process.");
            throw new InvalidOperationException("Failed to start LSP server process.");
        }

        // check if it dies within a second
        _process.WaitForExit(1000);

        if (_process.HasExited)
        {
            L.Info("LSP server process exited immediately after starting.");
            L.Info($"Exit code: {_process.ExitCode}");
            L.Info($"Error output: {_process.StandardError.ReadToEnd()}");
            L.Info($"Output: {_process.StandardOutput.ReadToEnd()}");
            throw new InvalidOperationException("LSP server process exited immediately after starting.");
        }

        base.Init(_process.StandardInput.BaseStream, _process.StandardOutput.BaseStream);
    }

    public void Dispose()
    {
        try
        {
            _cts.Cancel();

            if (_process != null && !_process.HasExited)
            {
                _process.Kill();
                _process.Dispose();
            }
        }
        catch { }
    }
}

class LspClientSocket : LspClient, IDisposable
{
    private System.Net.Sockets.TcpClient _tcpClient;

    public LspClientSocket(string host, int port) : base()
    {
        StationeersIC10Editor.L.Info("Starting LSP server socket connection...");
        _tcpClient = new System.Net.Sockets.TcpClient();
        StartAsync(host, port).Forget();
    }

    public async UniTask StartAsync(string host, int port)
    {
        await _tcpClient.ConnectAsync(host, port);
        base.Init(_tcpClient.GetStream(), _tcpClient.GetStream());
    }

    public void Dispose()
    {
        try
        {
            _cts.Cancel();

            if (_tcpClient != null)
            {
                _tcpClient.Close();
                _tcpClient.Dispose();
            }
        }
        catch { }
    }
}

