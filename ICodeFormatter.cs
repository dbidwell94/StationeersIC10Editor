namespace StationeersIC10Editor
{
    using ImGuiNET;
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    using static Settings;

    public static class CodeFormatters
    {
        private static Dictionary<string, Func<ICodeFormatter>> formatters = new Dictionary<string, Func<ICodeFormatter>>();
        private static string defaultFormatterName = "Plain";

        public static List<string> FormatterNames => new List<string>(formatters.Keys);

        public static void RegisterFormatter(string name, Func<ICodeFormatter> formatter, bool isDefault = false)
        {
            L.Info($"Registering code formatter: {name}");
            if (!formatters.ContainsKey(name))
                formatters.Add(name, formatter);

            formatters[name] = formatter;

            if (isDefault)
                defaultFormatterName = name;
        }

        public static ICodeFormatter GetFormatter(string name = null)
        {
            if (name == null || !formatters.ContainsKey(name))
                return GetFormatter(defaultFormatterName);
            var formatter = formatters[name]();
            formatter.Name = name;
            return formatter;
        }
    }

    public class Token
    {
        public string Text;
        public int Column;
        public uint Color;
        public uint Background;
        public string Tooltip;
        public string Error;
        public string Status;

        public Token(string text, int column, uint color = 0xFFFFFFFF, uint background = 0, string tooltip = null, string error = null)
        {
            Text = text;
            Column = column;
            Color = color;
            Background = background;
            Tooltip = tooltip;
            Error = error;
        }
    }

    public class Line : List<Token>
    {
        public int Length => (int)(this.Count == 0 ? 0 : this[this.Count - 1].Column + (uint)this[this.Count - 1].Text.Length);

        public Line()
          : base()
        { }
        public Line(string text, uint color = 0xFFFFFFFF)
          : base()
        {
            // By default, create a single token with the entire line
            Add(new Token(text, 0, color));
        }

        public string Text
        {
            get
            {
                char[] text = new char[Length];
                for (int i = 0; i < text.Length; i++)
                    text[i] = ' ';
                foreach (var token in this)
                {
                    for (int i = 0; i < token.Text.Length; i++)
                    {
                        text[token.Column + (uint)i] = token.Text[i];
                    }
                }
                return new string(text);
            }
        }

        public Token GetTokenAtColumn(int caretCol)
        {
            for (int i = 0; i < Count; i++)
            {
                var token = this[i];
                if (caretCol >= token.Column && caretCol < token.Column + token.Text.Length)
                    return token;
            }
            return null;
        }

        public string GetErrorAtColumn(int caretCol)
        {
            var token = GetTokenAtColumn(caretCol);
            return token?.Error;
        }

        public string GetTooltipAtColumn(int caretCol)
        {
            var token = GetTokenAtColumn(caretCol);
            return token?.Tooltip;
        }

        public string GetStatusAtColumn(int caretCol)
        {
            var token = GetTokenAtColumn(caretCol);
            return token?.Status;
        }

        public void Draw(Vector2 pos)
        {
            var drawList = ImGui.GetWindowDrawList();
            float x0 = pos.x;
            foreach (var token in this)
            {
                pos.x = x0 + (CharWidth * token.Column);
                L.Info($"Drawing token '{token.Text}' at ({pos.x}, {pos.y}) with color {token.Color:X8}");
                drawList.AddText(pos, token.Color, token.Text);
            }
        }

    }

    public class FormattedText : List<Line>
    {
        public float Width
        {
            get
            {
                int width = 0;
                foreach (var line in this)
                    if (line.Length > width)
                        width = line.Length;
                return width * CharWidth;
            }
        }

        public float Height => Count * LineHeight;

        public string RawText
        {
            get
            {
                return string.Join("\n", this.ConvertAll(line => line.Text));
            }
        }

        public Token GetTokenAtPosition(TextPosition pos)
        {
            if (pos.Line < 0 || pos.Line >= Count)
                return null;
            return this[pos.Line].GetTokenAtColumn(pos.Col);
        }

        public void Draw(Vector2 pos)
        {
            foreach (var line in this)
            {
                line.Draw(pos);
                pos.y += LineHeight;
            }
        }
    }

    public abstract class ICodeFormatter
    {
        public static uint ColorError = ColorFromHTML("#ff0000");
        public static uint ColorWarning = ColorFromHTML("#ff8f00");
        public static uint ColorComment = ColorFromHTML("#808080");
        public static uint ColorLineNumber = ColorFromHTML("#808080");
        public static uint ColorDefault = ColorFromHTML("#ffffff");
        public static uint ColorSelection = ColorFromHTML("#1a44b0ff");
        public static uint ColorNumber = ColorFromHTML("#20b2aa");
        public static float LineNumberOffset = 5.3f;

        public FormattedText Lines { get; protected set; } = new FormattedText();
        public string RawText => Lines.RawText;
        public string Name = "";

        protected FormattedText _status = null;
        protected FormattedText _autocomplete = null;
        protected FormattedText _tooltip = null;
        protected TextPosition _lastCaretPos = new TextPosition(-1, -1);
        protected Vector2 _lastMousePos = new Vector2(-1, -1);

        public Vector2 MousePos => _lastMousePos;
        public TextPosition CaretPos => _lastCaretPos;

        public FormattedText Status => _status;
        public FormattedText Tooltip => _tooltip;

        // this will be triggered by the editor after a (batch of) changes
        public Action OnCodeChanged = delegate { };

        public abstract Line ParseLine(string line);

        public ICodeFormatter()
        {
        }

        public static uint ColorFromHTML(string htmlColor)
        {
            if (string.IsNullOrWhiteSpace(htmlColor))
            {
                L.Warning("ColorFromHTML: empty color string");
                return ColorDefault;
            }

            if (htmlColor.StartsWith("0x"))
                htmlColor = htmlColor.Substring(2);
            if (!htmlColor.StartsWith("#"))
            {
                if (htmlColor == "blue")
                    return ColorFromHTML("#0000FF");
                if (htmlColor == "red")
                    return ColorFromHTML("#FF0000");
                if (htmlColor == "green")
                    return ColorFromHTML("#00FF00");
                if (htmlColor == "white")
                    return ColorFromHTML("#FFFFFF");
                if (htmlColor == "black")
                    return ColorFromHTML("#000000");
                if (htmlColor == "yellow")
                    return ColorFromHTML("#FFFF00");
                if (htmlColor == "orange")
                    return ColorFromHTML("#FF662B");
                if (htmlColor == "purple")
                    return ColorFromHTML("#800080");
                if (htmlColor == "gray" || htmlColor == "grey")
                    return ColorFromHTML("#808080");
                if (htmlColor == "brown")
                    return ColorFromHTML("#633C2B");
                if (htmlColor == "pink")
                    return ColorFromHTML("#E41C99");
                if (htmlColor == "khaki")
                    return ColorFromHTML("#63633F");

                L.Warning($"ColorFromHTML - unknown color: {htmlColor}");
                return ColorDefault;
            }
            htmlColor = htmlColor.TrimStart('#');
            uint rgb = uint.Parse(htmlColor, System.Globalization.NumberStyles.HexNumber);
            byte a = 0xFF;

            if (rgb > 0xFFFFFF)
            {
                a = (byte)(rgb & 0xFF);
                rgb = rgb >> 8;
            }

            byte r = (byte)((rgb >> 16) & 0xFF);
            byte g = (byte)((rgb >> 8) & 0xFF);
            byte b = (byte)(rgb & 0xFF);

            return ((uint)a << 24) | ((uint)b << 16) | ((uint)g << 8) | r;
        }

        virtual public void ReplaceLine(int index, string newLine)
        {
            RemoveLine(index);
            InsertLine(index, newLine);
        }

        virtual public void AppendLine(string line)
        {
            Lines.Add(ParseLine(line));
        }

        virtual public void InsertLine(int index, string line)
        {
            Lines.Insert(index, ParseLine(line));
        }

        virtual public void RemoveLine(int index)
        {
            Lines.RemoveAt(index);
        }

        virtual public void ResetCode(string code)
        {
            var lines = code.Split('\n');
            Lines.Clear();
            foreach (var line in lines)
                Lines.Add(ParseLine(line));
        }

        virtual public void DrawLine(int lineIndex, TextRange selection)
        {
            Vector2 pos = ImGui.GetCursorScreenPos();

            var drawList = ImGui.GetWindowDrawList();

            drawList.AddText(pos, ICodeFormatter.ColorLineNumber, lineIndex.ToString().PadLeft(3) + ".");
            pos.x += ICodeFormatter.LineNumberOffset * CharWidth;
            int selectionMin = -1,
                selectionMax = -1;

            Line line = Lines[lineIndex];

            foreach (var token in line)
                if (token.Background != 0)
                {
                    var tokenPos = new Vector2(pos.x + CharWidth * token.Column, pos.y);
                    drawList.AddRectFilled(
                        tokenPos,
                        new Vector2(tokenPos.x + CharWidth * token.Text.Length,
                                    tokenPos.y + LineHeight),
                        token.Background);
                }

            if (selection)
            {
                float lineHeight = ImGui.GetTextLineHeightWithSpacing();
                if (selection.Start.Line <= lineIndex && selection.End.Line >= lineIndex)
                {
                    selectionMin = lineIndex == selection.Start.Line ? selection.Start.Col : 0;
                    selectionMax =
                        lineIndex == selection.End.Line ? selection.End.Col : line.Length;

                    selectionMin = Mathf.Clamp(selectionMin, 0, line.Length);
                    selectionMax = Mathf.Clamp(selectionMax, 0, line.Length);

                    Vector2 selStart = new Vector2(pos.x + (CharWidth * selectionMin), pos.y);
                    Vector2 selEnd = new Vector2(
                        pos.x + (CharWidth * selectionMax),
                        pos.y + lineHeight);

                    drawList.AddRectFilled(selStart, selEnd, ICodeFormatter.ColorSelection);
                }
            }

            float x0 = pos.x;
            foreach (var token in line)
            {
                pos.x = x0 + (CharWidth * token.Column);
                drawList.AddText(pos, token.Color, token.Text);
            }
        }

        // Return compiled code (in case the formatter modifies it)
        public virtual string Compile()
        {
            return RawText;
        }


        public virtual void UpdateTooltip(TextPosition mouseTextPos)
        {
            _tooltip = null;

            if (!(bool)mouseTextPos)
                return;

            var token = Lines.GetTokenAtPosition(mouseTextPos);

            if (token == null)
                return;

            var tooltip = new FormattedText();

            if (!string.IsNullOrEmpty(token?.Error))
                tooltip.Add(new Line(token.Error, ColorError));

            if (!string.IsNullOrEmpty(token?.Tooltip))
                tooltip.Add(new Line(token.Tooltip));

            _tooltip = tooltip;
        }

        public virtual void UpdateStatus()
        {
            _status = null;

            if (!(bool)CaretPos)
                return;

            var token = Lines.GetTokenAtPosition(CaretPos);

            if (token == null)
                return;

            var status = new FormattedText();

            if (!string.IsNullOrEmpty(token?.Error))
                status.Add(new Line(token.Error, ColorError));

            if (!string.IsNullOrEmpty(token?.Status))
                status.Add(new Line(token.Status));

            _status = status;
        }

        public virtual void UpdateAutocomplete()
        {
            _autocomplete = null;
        }

        public virtual void Update(TextPosition caretPos, Vector2 mousePos, TextPosition mouseTextPos)
        {
            if ((bool)caretPos && !caretPos.Equals(_lastCaretPos))
            {
                UpdateStatus();
            }
            if (!mousePos.Equals(_lastMousePos))
            {
                UpdateTooltip(mouseTextPos);
                UpdateAutocomplete();
            }
            _lastCaretPos = caretPos;
            _lastMousePos = mousePos;
        }

        public virtual void DrawStatus(Vector2 pos)
        {
            if (Status != null)
                Status.Draw(pos);
        }

        public virtual void DrawTooltip(Vector2 pos)
        {
            if (Tooltip == null || Tooltip.Count == 0)
                return;

            ImGui.SetNextWindowSize(new Vector2(Tooltip.Width, Tooltip.Height) + 2 * ImGui.GetStyle().WindowPadding);
            ImGui.BeginTooltip();
            Tooltip.Draw(ImGui.GetCursorScreenPos());
            ImGui.EndTooltip();
        }

        public virtual void DrawAutocomplete(Vector2 pos)
        {
            if (_autocomplete != null)
                _autocomplete.Draw(pos);
        }
    }
}
