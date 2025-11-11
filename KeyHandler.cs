namespace StationeersIC10Editor
{
    using System;
    using ImGuiNET;
    using Assets.Scripts;
    using Assets.Scripts.Objects;
    using Assets.Scripts.UI;
    using UnityEngine;

    public enum KeyMode
    {
        Insert,
        VimNormal
    }

    public struct VimCommand
    {
        public string Command;
        public char Movement;
        public string Argument;
        private uint _count;

        public uint Count
        {
            get => _count == 0 ? 1 : _count;
            set => _count = value;
        }

        public VimCommand()
        {
            Command = "";
            Count = 0;
            Movement = '\0';
            Argument = "";
        }

        public void AddChar(char c)
        {
            if (Command == "r" || Command == ":" || Movement == 'f' || Movement == 't')
                Argument += c.ToString();
            else if (char.IsDigit(c) && (_count > 0 || c != '0'))
                Count = _count * 10 + (uint)(c - '0');
            else if (_movements.Contains($"{c}"))
                Movement = c;
            else
                Command += c;

            if (!IsValid)
                Reset();
        }

        public void Reset()
        {
            Command = "";
            Count = 0;
            Movement = '\0';
            Argument = "";
        }

        public override string ToString()
        {
            var sCount = _count > 0 ? Count.ToString() : "";
            var sMovement = Movement != '\0' ? Movement.ToString() : "";
            return $"{Command}{sCount}{sMovement}{Argument}";
        }

        private static readonly string _movements = "fthjklwb0$G";
        private static readonly string _immediateSingleCharCommands = "aiuCDxIAOoJpPx" + _movements.Substring(2);
        private static readonly string _singleCharCommands = "cdry";
        private static readonly string _twoCharCommands = "dd yy cc gg ";

        private static readonly string _validFirstChars = _immediateSingleCharCommands + _singleCharCommands + "gft:";

        public bool IsValid
        {
            get
            {
                if (Command == ":")
                    return true;

                if (Command.Length == 0)
                    return true;

                if (Command.Length > 2)
                    return false;

                if (Command.Length == 1)
                    return _validFirstChars.Contains(Command);

                return _twoCharCommands.Contains(Command + " ");
            }
        }

        public bool IsComplete
        {
            get
            {
                if (Command == ":")
                    return Argument.EndsWith("\n");

                if (Movement != '\0')
                    return (Movement != 'f' && Movement != 't') || Argument.Length == 1;

                if (Command.Length == 1)
                    if (_immediateSingleCharCommands.Contains(Command))
                        return true;
                    else
                        return _singleCharCommands.Contains(Command) && Argument.Length == 1;

                if (Command.Length == 2)
                    return _twoCharCommands.Contains(Command + " ");

                return false;
            }
        }

        public TextRange CaretAfterMove(IC10Editor ed)
        {
            var startPos = ed.CaretPos;
            var pos = ed.CaretPos;
            string cmd = Movement != '\0' ? $"{Movement}" : Command;
            switch (cmd)
            {
                case "dd":
                case "yy":
                    startPos.Col = 0;
                    pos.Line = startPos.Line + (int)Count - 1;
                    pos = ed.Clamp(pos);
                    pos.Col = ed.Lines[pos.Line].Length + 1;
                    return new TextRange(startPos, pos);
                case "j":
                    pos = ed.Move(pos, new MoveAction(MoveToken.Line, true, Count));
                    break;
                case "J":
                    pos = ed.Move(pos, new MoveAction(MoveToken.Line, true, Count));
                    startPos.Col = 0;
                    break;
                case "k":
                    pos = ed.Move(pos, new MoveAction(MoveToken.Line, false, Count));
                    break;
                case "h":
                    pos = ed.Move(pos, new MoveAction(MoveToken.Char, false, Count));
                    break;
                case "l":
                case "a":
                    pos = ed.Move(pos, new MoveAction(MoveToken.Char, true, Count));
                    break;
                case "x":
                case "r":
                    pos.Col += (int)Count;
                    pos.Col = Math.Min(pos.Col, ed.Lines[pos.Line].Length);
                    break;
                case "w":
                    pos = ed.Move(pos, new MoveAction(MoveToken.WordBeginning, true, Count));
                    break;
                case "b":
                    pos = ed.Move(pos, new MoveAction(MoveToken.WordBeginning, false, Count));
                    break;
                case "0":
                    pos = new TextPosition(pos.Line, 0);
                    break;
                case "$":
                    pos = new TextPosition(pos.Line, ed.Lines[pos.Line].Length - 1);
                    break;
                case "C":
                case "D":
                    var newLine = pos.Line + (int)Count - 1;
                    pos = new TextPosition(newLine, ed.Lines[newLine].Length);
                    break;
                case "I":
                    var col = ed.CurrentLine.Length - ed.CurrentLine.TrimStart().Length;
                    pos = new TextPosition(pos.Line, col);
                    break;
                case "A":
                    pos = new TextPosition(pos.Line, ed.Lines[pos.Line].Length);
                    break;
                case "f":
                case "t":
                    {
                        var line = ed.Lines[pos.Line];
                        var newCol = pos.Col;
                        var apply = true;
                        for (int i = 0; i < Count; i++)
                        {
                            var index = line.IndexOf(Argument, pos.Col + 1);
                            if (index == -1)
                            {
                                apply = false;
                                break;
                            }
                            newCol = index;
                            if (!string.IsNullOrEmpty(Command))
                                newCol += 1;
                            if (cmd == "t")
                                newCol -= 1;
                        }
                        if (apply)
                            pos.Col = newCol;
                    }
                    break;
                case "G":
                    pos.Line = ed.Lines.Count - 1;
                    pos.Col = ed.Lines[pos.Line].Length + 1;
                    break;
            }

            return ed.Clamp(new TextRange(startPos, pos));
        }

        public string Execute(IC10Editor editor)
        {
            TextRange range;
            if (editor.HaveSelection)
                range = editor.Clamp(editor.Selection.Sorted());
            else
                range = CaretAfterMove(editor);

            var status = "";
            var nLines = range.End.Line - range.Start.Line + 1;
            if (range.Start.Col > 0 || range.End.Col <= editor.Lines[range.End.Line].Length)
                nLines = 0;
            var sLines = $"{nLines} " + (nLines > 1 ? "lines" : "line");

            switch (Command)
            {
                case "":
                case "f":
                case "t":
                case "h":
                case "j":
                case "k":
                case "l":
                case "w":
                case "b":
                case "0":
                case "G":
                    editor.CaretPos = range.End;
                    break;
                case "d":
                case "D":
                case "x":
                case "dd":
                    editor.CopyRange(range);
                    editor.DeleteRange(range);
                    if (nLines > 0)
                        status = $"Deleted {sLines}";
                    break;
                case "r":
                    string oldCode = editor.GetCode(range);
                    editor.DeleteRange(range);
                    editor.Insert(Argument.PadRight(oldCode.Length, Argument[0]));
                    break;
                case "y":
                case "yy":
                    editor.CopyRange(range);
                    if (nLines > 0)
                        status = $"Yanked {sLines}";
                    break;
                case "c":
                case "C":
                    editor.KeyHandler.InsertMode();
                    editor.DeleteRange(range, false);
                    editor.CaretPos = range.Start;
                    break;
                case "O":
                    editor.KeyHandler.InsertMode();
                    editor.CaretPos = new TextPosition(editor.CaretLine, 0);
                    editor.Insert("\n");
                    editor.CaretPos = new TextPosition(editor.CaretLine - 1, 0);
                    break;
                case "o":
                    editor.KeyHandler.InsertMode();
                    bool move = editor.CaretLine < editor.Lines.Count -1;
                    editor.CaretPos = editor.Clamp(new TextPosition(editor.CaretLine + 1, 0));
                    editor.Insert("\n");
                    if (move)
                        editor.CaretPos = new TextPosition(editor.CaretLine - 1, 0);
                    break;
                case "u":
                    editor.Undo();
                    break;
                case "i":
                case "a":
                    editor.CaretPos = range.End;
                    editor.KeyHandler.InsertMode();
                    break;
                case "I":
                case "A":
                    editor.CaretPos = range.End;
                    editor.KeyHandler.InsertMode();
                    break;
                case "J":
                    if (range.End.Line > range.Start.Line)
                    {
                        var newCode = editor.GetCode(range).Replace("\n", " ");
                        editor.KeyHandler.OnKeyPressed($"J - Join lines, range={range}, newCode='{newCode}', oldCode='{editor.GetCode(range)}'");
                        editor.DeleteRange(range);
                        editor.Insert(newCode);
                    }
                    break;
                case "gg":
                    editor.CaretLine = (int)_count;
                    break;
                case "p":
                    editor.PushUndoState(false);
                    var code = GameManager.Clipboard.Replace("\r", "");
                    bool insertLines = code.EndsWith("\n");
                    for (int i = 0; i < (int)Count; i++)
                    {
                        if (insertLines)
                        {
                            editor.CaretCol = 0;
                            editor.CaretLine += 1;
                            var posBefore = editor.CaretPos;
                            editor.Insert(code);
                            editor.CaretPos = posBefore;
                        }
                        else
                        {
                            editor.CaretCol += 1;
                            editor.Insert(code);
                        }
                    }
                    break;
                case "P":
                    editor.PushUndoState(false);
                    code = GameManager.Clipboard.Replace("\r", "");
                    insertLines = code.EndsWith("\n");
                    for (int i = 0; i < (int)Count; i++)
                    {
                        if (insertLines)
                        {
                            editor.CaretCol = 0;
                            var posBefore = editor.CaretPos;
                            editor.Insert(code);
                            editor.CaretPos = posBefore;
                        }
                        else
                        {
                            editor.Insert(code);
                        }
                    }
                    break;
                case ":":
                    foreach (char c in Argument)
                    {
                        switch (c)
                        {
                            case 'q':
                                L.Info("Vim command :q - exiting editor");
                                editor.HideWindow();
                                status = "Exited editor";
                                break;
                            case 'w':
                                L.Info("Vim command :w - writing editor");
                                editor.Write();
                                status = "Saved file";
                                break;
                        }
                    }
                    break;
            }
            return status;
        }
    }

    public class KeyHandler
    {
        public Action<string> OnKeyPressed = delegate { };
        public static bool VimEnabled => IC10EditorPlugin.VimBindings.Value;

        IC10Editor Editor;
        public KeyMode Mode = KeyMode.Insert;

        private double _timeLastEscape = 0.0;
        private bool _isSelecting = false;
        private double _timeLastMouseMove = 0.0;
        private Vector2 _lastMousePos = new Vector2(0, 0);

        TextPosition CaretPos
        {
            get => Editor.CaretPos;
            set => Editor.CaretPos = value;
        }

        string CurrentLine
        {
            get => Editor.CurrentLine;
            set => Editor.CurrentLine = value;
        }

        int CaretCol
        {
            get => Editor.CaretCol;
            set => Editor.CaretCol = value;
        }

        int CaretLine
        {
            get => Editor.CaretLine;
            set => Editor.CaretLine = value;
        }

        public KeyHandler(IC10Editor editor)
        {
            Editor = editor;
        }

        TextPosition Move(TextPosition pos, MoveAction action)
        {
            return Editor.Clamp(Editor.Move(pos, action));
        }

        public bool IsMouseIdle(double idleTime)
        {
            return (ImGui.GetTime() - _timeLastMouseMove) >= idleTime;
        }


        public void InsertMode()
        {
            if (Mode == KeyMode.Insert)
                return;
            Editor.PushUndoState(false);
            Mode = KeyMode.Insert;
            ResetCommandState();
        }

        private string CommandStatus = "";

        public void DrawStatus()
        {
            if (VimEnabled)
            {
                String status = $"Mode: {Mode} ";
                ImGui.Text(status);
                ImGui.SameLine();

                if (Mode == KeyMode.VimNormal)
                {
                    if (String.IsNullOrEmpty(CommandStatus))
                    {
                        ImGui.Text($"{CurrentCommand}");
                        ImGui.SameLine();
                    }
                    ImGui.Text($"{CommandStatus}");
                    ImGui.SameLine();
                }
            }
        }

        public void HandleMouse(bool ctrlDown, bool shiftDown)
        {
            var mousePos = ImGui.GetMousePos();
            if (mousePos != _lastMousePos)
            {
                _timeLastMouseMove = ImGui.GetTime();
                _lastMousePos = mousePos;
            }

            if (Editor.IsMouseInsideTextArea())
            {
                if (ctrlDown)
                {
                    if (ImGui.IsMouseReleased(0))
                    {
                        OnKeyPressed("Ctrl+Click");
                        // open stationpedia page for word under mouse
                        var name = Editor.GetCode(Editor.GetWordAt(Editor.GetTextPositionFromMouse()));

                        if (Int32.TryParse(name, out var hash))
                        {
                            var thing = Prefab.Find<Thing>(hash);
                            if (thing == null) return;
                            name = thing.PrefabName;
                        }

                        name = "Thing" + name;

                        Stationpedia._linkIdLookup.TryGetValue(name, out var page);
                        if (page != null)
                            Stationpedia.Instance.OpenPageByKey(page.Key);
                    }
                }
                else
                {
                    if (ImGui.IsMouseDoubleClicked(0))
                    {
                        OnKeyPressed("DoubleClick");
                        _isSelecting = false;
                        var clickPos = Editor.GetTextPositionFromMouse();
                        var range = Editor.GetWordAt(clickPos);

                        Editor.Selection.Start = range.Start;
                        Editor.Selection.End = range.End;
                        CaretPos = range.End;
                        InsertMode();
                    }
                    else if (ImGui.IsMouseClicked(0)) // Left click
                    {
                        OnKeyPressed("Click");
                        _isSelecting = true;
                        var clickPos = Editor.GetTextPositionFromMouse();
                        CaretPos = clickPos;
                        Editor.Selection.Start = clickPos;
                        Editor.Selection.End.Reset();
                    }
                    else if (_isSelecting)
                        Editor.Selection.End = Editor.GetTextPositionFromMouse();

                    if (ImGui.IsMouseReleased(0))
                        _isSelecting = false;

                }
            }
        }

        public void HandleCommon(bool ctrlDown, bool shiftDown)
        {
            var io = ImGui.GetIO();
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                // these combos are not captured by ImGui for some reason, so handle them via Unity Input
                if (Input.GetKeyDown(KeyCode.S))
                    Editor.Confirm();
                if (Input.GetKeyDown(KeyCode.E))
                    Editor.Export();
            }

            if (ctrlDown)
            {
                if (ImGui.IsKeyPressed(ImGuiKey.V))
                {
                    Editor.Paste();
                    OnKeyPressed("Ctrl+V - Paste");
                }
                if (ImGui.IsKeyPressed(ImGuiKey.A))
                {
                    Editor.SelectAll();
                    OnKeyPressed("Ctrl+A - Select All");
                }
                if (ImGui.IsKeyPressed(ImGuiKey.C))
                {
                    Editor.Copy();
                    OnKeyPressed("Ctrl+C - Copy");
                }
                if (ImGui.IsKeyPressed(ImGuiKey.X))
                {
                    Editor.Cut();
                    OnKeyPressed("Ctrl+X - Cut");
                }
                if (ImGui.IsKeyPressed(ImGuiKey.Z))
                {
                    Editor.Undo();
                    OnKeyPressed("Ctrl+Z - Undo");
                }
                if (ImGui.IsKeyPressed(ImGuiKey.Y))
                {
                    Editor.Redo();
                    OnKeyPressed("Ctrl+Y - Redo");
                }

                // for (int i = 0; i < io.InputQueueCharacters.Size; i++)
                // {
                //     var ic = io.InputQueueCharacters[i];
                //     char c = (char)ic;
                // }
            }
            else
            {
                if (ImGui.IsKeyReleased(ImGuiKey.Escape))
                {
                    // Use IsKeyReleased instead of KeyPressed, otherwise
                    // Unity would also capture the key press and open the game menu
                    double timeNow = ImGui.GetTime();
                    if (timeNow < _timeLastEscape + 1.0)
                        Editor.HideWindow();
                    _timeLastEscape = timeNow;
                    OnKeyPressed("Escape");
                }
            }

            // check for move actions
            TextPosition newPos = new TextPosition(-1, -1);

            var arrowMoveToken = ctrlDown ? MoveToken.WordBeginning : MoveToken.Char;

            if (ImGui.IsKeyPressed(ImGuiKey.LeftArrow))
                newPos = Move(CaretPos, new MoveAction(arrowMoveToken, false, 1));
            if (ImGui.IsKeyPressed(ImGuiKey.RightArrow))
                newPos = Move(CaretPos, new MoveAction(arrowMoveToken, true, 1));
            if (ImGui.IsKeyPressed(ImGuiKey.UpArrow))
                newPos = Move(CaretPos, new MoveAction(MoveToken.Line, false, 1));
            if (ImGui.IsKeyPressed(ImGuiKey.DownArrow))
                newPos = Move(CaretPos, new MoveAction(MoveToken.Line, true, 1));
            if (ImGui.IsKeyPressed(ImGuiKey.Home))
                newPos = new TextPosition(CaretPos.Line, 0);
            if (ImGui.IsKeyPressed(ImGuiKey.End))
                newPos = new TextPosition(CaretPos.Line, Editor.Lines[CaretPos.Line].Length);
            if (ImGui.IsKeyPressed(ImGuiKey.PageUp))
                newPos = Move(CaretPos, new MoveAction(MoveToken.Line, false, 20));
            if (ImGui.IsKeyPressed(ImGuiKey.PageDown))
                newPos = Move(CaretPos, new MoveAction(MoveToken.Line, true, 20));

            if ((bool)newPos)
            {
                if (shiftDown)
                {
                    OnKeyPressed("Shift+Movement - Selecting");
                    if (!(bool)Editor.Selection.Start)
                        Editor.Selection.Start = CaretPos;
                    Editor.Selection.End = newPos;
                }
                else
                {
                    OnKeyPressed("Movement");
                    Editor.Selection.Reset();
                }

                CaretPos = newPos;
            }
        }

        public void HandleInsertMode(bool ctrlDown, bool shiftDown)
        {
            if (ctrlDown)
                return;

            var io = ImGui.GetIO();

            if (VimEnabled && ImGui.IsKeyPressed(ImGuiKey.Escape))
            {
                OnKeyPressed("Escape - to Normal mode");
                Mode = KeyMode.VimNormal;
                CurrentCommand.Reset();
                if (CaretCol > 0)
                    CaretCol--;
                return;
            }

            if (ImGui.IsKeyPressed(ImGuiKey.Backspace))
            {
                OnKeyPressed("Backspace");
                if (Editor.DeleteSelectedCode())
                    return;

                Editor.PushUndoState();
                if (CurrentLine.Length > 0 && CaretCol > 0)
                {
                    CurrentLine = CurrentLine.Remove(CaretCol - 1, 1);
                    CaretCol--;
                }
                else if (CaretCol == 0 && CaretLine > 0)
                {
                    // Merge with previous line
                    int prevLineLength = Editor.Lines[CaretLine - 1].Length;
                    CurrentLine = Editor.Lines[CaretLine - 1] + CurrentLine;
                    Editor.Lines.RemoveAt(CaretLine - 1);
                    Editor.CodeFormatter.RemoveLine(CaretLine - 1);
                    CaretLine--;
                    CaretCol = prevLineLength;
                }
            }

            if (ImGui.IsKeyPressed(ImGuiKey.Delete))
            {
                OnKeyPressed("Del");
                if (Editor.DeleteSelectedCode())
                    return;

                Editor.PushUndoState();
                if (CaretCol < CurrentLine.Length)
                    CurrentLine = CurrentLine.Remove(CaretCol, 1);
                else if (CaretCol == CurrentLine.Length && CaretLine < Editor.Lines.Count - 1)
                {
                    // Merge with next line
                    CurrentLine = CurrentLine + Editor.Lines[CaretLine + 1];
                    Editor.Lines.RemoveAt(CaretLine + 1);
                    Editor.CodeFormatter.RemoveLine(CaretLine);
                }
            }

            if (ImGui.IsKeyPressed(ImGuiKey.Enter))
            {
                OnKeyPressed("Enter");
                if (!VimEnabled)
                    Editor.PushUndoState();
                string newLine = CurrentLine.Substring(CaretCol);
                CurrentLine = CurrentLine.Substring(0, CaretCol);
                Editor.Lines.Insert(CaretLine + 1, newLine);
                Editor.CodeFormatter.InsertLine(CaretLine + 1, newLine);
                CaretPos = new TextPosition(CaretLine + 1, 0);
                Editor.ScrollToCaret += 1; // we need to scroll to caret in the next two frames, one for updating the scroll area, one to actually scroll there
            }

            string input = string.Empty;
            for (int i = 0; i < io.InputQueueCharacters.Size; i++)
            {
                char c = (char)io.InputQueueCharacters[i];
                if (c == '\t')
                    input += "  ";
                else
                    input += c;
            }

            if (input.Length > 0)
            {
                OnKeyPressed($"{input}");
                if (!Editor.DeleteSelectedCode() && !VimEnabled)
                {
                    L.Info($"Pushing undo state for input: {input}");
                    Editor.PushUndoState();
                }

                CurrentLine = CurrentLine.Insert(CaretCol, input);
                CaretCol += input.Length;
            }
        }

        private VimCommand CurrentCommand = new VimCommand();
        private VimCommand LastCommand = new VimCommand();
        private VimCommand LastFindCommand = new VimCommand();

        private void ResetCommandState()
        {
            if (CurrentCommand.IsComplete)
            {
                if (CurrentCommand.Command == "f")
                    LastFindCommand = CurrentCommand;
                else
                    LastCommand = CurrentCommand;
            }
            CurrentCommand.Reset();
        }

        private TextRange LineRange(int line, uint count)
        {
            int endLine = line + (int)count;
            if (endLine > Editor.Lines.Count)
                endLine = Editor.Lines.Count;
            return Editor.Clamp(new TextRange(new TextPosition(line, 0), new TextPosition(endLine, 0)));
        }


        public void CheckCommand()
        {
            if (CurrentCommand.IsComplete)
            {
                CommandStatus = CurrentCommand.Execute(Editor);
                ResetCommandState();
            }
        }

        public void HandleVimNormalMode(bool ctrlDown, bool shiftDown)
        {
            var io = ImGui.GetIO();

            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                // these combos are not captured by ImGui for some reason, so handle them via Unity Input
                if (Input.GetKeyDown(KeyCode.U))
                {
                    OnKeyPressed("Ctrl+U");
                    CaretPos = Move(CaretPos, new MoveAction(MoveToken.Line, false, 20));
                }
                if (Input.GetKeyDown(KeyCode.D))
                {
                    OnKeyPressed("Ctrl+D");
                    CaretPos = Move(CaretPos, new MoveAction(MoveToken.Line, true, 20));
                }
                if (Input.GetKeyDown(KeyCode.R))
                {
                    OnKeyPressed("Ctrl+R");
                    Editor.Redo();
                }
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                CurrentCommand.Reset();
            }

            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                if (CurrentCommand.Command == ":")
                    CurrentCommand.Argument = CurrentCommand.Argument.Length > 0 ? CurrentCommand.Argument.Substring(0, CurrentCommand.Argument.Length - 1) : "";
                else
                    CurrentCommand.Reset();
            }

            if (Input.GetKeyDown(KeyCode.Return))
            {
                L.Info("Enter key in Vim Normal mode, command before: " + CurrentCommand.ToString());
                if (CurrentCommand.Command == ":")
                    CurrentCommand.AddChar('\n');
                else
                {
                    CurrentCommand.Reset();
                    CurrentCommand.AddChar('j');
                }

                L.Info("Enter key in Vim Normal mode, command now: " + CurrentCommand.ToString());

                CheckCommand();
            }

            for (int iChar = 0; iChar < io.InputQueueCharacters.Size; iChar++)
            {
                CommandStatus = "";
                char c = (char)io.InputQueueCharacters[iChar];

                if (ctrlDown)
                    break;

                OnKeyPressed($"{c}");

                if (c == '.')
                {
                    if (LastCommand.IsComplete)
                        CommandStatus = LastCommand.Execute(Editor);
                    continue;
                }

                if (c == ';')
                {
                    if (LastFindCommand.IsComplete)
                        CommandStatus = LastFindCommand.Execute(Editor);
                    continue;
                }


                CurrentCommand.AddChar(c);
                CheckCommand();
            }
        }

        public void HandleInput(bool hasFocus)
        {
            var io = ImGui.GetIO();
            io.ConfigWindowsMoveFromTitleBarOnly = true;
            bool ctrlDown = io.KeyCtrl || Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shiftDown = io.KeyShift || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            HandleMouse(ctrlDown, shiftDown);

            if (!hasFocus)
                return;

            HandleCommon(ctrlDown, shiftDown);

            if (Mode == KeyMode.Insert)
                HandleInsertMode(ctrlDown, shiftDown);
            if (Mode == KeyMode.VimNormal)
                HandleVimNormalMode(ctrlDown, shiftDown);
        }
    }
}
