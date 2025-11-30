namespace StationeersIC10Editor;

using System;
using System.Collections.Generic;

using ImGuiNET;

using UnityEngine;

using static Settings;

/// <summary>
/// Line acts as the Source Buffer for a single line of code.
/// It holds the raw string and a list of SemanticTokens pointing to ranges within that string.
/// </summary>
public class Line
{
    private string _content;
    public string Text
    {
        get => _content;
        set => _content = value ?? string.Empty;
    }

    public List<SemanticToken> Tokens { get; private set; } = new List<SemanticToken>();

    public int Length => _content.Length;

    public Line(string text)
    {
        _content = text ?? string.Empty;
    }

    public void ClearTokens()
    {
        Tokens.Clear();
    }

    public void AddToken(SemanticToken token)
    {
        Tokens.Add(token);
    }

    // Helper to find a token at a specific column
    public SemanticToken? GetTokenAt(int column)
    {
        // Simple linear search. For very long lines, binary search could be used if tokens are sorted.
        foreach (var token in Tokens)
        {
            if (column >= token.Column && column < token.Column + token.Length)
                return token;
        }
        return null;
    }

    public void Draw(Vector2 pos, int lineIndex)
    {
        var drawList = ImGui.GetWindowDrawList();
        float startX = pos.x;

        // 1. Draw Backgrounds first
        foreach (var token in Tokens)
        {
            if (token.Background != 0)
            {
                var tokenPos = new Vector2(startX + CharWidth * token.Column, pos.y);
                drawList.AddRectFilled(
                    tokenPos,
                    new Vector2(tokenPos.x + CharWidth * token.Length, tokenPos.y + LineHeight),
                    token.Background
                );
            }
        }

        // 2. Draw Text
        // We iterate through the tokens to draw colored segments.
        // Text *not* covered by a token is drawn with the default color.

        int currentDrawIndex = 0;

        // Ensure tokens are sorted by column for drawing order
        Tokens.Sort((a, b) => a.Column.CompareTo(b.Column));

        foreach (var token in Tokens)
        {
            // Draw gap before token (if any)
            if (token.Column > currentDrawIndex)
            {
                int len = token.Column - currentDrawIndex;
                if (currentDrawIndex + len <= _content.Length)
                {
                    string segment = _content.Substring(currentDrawIndex, len);
                    drawList.AddText(
                        new Vector2(startX + CharWidth * currentDrawIndex, pos.y),
                        ICodeFormatter.ColorDefault,
                        segment
                    );
                }
            }

            // Draw token
            if (token.Column + token.Length <= _content.Length)
            {
                string segment = _content.Substring(token.Column, token.Length);
                drawList.AddText(
                    new Vector2(startX + CharWidth * token.Column, pos.y),
                    token.Color,
                    segment
                );
            }

            currentDrawIndex = token.Column + token.Length;
        }

        // Draw remaining text after last token
        if (currentDrawIndex < _content.Length)
        {
            string segment = _content.Substring(currentDrawIndex);
            drawList.AddText(
                new Vector2(startX + CharWidth * currentDrawIndex, pos.y),
                ICodeFormatter.ColorDefault,
                segment
            );
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
        get { return string.Join("\n", this.ConvertAll(line => line.Text)); }
    }

    public SemanticToken? GetTokenAtPosition(TextPosition pos)
    {
        if (pos.Line < 0 || pos.Line >= Count)
            return null;
        return this[pos.Line].GetTokenAt(pos.Col);
    }

    public void Draw(Vector2 pos)
    {
        for (int i = 0; i < Count; i++)
        {
            this[i].Draw(pos, i);
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

    public FormattedText Lines = new FormattedText();
    public string RawText => Lines.RawText;
    public Line CurrentLine
    {
        get
        {
            if (_lastCaretPos.Line >= 0 && _lastCaretPos.Line < Lines.Count)
                return Lines[_lastCaretPos.Line];
            return null;
        }
    }
    public string Name = "";

    protected FormattedText _status = null;
    protected FormattedText _autocomplete = null;
    protected FormattedText _tooltip = null;
    protected TextPosition _lastCaretPos = new TextPosition(-1, -1);
    protected Vector2 _lastMousePos = new Vector2(-1, -1);
    public IEditor Editor;

    public Vector2 MousePos => _lastMousePos;
    public TextPosition CaretPos => _lastCaretPos;

    public FormattedText Status => _status;
    public FormattedText Tooltip => _tooltip;

    public Action OnCodeChanged = () => { };

    public abstract Line ParseLine(string line);

    public ICodeFormatter()
    {
        OnCodeChanged += () =>
        {
            _status = null;
            _autocomplete = null;
            _tooltip = null;
        };
    }

    public static uint ColorFromHTML(string htmlColor)
    {
        if (string.IsNullOrWhiteSpace(htmlColor))
            return ColorDefault;

        if (htmlColor.StartsWith("0x"))
            htmlColor = htmlColor.Substring(2);
        if (!htmlColor.StartsWith("#"))
        {
            // Simple Color Map fallback
            switch (htmlColor.ToLower())
            {
                case "blue":
                    return 0xFFFF0000; // ImGui is ABGR/RGBA depending on backend, keeping logic same as before
                case "red":
                    return 0xFF0000FF;
                case "green":
                    return 0xFF00FF00;
                case "white":
                    return 0xFFFFFFFF;
                case "black":
                    return 0xFF000000;
                case "yellow":
                    return 0xFF00FFFF;
                case "orange":
                    return 0xFF2B66FF;
                case "purple":
                    return 0xFF800080;
                case "gray":
                case "grey":
                    return 0xFF808080;
                default:
                    return ColorDefault;
            }
        }
        htmlColor = htmlColor.TrimStart('#');
        if (
            uint.TryParse(
                htmlColor,
                System.Globalization.NumberStyles.HexNumber,
                null,
                out uint rgb
            )
        )
        {
            byte a = 0xFF;
            if (htmlColor.Length > 6)
            {
                a = (byte)(rgb & 0xFF);
                rgb = rgb >> 8;
            }
            byte r = (byte)((rgb >> 16) & 0xFF);
            byte g = (byte)((rgb >> 8) & 0xFF);
            byte b = (byte)(rgb & 0xFF);
            return ((uint)a << 24) | ((uint)b << 16) | ((uint)g << 8) | r;
        }
        return ColorDefault;
    }

    public virtual void ReplaceLine(int index, string newLine)
    {
        RemoveLine(index);
        InsertLine(index, newLine);
    }

    public virtual void AppendLine(string line)
    {
        Lines.Add(ParseLine(line));
    }

    public virtual void InsertLine(int index, string line)
    {
        Lines.Insert(index, ParseLine(line));
    }

    public virtual void RemoveLine(int index)
    {
        Lines.RemoveAt(index);
    }

    public virtual void ResetCode(string code)
    {
        var lines = code.Split('\n');
        Lines.Clear();
        foreach (var line in lines)
            Lines.Add(ParseLine(line));
        OnCodeChanged();
    }

    public virtual void DrawLine(int lineIndex, TextRange selection, bool drawLineNumber = true)
    {
        Vector2 pos = ImGui.GetCursorScreenPos();
        var drawList = ImGui.GetWindowDrawList();

        if (drawLineNumber)
        {
            drawList.AddText(
                pos,
                ICodeFormatter.ColorLineNumber,
                lineIndex.ToString().PadLeft(3) + "."
            );
            pos.x += ICodeFormatter.LineNumberOffset * CharWidth;
        }

        int selectionMin = -1,
            selectionMax = -1;
        Line line = Lines[lineIndex];

        // Calculate Selection Rect
        if (selection)
        {
            float lineHeight = ImGui.GetTextLineHeightWithSpacing();
            if (selection.Start.Line <= lineIndex && selection.End.Line >= lineIndex)
            {
                selectionMin = lineIndex == selection.Start.Line ? selection.Start.Col : 0;
                selectionMax = lineIndex == selection.End.Line ? selection.End.Col : line.Length;

                selectionMin = Mathf.Clamp(selectionMin, 0, line.Length);
                selectionMax = Mathf.Clamp(selectionMax, 0, line.Length);

                Vector2 selStart = new Vector2(pos.x + (CharWidth * selectionMin), pos.y);
                Vector2 selEnd = new Vector2(
                    pos.x + (CharWidth * selectionMax),
                    pos.y + lineHeight
                );

                drawList.AddRectFilled(selStart, selEnd, ICodeFormatter.ColorSelection);
            }
        }

        // Draw the actual line content using its SemanticTokens
        line.Draw(pos, lineIndex);
    }

    public virtual string Compile() => RawText;

    public virtual void UpdateTooltip(TextPosition mouseTextPos)
    {
        _tooltip = null;
        if (!(bool)mouseTextPos)
            return;

        var token = Lines.GetTokenAtPosition(mouseTextPos);
        if (token == null)
            return;
        if (string.IsNullOrEmpty(token.Value.Data))
            return;

        var tooltip = new FormattedText();
        // Assuming Data holds error or tooltip info.
        // If it's an error token, we color red, else default/white.
        uint color = token.Value.IsError ? ColorError : ColorDefault;

        // Simple handling: if Data contains newlines, create multiple lines
        foreach (var lineStr in token.Value.Data.Split('\n'))
        {
            var l = new Line(lineStr);
            // We add a "fake" token just to color the tooltip text
            l.AddToken(new SemanticToken(0, 0, lineStr.Length, color, 0));
            tooltip.Add(l);
        }

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
        if (string.IsNullOrEmpty(token.Value.Data))
            return;

        var status = new FormattedText();
        uint color = token.Value.IsError ? ColorError : ColorDefault;
        foreach (var lineStr in token.Value.Data.Split('\n'))
        {
            var l = new Line(lineStr);
            l.AddToken(new SemanticToken(0, 0, lineStr.Length, color, 0));
            status.Add(l);
        }
        _status = status;
    }

    public virtual void UpdateAutocomplete()
    {
        _autocomplete = null;
    }

    public virtual void PerformAutocomplete()
    {
    }

    public virtual void Update(TextPosition caretPos, Vector2 mousePos, TextPosition mouseTextPos)
    {
        if ((bool)caretPos && !caretPos.Equals(_lastCaretPos))
            UpdateStatus();
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
        ImGui.SetNextWindowSize(
            new Vector2(Tooltip.Width, Tooltip.Height) + 2 * ImGui.GetStyle().WindowPadding
        );
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
