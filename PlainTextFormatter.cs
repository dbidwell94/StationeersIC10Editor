namespace StationeersIC10Editor
{
    using ImGuiNET;
    using UnityEngine;

    public class PlainTextFormatter : ICodeFormatter
    {
        static PlainTextFormatter()
        {
            CodeFormatters.RegisterFormatter("Plain", () => new PlainTextFormatter());
        }

        public PlainTextFormatter()
        {
        }

        private void DrawRegistersGrid() { }

        public override void DrawStatus(IEditor ed, TextPosition caret) { }

        public override void DrawLine(int lineIndex, string line, TextRange selection = default)
        {
            float charWidth = ImGui.CalcTextSize("M").x;
            Vector2 pos = ImGui.GetCursorScreenPos();

            ImGui
                .GetWindowDrawList()
                .AddText(pos, ColorLineNumber, lineIndex.ToString().PadLeft(3) + ".");
            pos.x += LineNumberOffset * charWidth;
            int selectionMin = -1,
                selectionMax = -1;

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

                    Vector2 selStart = new Vector2(pos.x + (charWidth * selectionMin), pos.y);
                    Vector2 selEnd = new Vector2(
                        pos.x + (charWidth * selectionMax),
                        pos.y + lineHeight);

                    ImGui
                        .GetWindowDrawList()
                        .AddRectFilled(selStart, selEnd, ColorSelection);
                }
            }

            ImGui.GetWindowDrawList()
                    .AddText(pos, ColorDefault, line);
        }

        public override void ResetCode(string code)
        { }

        public override void AppendLine(string line)
        {
        }

        public override void InsertLine(int index, string line)
        { }

        public override void RemoveLine(int index)
        { }


        public override string GetAutocompleteSuggestion()
        {
            return null;
        }

        public override void DrawAutocomplete(IEditor ed, TextPosition caret, Vector2 pos)
        {
        }

        public override bool DrawTooltip(string line, TextPosition caret, Vector2 pos)
        {
            return false;
        }
    }
}
