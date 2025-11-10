namespace StationeersIC10Editor
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using Assets.Scripts;
    using Assets.Scripts.Objects.Motherboards;
    using BepInEx.Configuration;
    using Assets.Scripts.UI;
    using ImGuiNET;
    using UnityEngine;


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

    public class IC10Editor
    {
        public KeyHandler KeyHandler;

        public static bool IsWordChar(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_';
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
                    pos = FindWordBeginning(startPos, action.Forward);
                return pos;
            }
            if (action.Token == MoveToken.WordEnd)
            {
                var pos = startPos;
                for (int i = 0; i < action.Amount; i++)
                    pos = FindWordEnd(startPos, action.Forward);
                return pos;
            }

            throw new NotImplementedException($"Move not implemented for token {action.Token}");
        }

        public static bool UseNativeEditor = false;

        private ProgrammableChipMotherboard _pcm;
        private string Title = "IC10 Editor";

        public IC10Editor(ProgrammableChipMotherboard pcm)
        {
            CodeFormatter = new IC10.IC10CodeFormatter();
            UndoList = new LinkedList<EditorState>();
            RedoList = new LinkedList<EditorState>();
            Lines = new List<string>();
            Lines.Add("");
            CaretPos = new TextPosition(0, 0);
            CodeFormatter.AppendLine("");
            _pcm = pcm;
            KeyHandler = new KeyHandler(this);

            KeyHandler.OnKeyPressed = (key) =>
            {
                _keyLog.Enqueue(key);
                while (_keyLog.Count > 20)
                    _keyLog.Dequeue();
            };
        }

        public ICodeFormatter CodeFormatter;

        public List<string> Lines;

        public string Code => string.Join("\n", Lines);

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

        public LinkedList<EditorState> UndoList;
        public LinkedList<EditorState> RedoList;
        public int ScrollToCaret = 0;

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

        private bool Show = false;
        private double _timeLastAction = 0.0;

        public void SwitchToNativeEditor()
        {
            UseNativeEditor = true;
            Show = false;

            // localPosition was set to -10000,-10000,0 to hide the native editor, so set it back to 0,0,0 to show it
            InputSourceCode.Instance.Window.localPosition = new Vector3(0, 0, 0);
            KeyManager.RemoveInputState("ic10editorinputstate");
            InputSourceCode.Paste(Code);
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
        public TextPosition _caretPos;

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

        public void Copy()
        {
            string code = SelectedCode;
            if (code != null)
            {
                GameManager.Clipboard = code;
            }
        }

        public void Paste()
        {
            if (!DeleteSelectedCode())
                PushUndoState(false);
            Insert(GameManager.Clipboard);
        }

        public void SetTitle(string title)
        {
            Title = title;
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

        public bool HaveSelection => (bool)Selection;

        public string GetCode(TextRange range)
        {
            var start = range.Start;
            var end = range.End;

            if (start.Line == end.Line)
                return Lines[start.Line].Substring(start.Col, end.Col - start.Col);

            string code = Lines[start.Line].Substring(start.Col);
            for (int i = start.Line + 1; i < end.Line; i++)
                code += '\n' + Lines[i];

            code += '\n' + Lines[end.Line].Substring(0, end.Col);
            return code;
        }

        public string SelectedCode => GetCode(Selection.Sorted());

        public bool DeleteRange(TextRange range)
        {
            L.Info($"DeleteRange: {range.Start.Line},{range.Start.Col} - {range.End.Line},{range.End.Col}");
            if (!(bool)range)
                return false;

            L.Info("Valid range");

            range = range.Sorted();

            L.Info($"SortedRange: {range.Start.Line},{range.Start.Col} - {range.End.Line},{range.End.Col}");

            PushUndoState(false);

            var start = range.Start;
            var end = range.End;
            if (start.Line == end.Line)
            {
                string line = Lines[start.Line];
                string newLine =
                    line.Substring(0, start.Col) + line.Substring(end.Col, line.Length - end.Col);
                ReplaceLine(start.Line, newLine);
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

            var startPos = FindWordBeginning(pos, !isWordChar);
            var endPos = FindWordEnd(pos, isWordChar);

            return new TextRange(startPos, endPos);
        }

        public void HandleInput(bool hasFocus)
        {
            KeyHandler.HandleInput(hasFocus);
        }

        public static Vector2 buttonSize => Scale * new Vector2(85, 0);
        public static Vector2 smallButtonSize => Scale * new Vector2(50, 0);

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
                ClearCode();

            ImGui.SameLine();

            if (ImGui.Button("Copy", buttonSize))
                GameManager.Clipboard = Code;

            ImGui.SameLine();

            if (ImGui.Button("Paste", buttonSize))
            {
                ClearCode();
                Insert(GameManager.Clipboard);
            }

            ImGui.SameLine();

            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 2 * ImGui.GetStyle().ItemSpacing.x);

            if (ImGui.Button("?", smallButtonSize))
                _helpWindowVisible = !_helpWindowVisible;

            ImGui.SameLine();

            if (ImGui.Button("Native", buttonSize))
                SwitchToNativeEditor();

            ImGui.SameLine();

            ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 3 * smallButtonSize.x - buttonSize.x - ImGui.GetStyle().FramePadding.x * 3 - ImGui.GetStyle().ItemSpacing.x * 3);


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

        public void DrawFooter()
        {
            ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 5.0f);
            var sLines = $"{Lines.Count}".PadLeft(3, ' ');
            var sBytes = $"{Code.Length}".PadLeft(4, ' ');
            var sLine = $"{CaretLine}".PadLeft(3, ' ');
            var sCol = $"{CaretCol}".PadLeft(2, ' ');
            ImGui.Text($"Caret: ({sLine},{sCol}), ");
            ImGui.SameLine();

            var pos = ImGui.GetCursorScreenPos();
            var charWidth = ImGui.CalcTextSize("M").x;

            sLines = $"{sLines}/128 lines ";
            sBytes = $"{sBytes}/4096 bytes";
            uint colorGood = ICodeFormatter.ColorFromHTML("green");
            uint colorWarning = ICodeFormatter.ColorFromHTML("orange");
            uint colorBad = ICodeFormatter.ColorFromHTML("red");

            uint lineColor = Lines.Count < 120 ? colorGood : (Lines.Count <= 128 ? colorWarning : colorBad);
            uint byteColor = Code.Length < 4000 ? colorGood : (Code.Length <= 4096 ? colorWarning : colorBad);

            var drawList = ImGui.GetWindowDrawList();
            drawList.AddText(pos, lineColor, sLines);
            pos.x += sLines.Length * charWidth;
            drawList.AddText(pos, byteColor, sBytes);

            ImGui.SameLine();

            ImGui.SetCursorPosX(ImGui.GetWindowWidth() - 3 * buttonSize.x - ImGui.GetStyle().FramePadding.x * 3 - ImGui.GetStyle().ItemSpacing.x);
            if (ImGui.Button("Cancel", buttonSize))
                HideWindow();

            ImGui.SameLine();

            bool exportDisabled = (EnforceLineLimit && Lines.Count > 128) || (EnforceByteLimit && Code.Length > 4096);

            if (exportDisabled)
            {
                ImGui.PushItemFlag(ImGuiItemFlags.Disabled, true);
                ImGui.PushStyleColor(ImGuiCol.Button, ICodeFormatter.ColorFromHTML("gray"));
            }

            if (ImGui.Button("Export", buttonSize))
                Export();

            ImGui.SameLine();
            if (ImGui.Button("Confirm", buttonSize))
                Confirm();

            if (exportDisabled)
            {
                ImGui.PopItemFlag();
                ImGui.PopStyleColor();
            }

            KeyHandler.DrawStatus();
            CodeFormatter.DrawStatus();

            ImGui.PopStyleVar();
        }

        public static bool VimEnabled => IC10EditorPlugin.VimBindings.Value;
        public static bool EnforceLineLimit => IC10EditorPlugin.EnforceLineLimit.Value;
        public static bool EnforceByteLimit => IC10EditorPlugin.EnforceByteLimit.Value;
        public static bool PauseOnOpen => IC10EditorPlugin.PauseOnOpen.Value;
        public static float TooltipDelay => IC10EditorPlugin.TooltipDelay.Value;
        public static float Scale => Mathf.Clamp(IC10EditorPlugin.ScaleFactor.Value, 0.25f, 5.0f);

        public void Confirm()
        {
            _pcm.InputFinished(Code);
            HideWindow();
        }

        public void Export()
        {
            Confirm();
            _pcm.Export();
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

            if (ScrollToCaret > 0)
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
                ScrollToCaret -= 1;
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

            clipper.End();
            ImGui.EndChild();

        }

        public void DrawCaret(Vector2 pos)
        {
            var drawList = ImGui.GetWindowDrawList();
            var lineHeight = ImGui.GetTextLineHeight();
            var lineHeight2 = ImGui.GetTextLineHeightWithSpacing();

            pos.y -= (lineHeight2 - lineHeight) / 2;

            if (KeyHandler.Mode == KeyMode.Insert)
            {
                bool blinkOn = ((int)((ImGui.GetTime() - _timeLastAction) * 2) % 2) == 0;
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
            }
        }

        private bool _hasFocus = false;
        private int _openGameWindows = 0;
        private Vector2 _windowPos = new Vector2(100, 100);
        private bool _didGameWindowOpen = false;

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
            ImGui.SetWindowFontScale(Scale);
            DrawHeader();

            ImGui.PushFont(ImGui.GetIO().Fonts.Fonts[0]);
            _windowPos = ImGui.GetWindowPos();

            HandleInput(_hasFocus);

            _hasFocus = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);

            DrawCodeArea();
            ImGui.PopFont();

            DrawFooter();


            ImGui.End();
            ImGui.PopStyleColor();

            if (_hasFocus && IsMouseInsideTextArea())
            {
                ImGui.PushFont(ImGui.GetIO().Fonts.Fonts[0]);
                if (KeyHandler.IsMouseIdle(TooltipDelay / 1000.0f))
                {
                    var pos = GetTextPositionFromMouse(false);
                    ImGui.PushFont(ImGui.GetIO().Fonts.Fonts[0]);
                    if (pos)
                        CodeFormatter.DrawTooltip(Lines[CaretLine], pos, _caretPixelPos);
                    ImGui.PopFont();
                }
                ImGui.PopFont();
            }

            DrawHelpWindow();
            DrawDebugWindow();

            ImGui.PopFont();


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
            ImGui.Checkbox("Show debug window", ref _debugWindowVisible);

            ImGui.Separator();

            ImGui.TextWrapped(
                "\nKeyboard Shortcuts:\n" +
                "\n" +
                "Arrow Keys     Move caret\n" +
                "Home/End       Move caret to start/end of line\n" +
                "Page Up/Down   Move caret up/down by 20 lines\n" +
                "Shift+Arrow    Select text while moving caret\n" +
                "2 * Escape     Cancel\n" +
                "Ctrl+S         Save and confirm changes\n" +
                "Ctrl+E         Save + export code to ic chip\n" +
                "Ctrl+Z         Undo\n" +
                "Ctrl+Y         Redo\n" +
                "Ctrl+C         Copy selected code\n" +
                "Ctrl+V         Paste code from clipboard\n" +
                "Ctrl+A         Select all code\n" +
                "Ctrl+X         Cut selected code\n" +
                "Ctrl+Arrow     Move caret by word\n" +
                "Ctrl+Click     Open Stationpedia page of word at cursor\n\n"
                );

            ImGui.Separator();

            ImGui.TextWrapped(
                "\nNotes:\n" +
                "\n" +
                "Closing the editor via escape key or Cancel button will not ask for confirmation, BUT you can always reopen the editor and Undo (Ctrl+Z) to get the state before cancelling.\n"
                );

            ImGui.End();
            ImGui.PopStyleColor();
        }

        private Queue<string> _keyLog = new Queue<string>();

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


            foreach (var key in _keyLog.Reverse())
                ImGui.Text(key);

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
