namespace StationeersIC10Editor
{
    using Assets.Scripts.Networking.Transports;
    using Assets.Scripts.Networking;
    using Assets.Scripts.Objects.Motherboards;
    using Assets.Scripts.UI;
    using Assets.Scripts;
    using BepInEx.Configuration;
    using Cysharp.Threading.Tasks;
    using ImGuiNET;
    using System.Collections.Generic;
    using System.Linq;
    using System;
    using UnityEngine;

    using static Utils;
    using static Settings;

    public class EditorState
    {
        public string Code;
        public TextPosition CaretPos;
        public double Timestamp;
        public bool Mergeable;
    }

    public enum MoveToken
    {
        Char,
        Line,
        WordBeginning,
        WordEnd,
    }

    public struct MoveAction
    {
        public MoveToken Token;
        public bool Forward;
        public uint Amount;

        public int Direction => Forward ? 1 : -1;
        public int SignedAmount => (int)(Direction * Amount);


        public MoveAction(MoveToken token = MoveToken.Char, bool forward = true, uint amount = 0)
        {
            Token = token;
            Forward = forward;
            Amount = amount;
        }
    }

    public static class Utils
    {
        public static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || c == '$' || c == '-';
        }


    }

    public static class Settings
    {
        public static bool VimEnabled => IC10EditorPlugin.VimBindings.Value;
        public static bool EnforceLineLimit => IC10EditorPlugin.EnforceLineLimit.Value;
        public static bool EnforceByteLimit => IC10EditorPlugin.EnforceByteLimit.Value;
        public static bool PauseOnOpen => IC10EditorPlugin.PauseOnOpen.Value;
        public static float TooltipDelay => IC10EditorPlugin.TooltipDelay.Value;
        public static float Scale => Mathf.Clamp(IC10EditorPlugin.ScaleFactor.Value, 0.25f, 5.0f);
        public static bool EnableAutoComplete => IC10EditorPlugin.EnableAutoComplete.Value;

        public static Vector2 buttonSize => Scale * new Vector2(85, 0);
        public static Vector2 smallButtonSize => Scale * new Vector2(50, 0);

        public const string LimitExceededMessage = "Size limit exceeded: cannot save or export.";
    }

    public class IEditor
    {
        public object Target;
        public string Title = "Motherboard";
        public ProgrammableChipMotherboard PCM => Target as ProgrammableChipMotherboard;
        public InstructionData InstructionData => Target as InstructionData;
        public bool LimitExceeded => (EnforceLineLimit && Lines.Count > 128) || (EnforceByteLimit && Code.Length > 4096);

        public bool HaveSelection => (bool)Selection;
        public KeyHandler KeyHandler;

        public TextPosition _caretPos;

        public int ScrollToCaret = 0;
        protected double _timeLastAction = 0.0;

        public double TimeLastAction => _timeLastAction;
        public KeyMode KeyMode => KeyHandler.Mode;

        public LinkedList<EditorState> UndoList;
        public LinkedList<EditorState> RedoList;
        public ICodeFormatter CodeFormatter;
        public List<string> Lines;
        public string Code => string.Join("\n", Lines);
        public string CommandStatus = "";

        public IEditor(KeyHandler keyHandler, object target = null)
        {
            Target = target;
            KeyHandler = keyHandler;
            CodeFormatter = CodeFormatters.GetFormatter("IC10");
            UndoList = new LinkedList<EditorState>();
            RedoList = new LinkedList<EditorState>();
            Lines = new List<string>();
            Lines.Add("");
            CaretPos = new TextPosition(0, 0);
            CodeFormatter.AppendLine("");
        }

        public TextPosition CaretPos
        {
            get { return _caretPos; }
            set
            {
                _caretPos = value;
                if (_caretPos.Line < 0)
                    _caretPos.Line = 0;
                if (_caretPos.Line >= Lines.Count)
                    _caretPos.Line = Lines.Count - 1;
                if (_caretPos.Col < 0)
                    _caretPos.Col = 0;
                if (_caretPos.Col > Lines[_caretPos.Line].Length)
                    _caretPos.Col = Lines[_caretPos.Line].Length;
                ScrollToCaret += 1;
                _timeLastAction = ImGui.GetTime();
            }
        }

        public int CaretLine
        {
            get { return _caretPos.Line; }
            set { CaretPos = new TextPosition(value, _caretPos.Col); }
        }

        public int CaretCol
        {
            get { return CaretPos.Col; }
            set { CaretPos = new TextPosition(_caretPos.Line, value); }
        }

        public TextRange Selection;

        public string CurrentLine
        {
            get
            {
                return Lines[CaretLine];
            }

            set
            {
                if (value == Lines[CaretLine])
                    return;

                ReplaceLine(CaretLine, value);
            }
        }
        public EditorState State
        {
            get
            {
                return new EditorState { Code = Code, CaretPos = CaretPos, Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
            }

            set
            {
                ResetCode(value.Code, false);
                CaretPos = value.CaretPos;
            }
        }

        public void PushUndoState(bool merge = true)
        {
            while (UndoList.Count > 100)
                UndoList.RemoveLast();

            var state = State;
            state.Mergeable = merge;

            if (string.IsNullOrEmpty(state.Code))
                return;

            if (UndoList.Count > 0)
            {
                var first = UndoList.First.Value;
                if (state.Code == first.Code)
                {
                    first.CaretPos = state.CaretPos;
                    return;
                }

                // merge with previous state if within 500ms or same code
                // merging does not happen accross "large" changes (e.g. paste, cut, delete selection etc.)
                if (merge && first.Mergeable && state.Timestamp < first.Timestamp + 500)
                    UndoList.RemoveFirst();
            }

            UndoList.AddFirst(state);
        }

        public void Undo()
        {
            if (UndoList.Count > 0)
            {
                RedoList.AddFirst(State);
                State = UndoList.First.Value;
                UndoList.RemoveFirst();
            }
        }

        public void Redo()
        {
            if (RedoList.Count > 0)
            {
                UndoList.AddFirst(State);
                State = RedoList.First.Value;
                RedoList.RemoveFirst();
            }
        }

        public void RemoveLine(int lineIndex)
        {
            if (lineIndex < 0 || lineIndex >= Lines.Count)
                return;

            CodeFormatter.RemoveLine(lineIndex);
            Lines.RemoveAt(lineIndex);
        }

        public void ReplaceLine(int lineIndex, string newLine)
        {
            if (lineIndex < 0 || lineIndex >= Lines.Count)
                return;

            CodeFormatter.ReplaceLine(lineIndex, newLine);
            Lines[lineIndex] = newLine;
        }


        public bool IsWordBeginning(TextPosition pos)
        {
            if (pos.Col == 0)
                return true;

            var leftPos = new TextPosition(pos.Line, pos.Col - 1);
            return !IsWordChar(this[leftPos]) && IsWordChar(this[pos]);
        }

        public bool IsWordEnd(TextPosition pos)
        {
            if (pos.Col == 0)
                return false;

            var leftPos = new TextPosition(pos.Line, pos.Col - 1);

            return IsWordChar(this[leftPos]) && !IsWordChar(this[pos]);
        }

        public TextPosition FindWordBeginning(TextPosition pos, bool forward)
        {
            int dir = forward ? 1 : -1;
            pos.Col += dir;
            pos = WrapPos(pos);
            while (!IsWordBeginning(pos))
            {
                pos.Col += dir;
                pos = WrapPos(pos);
                if (pos.Line == Lines.Count - 1 && pos.Col == Lines[pos.Line].Length)
                    break;
            }
            return pos;
        }

        public TextPosition FindWordEnd(TextPosition pos, bool forward)
        {
            int dir = forward ? 1 : -1;
            pos.Col += dir;
            pos = WrapPos(pos);
            while (!IsWordEnd(pos))
            {
                pos.Col++;
                pos = WrapPos(pos);
                if (pos.Line == 0 && pos.Col == 0)
                    break;
            }
            return pos;
        }

        public TextPosition WrapPos(TextPosition pos)
        {
            if (pos.Col < 0 && pos.Line > 0)
            {
                pos.Line--;
                pos.Col = Lines[pos.Line].Length;
            }
            if (pos.Col > Lines[pos.Line].Length && pos.Line < Lines.Count)
            {
                pos.Col = 0;
                pos.Line++;
            }

            if (pos.Line < 0)
                pos.Line = 0;
            if (pos.Line >= Lines.Count)
                pos.Line = Lines.Count - 1;

            if (pos.Col < 0)
                pos.Col = 0;
            if (pos.Col > Lines[pos.Line].Length)
                pos.Col = Lines[pos.Line].Length;

            return pos;
        }

        public TextPosition MoveLines(TextPosition pos, int amount)
        {
            pos.Line += amount;
            pos.Line = Math.Max(0, Math.Min(pos.Line, Lines.Count - 1));
            return pos;
        }

        public TextPosition MoveChars(TextPosition startPos, int amount)
        {
            int dir = amount >= 0 ? 1 : -1;
            amount = Math.Abs(amount);
            TextPosition pos = startPos;
            for (int i = 0; i < amount; i++)
            {
                pos.Col += dir;
                pos = WrapPos(pos);
            }

            return pos;
        }

        public TextPosition FindWhitespace(TextPosition pos, bool forward = true)
        {
            // Move to the next whitespace or next line if there is none in this line
            string line = Lines[pos.Line];
            int dir = forward ? 1 : -1;
            while (pos.Col < line.Length && pos.Col >= 0 && !char.IsWhiteSpace(line[pos.Col]))
            {
                pos.Col += dir;
                if (pos.Col < 0)
                    return WrapPos(pos);
            }

            return pos;
        }

        public TextPosition FindNonWhitespace(TextPosition pos, bool forward = true)
        {
            if (!char.IsWhiteSpace(this[pos]))
                return pos;

            int dir = forward ? 1 : -1;
            string line = Lines[pos.Line];

            while (pos.Col < line.Length && pos.Col >= 0 && char.IsWhiteSpace(this[pos]))
                pos.Col += dir;

            pos = WrapPos(pos);
            return pos;
        }

        public TextPosition FindNextWord(TextPosition startPos, bool forward = true)
        {
            TextPosition pos = startPos;
            if (char.IsWhiteSpace(this[pos]))
                return FindNonWhitespace(pos, forward);

            pos = FindWhitespace(pos, forward);
            return FindNonWhitespace(pos, forward);
        }

        public TextPosition FindString(TextPosition startPos, string searchTerm, bool forward = true)
        {
            if (forward)
                return FindStringForward(startPos, searchTerm);
            else
                return FindStringBackward(startPos, searchTerm);
        }

        public TextPosition FindStringForward(TextPosition startPos, string searchTerm)
        {
            int lineIndex = startPos.Line;
            int colIndex = startPos.Col + 1;

            while (lineIndex < Lines.Count)
            {
                string line = Lines[lineIndex];
                int foundIndex = line.IndexOf(searchTerm, colIndex, StringComparison.Ordinal);
                if (foundIndex != -1)
                    return new TextPosition(lineIndex, foundIndex);

                lineIndex++;
                colIndex = 0;
            }

            return new TextPosition(-1, -1);

        }

        private TextPosition FindStringBackward(TextPosition startPos, string searchTerm)
        {
            int lineIndex = startPos.Line;
            int colIndex = startPos.Col - 1;

            while (lineIndex >= 0)
            {
                string line = Lines[lineIndex];

                if (colIndex >= line.Length) colIndex = line.Length - 1;
                if (colIndex < 0)
                {
                    lineIndex--;
                    if (lineIndex >= 0)
                        colIndex = Lines[lineIndex].Length - 1;
                    continue;
                }

                int foundIndex = line.LastIndexOf(searchTerm, colIndex, StringComparison.Ordinal);

                if (foundIndex != -1)
                    return new TextPosition(lineIndex, foundIndex);

                lineIndex--;
                if (lineIndex >= 0)
                    colIndex = Lines[lineIndex].Length - 1;
            }

            return new TextPosition(-1, -1);
        }


        public char this[TextPosition pos]
        {
            get
            {
                var line = Lines[pos.Line];
                if (pos.Col == line.Length)
                    return '\n';
                return line[pos.Col];
            }
        }

        public void CaretToEndOfLine()
        {
            CaretCol = Lines[CaretLine].Length;
        }

        public void CaretToStartOfLine()
        {
            CaretCol = 0;
        }

        public void CaretUp(int numLines = 1)
        {
            MoveCaret(0, -numLines, true);
        }

        public void CaretDown(int numLines = 1)
        {
            MoveCaret(0, numLines, true);
        }

        public void CaretLeft(int numCols = 1)
        {
            MoveCaret(-numCols, 0, true);
        }

        public void CaretRight(int numCols = 1)
        {
            MoveCaret(numCols, 0, true);
        }

        public void MoveCaret(
            int horizontal = 0,
            int vertical = 0,
            bool isRelative = true,
            bool isSelecting = false)
        {
            Selection.Reset();
            TextPosition newPos = CaretPos;
            if (isRelative)
            {
                newPos.Line += vertical;
                newPos.Col += horizontal;
            }
            else
            {
                newPos.Line = vertical;
                newPos.Col = horizontal;
            }

            if (newPos.Line < 0)
                newPos.Line = 0;

            if (newPos.Line >= Lines.Count)
                newPos.Line = Lines.Count - 1;

            if (newPos.Col < 0)
                newPos.Col = 0;

            if (newPos.Col > Lines[newPos.Line].Length)
                newPos.Col = Lines[newPos.Line].Length;

            if (CaretPos == newPos)
                return;

            if (isSelecting)
                Selection.End = newPos;
            else
                Selection.Reset();

            CaretPos = newPos;
        }

        public void SelectAll()
        {
            Selection.Start = new TextPosition(0, 0);
            Selection.End = new TextPosition(Lines.Count - 1, Lines[Lines.Count - 1].Length);
        }

        public void Cut()
        {
            if (!HaveSelection)
                return;
            GameManager.Clipboard = SelectedCode;
            DeleteSelectedCode();
        }

        public void CopyRange(TextRange range)
        {
            string code = GetCode(range);
            if (code != null)
            {
                GameManager.Clipboard = code;
            }
        }

        public void Copy()
        {
            CopyRange(Selection.Sorted());
        }

        public void Paste()
        {
            if (!DeleteSelectedCode())
                PushUndoState(false);
            Insert(GameManager.Clipboard);
        }

        public string GetCode(TextRange range)
        {
            L.Info("GetCode called with range: " + range);
            var start = range.Start;
            var end = range.End;
            var suffix = "";

            if (end.Col > Lines[end.Line].Length)
            {
                end.Col = Lines[end.Line].Length;
                suffix = "\n";
            }

            if (start.Line == end.Line)
                return Lines[start.Line].Substring(start.Col, end.Col - start.Col) + suffix;

            string code = Lines[start.Line].Substring(start.Col);
            for (int i = start.Line + 1; i < end.Line; i++)
                code += '\n' + Lines[i];

            code += '\n' + Lines[end.Line].Substring(0, end.Col);
            return code + suffix;
        }

        public string SelectedCode => GetCode(Selection.Sorted());

        public TextPosition Clamp(TextPosition pos)
        {
            if (pos.Line < 0)
            {
                pos.Line = 0;
                pos.Col = 0;
            }
            else if (pos.Line >= Lines.Count)
            {
                pos.Line = Lines.Count - 1;
                pos.Col = Lines[pos.Line].Length;
            }
            else if (pos.Col < 0)
                pos.Col = 0;
            else if (pos.Col > Lines[pos.Line].Length)
                pos.Col = Lines[pos.Line].Length;
            return pos;
        }

        public TextRange Clamp(TextRange range)
        {
            range.Start = Clamp(range.Start);
            range.End = Clamp(range.End);
            return range;
        }

        public bool DeleteRange(TextRange range, bool pushUndo = true)
        {
            if (!(bool)range)
                return false;

            range = range.Sorted();
            bool removeLast = range.End.Col > Lines[range.End.Line].Length;
            range = Clamp(range);

            if (pushUndo)
                PushUndoState(false);

            var start = range.Start;
            var end = range.End;

            if (start.Line == end.Line)
            {
                if (start.Col == 0 && removeLast)
                    RemoveLine(start.Line);
                else
                {
                    string line = Lines[start.Line];
                    string newLine =
                        line.Substring(0, start.Col) + line.Substring(end.Col, line.Length - end.Col);
                    ReplaceLine(start.Line, newLine);

                }
            }
            else
            {
                string firstLine = Lines[start.Line];
                string lastLine = Lines[end.Line];
                string newFirstLine = firstLine.Substring(0, start.Col);
                string newLastLine = lastLine.Substring(end.Col, lastLine.Length - end.Col);
                ReplaceLine(start.Line, newFirstLine + newLastLine);

                for (int i = end.Line; i > start.Line; i--)
                {
                    CodeFormatter.RemoveLine(i);
                    Lines.RemoveAt(i);
                }
                if (removeLast)
                    RemoveLine(start.Line);
            }

            CaretPos = start;
            L.Info($"CaretPos after delete: {CaretPos.Line},{CaretPos.Col}");
            return true;
        }

        public bool DeleteSelectedCode()
        {
            if (DeleteRange(Selection))
            {
                Selection.Reset();
                return true;
            }

            return false;
        }

        public TextRange GetWordAt(TextPosition pos)
        {
            bool isWordChar = IsWordChar(this[pos]);
            bool IsWordBeginning = pos.Col == 0 || (isWordChar && !IsWordChar(this[new TextPosition(pos.Line, pos.Col - 1)]));

            var startPos = IsWordBeginning ? pos : FindWordBeginning(pos, !isWordChar);
            var endPos = FindWordEnd(pos, isWordChar);

            return new TextRange(startPos, endPos);
        }

        public void ClearCode(bool pushUndo = true)
        {
            if (pushUndo)
                PushUndoState(false);
            Lines.Clear();
            Lines.Add(string.Empty);
            CodeFormatter.ResetCode(string.Empty);
            CaretLine = 0;
            CaretCol = 0;
            Selection.Reset();
        }

        public void Insert(string code)
        {
            code = code.Replace("\r", string.Empty);
            if (string.IsNullOrEmpty(code))
                return;
            var newLines = new List<string>(code.Split('\n'));
            if (newLines.Count == 0)
                return;

            // CodeFormatter.RemoveLine(CaretLine);
            string beforeCaret = CurrentLine.Substring(0, CaretCol);
            string afterCaret = CurrentLine.Substring(CaretCol, CurrentLine.Length - CaretCol);
            L.Info($"Inserting code at {CaretLine},{CaretCol}: '{beforeCaret}|{afterCaret}'");
            L.Info($"{newLines.Count} new lines: {newLines}");
            L.Info($"starts with newline {code.StartsWith("\n")}, ends with newline {code.EndsWith("\n")}");
            if (newLines.Count == 1)
            {
                CurrentLine = beforeCaret + newLines[0] + afterCaret;
                CaretCol = beforeCaret.Length + newLines[0].Length;
                return;
            }
            CurrentLine = beforeCaret + newLines[0];
            newLines.RemoveAt(0);
            int newCaretCol = newLines[newLines.Count - 1].Length;
            newLines[newLines.Count - 1] += afterCaret;
            Lines.InsertRange(CaretLine + 1, newLines);
            for (var j = 0; j < newLines.Count; j++)
                CodeFormatter.InsertLine(CaretLine + 1 + j, newLines[j]);

            CaretPos = new TextPosition(
                CaretLine + newLines.Count,
                newCaretCol);
        }

        public TextPosition Move(TextPosition startPos, MoveAction action)
        {
            if (action.Amount == 0)
                return startPos;

            if (action.Token == MoveToken.Char)
                return MoveChars(startPos, action.SignedAmount);

            if (action.Token == MoveToken.Line)
            {
                var newLine = startPos.Line + action.SignedAmount;
                if (newLine < 0)
                    newLine = 0;
                if (newLine >= Lines.Count)
                    newLine = Lines.Count - 1;
                return new TextPosition(newLine, startPos.Col);
            }

            if (action.Token == MoveToken.WordBeginning)
            {
                var pos = startPos;
                for (int i = 0; i < action.Amount; i++)
                    pos = FindWordBeginning(pos, action.Forward);
                return pos;
            }
            if (action.Token == MoveToken.WordEnd)
            {
                var pos = startPos;
                for (int i = 0; i < action.Amount; i++)
                    pos = FindWordEnd(pos, action.Forward);
                return pos;
            }

            throw new NotImplementedException($"Move not implemented for token {action.Token}");
        }

        public void ResetCode(string code, bool pushUndo = true)
        {
            code = code.Replace("\r", string.Empty);
            ClearCode(pushUndo);
            Lines.Clear();
            var lines = code.Split('\n');
            CodeFormatter.ResetCode(code);
            foreach (var line in lines)
                Lines.Add(line);
            CaretPos = new TextPosition(0, 0);
            if (pushUndo)
                PushUndoState(false);
        }

        public string Save()
        {
            if (PCM)
            {
                if (LimitExceeded)
                {
                    CommandStatus = LimitExceededMessage;
                    return "Cannot save file, limits exceeded.";
                }
                PCM.InputFinished(Code);
                return "Saved file";
            }
            if (InstructionData != null)
            {
                InstructionData.Instructions = Code;
                InstructionData.SaveToFile(InstructionData.DirectoryPath);
                return $"Saved to Library {InstructionData.Title}";
            }
            return "Error: No target to save to.";
        }
    }

    public class EditorWindow
    {

        IEditor Editor;
        public KeyMode KeyMode;
        public static bool UseNativeEditor = false;
        KeyHandler KeyHandler;

        public List<IEditor> Tabs = new List<IEditor>();

        private int _activeTabIndex = 0;
        public IEditor ActiveTab => Tabs[_activeTabIndex];
        public IEditor MotherboardTab => Tabs[0];

        public List<string> Lines => ActiveTab.Lines;
        public string Code => ActiveTab.Code;
        public int CaretLine => ActiveTab.CaretLine;
        public int CaretCol => ActiveTab.CaretCol;
        public TextPosition CaretPos => ActiveTab.CaretPos;

        public ICodeFormatter CodeFormatter => ActiveTab.CodeFormatter;
        public TextRange Selection => ActiveTab.Selection;

        bool LimitExceeded => ActiveTab.LimitExceeded;
        string CommandStatus => ActiveTab.CommandStatus;

        private string Title = "IC10 Editor";

        public EditorWindow(ProgrammableChipMotherboard pcm)
        {
            KeyHandler = new KeyHandler(this);
            Editor = new IEditor(KeyHandler, pcm);

            Tabs.Add(Editor);
        }

        private bool Show = false;

        private Dictionary<string, InstructionData> _libraryCodes = new Dictionary<string, InstructionData>();
        private List<InstructionData> _librarySearchResults = new List<InstructionData>();

        private bool _librarySearchVisible = false;
        private string _librarySearchText = "";


        private bool _librarySearchJustOpened = false;
        private int _librarySelectedIndex = -1;

        public async UniTaskVoid LoadLibraries()
        {

            var items = await NetworkManager.GetLocalAndWorkshopItems(SteamTransport.WorkshopType.ICCode);

            var libs = new Dictionary<string, InstructionData>();

            foreach (var item in items)
            {
                InstructionData data = InstructionData.GetFromFile(item.FilePathFullName);
                data.ItemWrapper = item;
                libs.Add(data.Title, data);
            }

            await UniTask.SwitchToMainThread();
            _libraryCodes = libs;

            if (_librarySearchVisible)
                PerformLibrarySearch(_librarySearchText);
        }

        public void ShowLibrarySearch()
        {
            _librarySearchVisible = true;
            _librarySearchJustOpened = true;

            ImGui.OpenPopup("Library Search");

            LoadLibraries().Forget();
        }

        public void DrawLibrarySearchWindow()
        {
            if (!_librarySearchVisible)
                return;

            bool open = true;
            if (ImGui.BeginPopupModal("Library Search", ref open, ImGuiWindowFlags.AlwaysAutoResize))
            {
                if (_librarySearchJustOpened)
                {
                    ImGui.SetKeyboardFocusHere();
                    _librarySearchJustOpened = false;
                }

                ImGui.Text("Search libraries:");
                ImGui.SameLine();
                string oldSearchText = _librarySearchText;
                ImGui.InputText("##LibrarySearch", ref _librarySearchText, 256, ImGuiInputTextFlags.EnterReturnsTrue);
                if (oldSearchText != _librarySearchText)
                    PerformLibrarySearch(_librarySearchText);

                // Search if Enter pressed or text changed
                if (ImGui.IsItemDeactivatedAfterEdit() || ImGui.IsItemFocused() && ImGui.IsKeyPressed(ImGuiKey.Enter))
                {
                    if (_librarySearchResults.Count > 0)
                    {
                        LoadLibraryEntry(_librarySearchResults[0]);
                        _librarySearchVisible = false;
                        ImGui.CloseCurrentPopup();
                    }
                }

                ImGui.SameLine();

                if (ImGui.Button("Create new"))
                {
                    var editor = new IEditor(KeyHandler);
                    editor.Title = _librarySearchText;
                    editor.ResetCode("");
                    Tabs.Add(editor);
                    _activeTabIndex = Tabs.Count - 1;
                    _librarySearchVisible = false;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.Separator();

                // Show results
                if (_librarySearchResults.Count == 0)
                {
                    ImGui.TextColored(new Vector4(0.7f, 0.7f, 0.7f, 1), "No results found.");
                }
                else
                {
                    ImGui.BeginChild("LibrarySearchResults", new Vector2(500, 400), true);
                    for (int i = 0; i < _librarySearchResults.Count; i++)
                    {
                        var lib = _librarySearchResults[i];

                        var entryLabel = "";
                        if (lib.WorkshopFileHandle != 0)
                            entryLabel = $"{lib.Title} by {lib.Author} (workshop)";
                        else
                            entryLabel = $"{lib.Title} by {lib.Author} (local)";
                        if (ImGui.Selectable(entryLabel, _librarySelectedIndex == i))
                            _librarySelectedIndex = i;

                        // Tooltip showing first 20 lines
                        if (ImGui.IsItemHovered())
                        {
                            ImGui.BeginTooltip();
                            var preview = GetLibraryPreview(lib);
                            ImGui.TextUnformatted(preview);
                            ImGui.EndTooltip();
                        }

                        // Double-click to load
                        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                        {
                            LoadLibraryEntry(lib);
                            _librarySearchVisible = false;
                            ImGui.CloseCurrentPopup();
                        }
                    }
                    ImGui.EndChild();
                }

                ImGui.Separator();
                if (ImGui.Button("Close"))
                {
                    _librarySearchVisible = false;
                    ImGui.CloseCurrentPopup();
                }

                ImGui.EndPopup();
            }

            if (!open)
            {
                _librarySearchVisible = false;
            }
        }

        private void PerformLibrarySearch(string query)
        {
            _librarySearchResults.Clear();

            // if (string.IsNullOrWhiteSpace(query))
            // return;

            string q = query.Trim().ToLowerInvariant();

            foreach (var kvp in _libraryCodes)
            {
                if (string.IsNullOrEmpty(q) || kvp.Key.ToLowerInvariant().Contains(q) ||
                    kvp.Value.Instructions.ToLowerInvariant().Contains(q))
                {
                    _librarySearchResults.Add(kvp.Value);
                }
            }
        }

        private string GetLibraryPreview(InstructionData lib)
        {
            if (lib?.Instructions == null)
                return "";

            var header = $"Title: {lib.Title}\nAuthor: {lib.Author}\n";
            var date = new DateTime(lib.DateTime, DateTimeKind.Utc);
            header += $"Date: {date}\n";
            header += $"Description: {lib.Description}\n\n";
            var lines = lib.Instructions.Split('\n');
            int count = Math.Min(15, lines.Length);
            var preview = string.Join("\n", lines.Take(count));
            return header + preview + "\n...";
        }

        private void LoadLibraryEntry(InstructionData lib)
        {
            if (lib == null)
                return;

            var editor = new IEditor(KeyHandler, lib);
            editor.Title = lib.Title;
            editor.ResetCode(lib.Instructions);
            Tabs.Add(editor);
            _activeTabIndex = Tabs.Count - 1;
        }

        public void DrawLibrarySearchWindow123()
        {
            if (!_librarySearchVisible)
                return;

            ImGui.BeginPopupModal("Library Search");
            ImGui.Text("Library search is not implemented yet.");
            ImGui.Separator();
            ImGui.InputText("Search", ref _librarySearchText, 256);

            ImGui.TextWrapped("Library search functionality is not implemented in this version of the editor. Sorry for the inconvenience.");
            // generate results (full text case-insensitive search)
            // show all results as a list
            ImGui.Separator();

            if (ImGui.Button("Close"))
            {
                _librarySearchVisible = false;
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();

            ImGui.EndPopup();
        }

        public void SwitchToNativeEditor()
        {
            UseNativeEditor = true;
            Show = false;

            // localPosition was set to -10000,-10000,0 to hide the native editor, so set it back to 0,0,0 to show it
            InputSourceCode.Instance.Window.localPosition = new Vector3(0, 0, 0);
            KeyManager.RemoveInputState("ic10editorinputstate");
            InputSourceCode.Paste(MotherboardTab.Code);
        }

        public void Confirm()
        {
            if (LimitExceeded)
            {
                ActiveTab.CommandStatus = LimitExceededMessage;
                return;
            }
            ActiveTab.Save();
            HideWindow();
        }

        public void Export()
        {
            if (LimitExceeded)
            {
                ActiveTab.CommandStatus = LimitExceededMessage;
                return;
            }
            Confirm();
            MotherboardTab.PCM.Export();
        }
        public void HideWindow()
        {
            if (Show == false)
                return;

            Show = false;
            KeyManager.RemoveInputState("ic10editorinputstate");
            if (InputWindow.InputState == InputPanelState.Waiting)
                InputWindow.CancelInput();
            if (WorldManager.IsGamePaused)
                InputSourceCode.Instance.PauseGameToggle(false);
            InputSourceCode.Instance.ButtonInputCancel();

            // This fixes following behavior:
            // 1. Open IC10 editor while alt key is pressed (e.g. laptop)
            // 2. Close IC10 editor with cancel button
            // -> Right click action on any tool not working until alt key is pressed again
            InputMouse.SetMouseControl(false);
        }

        public void ShowWindow()
        {
            Show = true;
            KeyManager.SetInputState("ic10editorinputstate", KeyInputState.Typing);

            if (VimEnabled)
                KeyHandler.Mode = KeyMode.VimNormal;

            if (!WorldManager.IsGamePaused && PauseOnOpen)
                InputSourceCode.Instance.PauseGameToggle(true);

            InputSourceCode.Instance.RectTransform.localPosition = new Vector3(-10000, -10000, 0);
        }

        public bool IsInitialized = false;
        public void SetTitle(string title)
        {
            Title = title;
        }

        public void HandleInput(bool hasFocus)
        {
            KeyHandler.HandleInput(hasFocus);
        }

        public void ShowNativeWindow(HelpMode mode)
        {
            foreach (var window in InputSourceCode.Instance.HelpWindows)
                if (window.HelpMode == mode)
                    window.ToggleVisibility();
        }

        private bool _helpWindowVisible = false;
        private bool _debugWindowVisible = false;

        public void DrawHeader()
        {
            // rounded buttons style
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5.0f);

            if (ImGui.Button($"Library", buttonSize))
                ShowNativeWindow(HelpMode.Instructions);

            ImGui.SameLine();

            if (ImGui.Button("Clear", buttonSize))
                ActiveTab.ClearCode();

            ImGui.SameLine();

            if (ImGui.Button("Copy", buttonSize))
                GameManager.Clipboard = Code;

            ImGui.SameLine();

            if (ImGui.Button("Paste", buttonSize))
            {
                ActiveTab.ClearCode();
                ActiveTab.Insert(GameManager.Clipboard);
            }

            ImGui.SameLine();

            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 2 * ImGui.GetStyle().ItemSpacing.x);

            if (ImGui.Button("?", smallButtonSize))
                _helpWindowVisible = !_helpWindowVisible;

            ImGui.SameLine();

            if (ImGui.Button("Native", buttonSize))
                SwitchToNativeEditor();

            ImGui.SameLine();

            ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 5 * smallButtonSize.x - buttonSize.x - ImGui.GetStyle().FramePadding.x * 3 - ImGui.GetStyle().ItemSpacing.x * 3);

            bool isIC10 = ActiveTab.CodeFormatter as IC10.IC10CodeFormatter != null;
            if (ImGui.Button(isIC10 ? "IC10" : "txt"))
            {
                ActiveTab.CodeFormatter = CodeFormatters.GetFormatter(isIC10 ? "Plain" : "txt");
                ActiveTab.CodeFormatter.ResetCode(ActiveTab.Code);
            }

            ImGui.SameLine();

            if (ImGui.Button("s(x)", smallButtonSize))
                ShowNativeWindow(HelpMode.SlotVariables);

            ImGui.SameLine();

            if (ImGui.Button("x", smallButtonSize))
                ShowNativeWindow(HelpMode.Variables);

            ImGui.SameLine();

            if (ImGui.Button("f", smallButtonSize))
                ShowNativeWindow(HelpMode.Functions);

            ImGui.SameLine();

            bool isPaused = WorldManager.IsGamePaused;
            if (ImGui.Button(isPaused ? "Resume" : "Pause", buttonSize))
                InputSourceCode.Instance.PauseGameToggle(!isPaused);

            ImGui.PopStyleVar();
        }

        private static uint _colorGood = ICodeFormatter.ColorFromHTML("green");
        private static uint _colorWarning = ICodeFormatter.ColorFromHTML("orange");
        private static uint _colorBad = ICodeFormatter.ColorFromHTML("red");
        private static uint _colorDefault = ICodeFormatter.ColorFromHTML("white");

        public void DrawFooter()
        {
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5.0f);
            ImGui.Text($"{CaretLine,3}/{CaretCol,2},");
            ImGui.SameLine();

            var pos = ImGui.GetCursorScreenPos();
            var charWidth = ImGui.CalcTextSize("M").x;

            var sLines = $"{Lines.Count,3}";
            var sBytes = $"{Code.Length,4}";

            uint lineColor = _colorDefault;
            if (EnforceLineLimit)
            {
                sLines += "/128";
                lineColor = Lines.Count < 120 ? _colorGood : (Lines.Count <= 128 ? _colorWarning : _colorBad);
            }

            uint byteColor = _colorDefault;
            if (EnforceByteLimit)
            {
                sBytes += "/4096";
                byteColor = Code.Length < 4000 ? _colorGood : (Code.Length <= 4096 ? _colorWarning : _colorBad);
            }

            var drawList = ImGui.GetWindowDrawList();
            drawList.AddText(pos, lineColor, sLines);
            pos.x += sLines.Length * charWidth;
            drawList.AddText(pos, _colorDefault, " lines,");
            pos.x += 8 * charWidth;
            drawList.AddText(pos, byteColor, sBytes);
            pos.x += sBytes.Length * charWidth;
            drawList.AddText(pos, _colorDefault, " bytes");

            ImGui.SameLine();

            ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 3 * buttonSize.x - ImGui.GetStyle().FramePadding.x * 3 - ImGui.GetStyle().ItemSpacing.x);
            if (ImGui.Button("Cancel", buttonSize))
                HideWindow();

            ImGui.SameLine();

            if (LimitExceeded)
            {
                ImGui.PushItemFlag(ImGuiItemFlags.Disabled, true);
                ImGui.PushStyleColor(ImGuiCol.Button, ICodeFormatter.ColorFromHTML("gray"));
            }

            if (ImGui.Button("Export", buttonSize))
                Export();

            ImGui.SameLine();
            if (ImGui.Button("Confirm", buttonSize))
                Confirm();

            if (LimitExceeded)
            {
                ImGui.PopItemFlag();
                ImGui.PopStyleColor();
            }

            KeyHandler.DrawStatus();
            CodeFormatter.DrawStatus();

            ImGui.PopStyleVar();
        }


        private Vector2 _textAreaOrigin, _textAreaSize;
        private float _scrollY = 0.0f;

        public bool IsMouseInsideTextArea()
        {
            Vector2 mousePos = ImGui.GetMousePos();
            float px = _textAreaOrigin.x;
            float py = _textAreaOrigin.y + _scrollY - ImGui.GetStyle().FramePadding.y;
            return mousePos.x >= px
                && mousePos.x <= px + _textAreaSize.x
                && mousePos.y >= py
                && mousePos.y <= py + _textAreaSize.y;
        }

        private Vector2 _caretPixelPos;

        public unsafe void DrawCodeArea()
        {
            var padding = ImGui.GetStyle().FramePadding;
            float scrollHeight = ImGui.GetContentRegionAvail().y - 2 * ImGui.GetTextLineHeightWithSpacing() - 2 * padding.y;
            ImGui.BeginChild("ScrollRegion", new Vector2(0, scrollHeight), true);
            _textAreaOrigin = ImGui.GetCursorScreenPos();
            _textAreaSize = ImGui.GetContentRegionAvail() + 2 * padding;

            ImGuiListClipperPtr clipper = new ImGuiListClipperPtr(
                ImGuiNative.ImGuiListClipper_ImGuiListClipper());

            clipper.Begin(Lines.Count);

            Vector2 mousePos = ImGui.GetMousePos();

            if (ActiveTab.ScrollToCaret > 0)
            {
                float lineHeight = ImGui.GetTextLineHeightWithSpacing();
                float lineSpacing = ImGui.GetStyle().ItemSpacing.y;

                float pageHeight = (Lines.Count * lineHeight) - ImGui.GetScrollMaxY();
                float scrollY = ImGui.GetScrollY();
                float viewTop = _scrollY;
                float viewBottom = _scrollY + pageHeight;

                float caretTop = CaretLine * lineHeight;
                float caretBottom = caretTop + lineHeight;

                if (caretTop < viewTop)
                {
                    scrollY = caretTop;
                }
                else if (caretBottom > viewBottom)
                {
                    scrollY = caretBottom - pageHeight + lineSpacing;
                }

                ImGui.SetScrollY(Math.Min(scrollY, ImGui.GetScrollMaxY()));
                ActiveTab.ScrollToCaret -= 1;
            }

            _scrollY = ImGui.GetScrollY();

            var selection = Selection.Sorted();
            while (clipper.Step())
            {
                for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                {
                    CodeFormatter.DrawLine(i, Lines[i], selection);

                    if (i == CaretLine)
                    {
                        DrawCaret(_caretPixelPos);
                        _caretPixelPos = ImGui.GetCursorScreenPos();
                        _caretPixelPos.x += ImGui.CalcTextSize("M").x * (CaretCol + ICodeFormatter.LineNumberOffset);
                    }

                    ImGui.NewLine();
                }
            }

            if (EnableAutoComplete)
            {
                float charHeight = ImGui.GetTextLineHeightWithSpacing();
                var completePos = _caretPixelPos + new Vector2(0, 1.5f * charHeight);

                CodeFormatter.DrawAutocomplete(ActiveTab, CaretPos, completePos);
            }

            clipper.End();
            ImGui.EndChild();

        }

        public void DrawCaret(Vector2 pos)
        {
            var drawList = ImGui.GetWindowDrawList();
            var lineHeight = ImGui.GetTextLineHeight();
            var lineHeight2 = ImGui.GetTextLineHeightWithSpacing();

            if (KeyHandler.Mode == KeyMode.Insert)
            {
                bool blinkOn = ((int)((ImGui.GetTime() - ActiveTab.TimeLastAction) * 2) % 2) == 0;
                if (blinkOn)
                {
                    drawList.AddLine(
                        pos,
                        new Vector2(pos.x, pos.y + lineHeight2),
                        ImGui.ColorConvertFloat4ToU32(new Vector4(1, 1, 1, 1)),
                        1.5f);
                }
            }
            else
            {
                // Draw a block cursor
                drawList.AddRect(
                    new Vector2(pos.x, pos.y),
                    new Vector2(pos.x + ImGui.CalcTextSize("M").x, pos.y + lineHeight2),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(0.7f, 0.7f, 0.7f, 1.0f)));
                drawList.AddRect(
                    new Vector2(pos.x - 1, pos.y - 1),
                    new Vector2(pos.x + 1 + ImGui.CalcTextSize("M").x, pos.y + lineHeight2 + 1),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 1.0f)));
            }
        }

        private bool _hasFocus = false;
        private int _openGameWindows = 0;
        private Vector2 _windowPos = new Vector2(100, 100);
        private bool _didGameWindowOpen = false;


        public bool HasFocus => _hasFocus && !_librarySearchVisible;

        public void CalcDidGameWindowOpen()
        {
            int count = 0;
            count += Stationpedia.Instance.IsVisible ? 1 : 0;

            foreach (var window in InputSourceCode.Instance.HelpWindows)
                count += window.IsVisible ? 1 : 0;

            _didGameWindowOpen = count > _openGameWindows;
            _openGameWindows = count;
        }

        private Queue<double> _renderTimes = new();
        private System.Diagnostics.Stopwatch _renderStopwatch;

        public void DrawEditor()
        {
            ImGui.PushFont(ImGui.GetIO().Fonts.Fonts[0]);
            _windowPos = ImGui.GetWindowPos();

            DrawCodeArea();
            ImGui.PopFont();
        }

        public void Draw()
        {
            if (!Show) return;

            if (_debugWindowVisible)
                _renderStopwatch = System.Diagnostics.Stopwatch.StartNew();

            // make sure the native editor is hidden
            InputSourceCode.Instance.Window.localPosition = new Vector3(-10000, -10000, 0);

            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.1f, 0.1f, 0.1f, 1.0f));
            var io = ImGui.GetIO();
            ImGui.PushFont(ImGui.GetIO().Fonts.Fonts[0]);

            if (!IsInitialized)
            {
                var displaySize = ImGui.GetIO().DisplaySize;
                var windowSize = new Vector2(
                    Math.Min(1200, displaySize.x - 100),
                    displaySize.y - 100);
                var _windowPos = 0.5f * (displaySize - windowSize);

                _windowPos.x = Mathf.Round(_windowPos.x);
                _windowPos.y = Mathf.Round(_windowPos.y);

                _windowPos = Scale * _windowPos;

                ImGui.SetNextWindowSize(windowSize);
                ImGui.SetNextWindowPos(_windowPos);
                IsInitialized = true;
            }

            CalcDidGameWindowOpen();
            if (_didGameWindowOpen)
            {
                _windowPos.x = Math.Max(0.5f * ImGui.GetIO().DisplaySize.x + 50.0f, _windowPos.x);
                _windowPos.x = Mathf.Round(_windowPos.x);
                _windowPos.y = Mathf.Round(_windowPos.y);
                ImGui.SetNextWindowPos(_windowPos);
            }

            ImGui.Begin(Title + "###IC10EditorWindow", ImGuiWindowFlags.NoSavedSettings);

            // Create a tab bar (the shared titlebar effect comes from this)

            ImGui.SetWindowFontScale(Scale);
            DrawHeader();

            if (HasFocus)
                HandleInput(_hasFocus);

            _hasFocus = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);


            if (ImGui.BeginTabBar("EditorTabs"))
            {
                for (int i = 0; i < Tabs.Count; i++)
                {
                    var tab = Tabs[i];
                    bool isOpen = _activeTabIndex == i;
                    if (ImGui.BeginTabItem($"{tab.Title} ###{i}", isOpen ? ImGuiTabItemFlags.SetSelected : 0))
                    {
                        DrawEditor();
                        ImGui.EndTabItem();
                    }
                    if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
                        _activeTabIndex = i;
                }
            }
            ImGui.EndTabBar();

            DrawFooter();
            DrawLibrarySearchWindow();


            ImGui.End();
            ImGui.PopStyleColor();

            if (_hasFocus && IsMouseInsideTextArea())
            {
                ImGui.PushFont(ImGui.GetIO().Fonts.Fonts[0]);
                if (KeyHandler.IsMouseIdle(TooltipDelay / 1000.0f))
                {
                    var pos = GetTextPositionFromMouse(false);
                    if (pos)
                        CodeFormatter.DrawTooltip(Lines[CaretLine], pos, _caretPixelPos);
                }
                ImGui.PopFont();
            }

            DrawHelpWindow();
            DrawDebugWindow();

            ImGui.PopFont();


        }

        public void CloseTab()
        {
            var index = _activeTabIndex;
            if (index == 0)
                return;
            if (Tabs.Count <= 1)
                return;
            Tabs.RemoveAt(index);
            if (_activeTabIndex >= Tabs.Count)
                _activeTabIndex = Tabs.Count - 1;

        }
        public void PreviousTab()
        {
            _activeTabIndex = (_activeTabIndex - 1 + Tabs.Count) % Tabs.Count;
        }
        public void NextTab()
        {
            _activeTabIndex = (_activeTabIndex + 1) % Tabs.Count;
        }

        public void SetTab(int index)
        {
            index--;
            if (index < 0 || index >= Tabs.Count)
                return;
            _activeTabIndex = index;
        }

        private void DrawBoolOption(string label, ConfigEntry<bool> entry)
        {
            var value = entry.Value;
            if (ImGui.Checkbox(label, ref value))
                entry.BoxedValue = value;
        }

        private void DrawFloatOption(string label, ConfigEntry<float> entry, float min, float max)
        {
            var value = entry.Value;
            if (ImGui.Button("Reset"))
            {
                entry.BoxedValue = 1.0f;
                value = 1.0f;
            }
            ImGui.SameLine();
            if (ImGui.SliderFloat(label, ref value, min, max))
                entry.BoxedValue = value;
        }

        private void DrawFloatOption(string label, ConfigEntry<float> entry)
        {
            var value = entry.Value;
            ImGui.PushItemWidth(ImGui.CalcTextSize("000000.00").x);
            if (ImGui.InputFloat(label, ref value))
                entry.BoxedValue = value;
            ImGui.PopItemWidth();
        }

        public void DrawHelpWindow()
        {
            if (!_helpWindowVisible)
                return;
            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.2f, 0.2f, 0.2f, 1.0f));
            ImGui.SetNextWindowSize(Scale * new Vector2(600, 400), ImGuiCond.FirstUseEver);
            ImGui.Begin("IC10 Editor Help", ref _helpWindowVisible, ImGuiWindowFlags.NoSavedSettings);
            ImGui.SetWindowFontScale(Mathf.Clamp(Scale, 0.5f, 5.0f));

            ImGui.TextWrapped(
                        "This is the IC10 Editor. It allows you to edit the source code of IC10 programs with syntax highlighting, undo/redo, and other features.\n\n");


            ImGui.Separator();
            ImGui.Text("\nConfiguration:");
            DrawBoolOption("Pause Game on Open", IC10EditorPlugin.PauseOnOpen);
            DrawBoolOption("Enforce 128 Lines Limit", IC10EditorPlugin.EnforceLineLimit);
            DrawBoolOption("Enforce 4096 Bytes Limit", IC10EditorPlugin.EnforceByteLimit);
            DrawFloatOption("UI Scaling", IC10EditorPlugin.ScaleFactor, 0.25f, 5.0f);
            DrawFloatOption("Toolitp delay (ms)", IC10EditorPlugin.TooltipDelay);
            DrawBoolOption("VIM bindings", IC10EditorPlugin.VimBindings);
            DrawBoolOption("Auto Completion (experimental, insert with Tab key)", IC10EditorPlugin.EnableAutoComplete);
            ImGui.Checkbox("Show debug window", ref _debugWindowVisible);

            ImGui.Separator();

            ImGui.TextWrapped(
                "\nKeyboard Shortcuts:\n" +
                "\n" +
                "Arrow Keys     Move caret\n" +
                "Home/End       Move caret to start/end of line\n" +
                "Page Up/Down   Move caret up/down by 20 lines\n" +
                "Shift+Arrow    Select text while moving caret\n" +
                "Ctrl + Q         Quit (no confirm, see note below)\n" +
                "Ctrl + S         Save\n" +
                "Ctrl + E         Save + export code to ic chip + close\n" +
                "Ctrl + Z         Undo\n" +
                "Ctrl + Y         Redo\n" +
                "Ctrl + C         Copy selected code\n" +
                "Ctrl + V         Paste code from clipboard\n" +
                "Ctrl + A         Select all code\n" +
                "Ctrl + X         Cut selected code\n" +
                "Ctrl + Arrow     Move caret by word\n" +
                "Ctrl + Click     Open Stationpedia page of word at cursor\n\n"
                );

            ImGui.Separator();

            ImGui.TextWrapped(
                "\nNotes:\n" +
                "\n" +
                "Closing the editor via Ctrl+Q key or Cancel button will not ask for confirmation, BUT you can always reopen the editor and Undo (Ctrl+Z) to get the state before cancelling.\n"
                );

            ImGui.Separator();

            ImGui.TextWrapped(
                "\nVIM Mode - Supported Commands:\n" +
                "\n" +
                "Movements (with optional number prefix):\n" +
                "h j, k, l, w, b, 0, $, gg, G\n\n" +
                "Editing (with optional number and movement or search):\n" +
                "i I a A c C d D dd o O x y yy p ~ << >> u Ctrl+r\n\n" +
                "Search:\n" +
                "f t gf\n\n" +
                "Other:\n" +
                ". ; :w :wq :q\n\n" +
                "Notes:\n" +
                "'gf' opens Stationpedia page of hash/name at cursor\n\n" +
                "'.'  is not working for commands that switch to insert mode\n\n"
                );

            ImGui.Separator();
            ImGui.End();
            ImGui.PopStyleColor();
        }

        public void DrawDebugWindow()
        {
            if (!_debugWindowVisible)
                return;

            ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.2f, 0.2f, 0.2f, 1.0f));
            ImGui.SetNextWindowSize(Scale * new Vector2(600, 400), ImGuiCond.FirstUseEver);
            ImGui.Begin("IC10 Debug Window", ref _debugWindowVisible, ImGuiWindowFlags.NoSavedSettings);
            ImGui.SetWindowFontScale(Mathf.Clamp(Scale, 0.5f, 5.0f));

            double avgRenderTime = 0.0;
            double maxRenderTime = 0.0;

            foreach (var time in _renderTimes)
            {
                avgRenderTime += time;
                if (time > maxRenderTime)
                    maxRenderTime = time;
            }
            if (_renderTimes.Count > 0)
                avgRenderTime /= _renderTimes.Count;

            avgRenderTime = (avgRenderTime * 1000000.0);
            maxRenderTime = (maxRenderTime * 1000000.0);

            ImGui.Text($"Render Time: {avgRenderTime:F0} us avg, {maxRenderTime:F0} us max");

            if (_renderStopwatch != null)
            {
                double seconds = _renderStopwatch.Elapsed.TotalSeconds;
                _renderTimes.Enqueue(seconds);
                while (_renderTimes.Count > 100)
                    _renderTimes.Dequeue();
            }

            ImGui.Separator();
        }

        public TextPosition GetTextPositionFromMouse(bool clampToTextArea = true)
        {
            Vector2 mousePos = ImGui.GetMousePos();
            float charWidth = ImGui.CalcTextSize("M").x;
            float lineHeight = ImGui.GetTextLineHeightWithSpacing();

            int line = (int)((mousePos.y - _textAreaOrigin.y) / lineHeight);
            int column =
                (int)((mousePos.x - _textAreaOrigin.x) / charWidth) - ICodeFormatter.LineNumberOffset;

            if (!clampToTextArea && (line < 0 || line >= Lines.Count))
            {
                return new TextPosition(-1, -1);
            }

            line = Mathf.Clamp(line, 0, Lines.Count - 1);
            string lineText = Lines[line];
            column = Mathf.Clamp(column, 0, lineText.Length);

            return new TextPosition(line, column);
        }
    }

}
