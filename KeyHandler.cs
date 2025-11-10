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
            return Editor.Move(pos, action);
        }

        public bool IsMouseIdle(double idleTime)
        {
            return (ImGui.GetTime() - _timeLastMouseMove) >= idleTime;
        }


        public void DrawStatus()
        {
            if (VimEnabled)
            {
                String status = $"Mode: {Mode}";
                ImGui.Text(status);
                ImGui.SameLine();
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
                input += c;
            }

            if (input.Length > 0)
            {
                OnKeyPressed($"{input}");
                if (!Editor.DeleteSelectedCode())
                {
                    L.Info($"Pushing undo state for input: {input}");
                    Editor.PushUndoState();
                }

                CurrentLine = CurrentLine.Insert(CaretCol, input);
                CaretCol += input.Length;
            }
        }

        public void HandleVimNormalMode(bool ctrlDown, bool shiftDown)
        {
            var io = ImGui.GetIO();

            if (ImGui.IsKeyPressed(ImGuiKey.U))
            {
                OnKeyPressed("U - Undo");
            }

            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
            {
                // these combos are not captured by ImGui for some reason, so handle them via Unity Input
                if (Input.GetKeyDown(KeyCode.U))
                {
                    OnKeyPressed("Ctrl+U - Move up 20 lines");
                    CaretPos = Move(CaretPos, new MoveAction(MoveToken.Line, false, 20));
                }
                if (Input.GetKeyDown(KeyCode.D))
                {
                    OnKeyPressed("Ctrl+D - Move down 20 lines");
                    CaretPos = Move(CaretPos, new MoveAction(MoveToken.Line, true, 20));
                }
                if (Input.GetKeyDown(KeyCode.R))
                {
                    OnKeyPressed("Ctrl+R - Redo");
                    Editor.Redo();
                }
            }

            for (int i = 0; i < io.InputQueueCharacters.Size; i++)
            {
                char c = (char)io.InputQueueCharacters[i];

                if (ctrlDown)
                    break;

                OnKeyPressed($"{c}");

                switch (c)
                {
                    case 'I':
                        Mode = KeyMode.Insert;
                        var line = CurrentLine;
                        int col = 0;
                        if (line.Length > 0 && !string.IsNullOrWhiteSpace(line))
                            while (col < line.Length - 1 && char.IsWhiteSpace(line[col]))
                                col++;
                        CaretCol = col;
                        break;
                    case 'A':
                        Mode = KeyMode.Insert;
                        CaretCol = CurrentLine.Length;
                        break;
                    case 'O':
                        Editor.PushUndoState();
                        CaretPos = new TextPosition(CaretLine, 0);
                        Editor.Insert("\n");
                        CaretPos = new TextPosition(CaretLine - 1, 0);
                        Mode = KeyMode.Insert;
                        break;
                    case 'o':
                        Editor.PushUndoState();
                        CaretPos = new TextPosition(CaretLine + 1, 0);
                        Editor.Insert("\n");
                        CaretPos = new TextPosition(CaretLine - 1, 0);
                        Mode = KeyMode.Insert;
                        break;
                    case 'J':
                        if (CaretLine < Editor.Lines.Count - 1)
                        {
                            Editor.PushUndoState();
                            int lengthBefore = CurrentLine.Length;
                            CurrentLine = CurrentLine + Editor.Lines[CaretLine + 1];
                            Editor.RemoveLine(CaretLine + 1);
                            CaretCol = Math.Max(0, Math.Min(lengthBefore, CurrentLine.Length - 1));
                        }
                        break;
                    case 'i':
                        Mode = KeyMode.Insert;
                        break;

                    case 'x':
                        Editor.PushUndoState();
                        CurrentLine = CurrentLine.Remove(CaretCol, 1);
                        break;

                    case 'j':
                        CaretPos = Move(CaretPos, new MoveAction(MoveToken.Line, true, 1));
                        break;
                    case 'k':
                        CaretPos = Move(CaretPos, new MoveAction(MoveToken.Line, false, 1));
                        break;
                    case 'h':
                        CaretPos = Move(CaretPos, new MoveAction(MoveToken.Char, false, 1));
                        break;
                    case 'l':
                        CaretPos = Move(CaretPos, new MoveAction(MoveToken.Char, true, 1));
                        break;
                    case 'G':
                        CaretLine = Math.Max(0, Editor.Lines.Count - 1);
                        break;
                    case '0':
                        CaretCol = 0;
                        break;
                    case '$':
                        CaretCol = Math.Max(0, CurrentLine.Length - 1);
                        break;

                    case 'w':
                        CaretPos = Move(CaretPos, new MoveAction(MoveToken.WordBeginning, true, 1));
                        break;
                    case 'b':
                        CaretPos = Move(CaretPos, new MoveAction(MoveToken.WordBeginning, false, 1));
                        break;
                    case 'D':
                        Editor.PushUndoState(false);
                        CurrentLine = CurrentLine.Substring(0, CaretCol);
                        CaretCol = Math.Max(CaretCol - 1, 0);
                        break;

                    case 'C':
                        Editor.PushUndoState(false);
                        CurrentLine = CurrentLine.Substring(0, CaretCol);
                        Mode = KeyMode.Insert;
                        break;
                    case 'u':
                        Editor.Undo();
                        break;

                    default:
                        break;
                }
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
