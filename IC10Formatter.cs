namespace StationeersIC10Editor
{
    using System;
    using System.Text.RegularExpressions;
    using System.Collections.Generic;
    using ImGuiNET;
    using UnityEngine;
    using static Settings;

    namespace IC10
    {
        public class IC10CodeFormatter : ICodeFormatter
        {

            virtual public string TrimToken(string token)
            {
                return token.TrimEnd(':');
            }


            private void DrawRegistersGrid()
            {
                // todo: store this information, update only when code changes
                bool[] registers = new bool[16];
                for (int i = 0; i < 16; i++)
                    registers[i] = false;

                foreach (var line in Lines)
                    foreach (var token in line)
                        if (IC10Utils.Registers.Contains(token.Text) && token.Text.StartsWith("r") && token.Text != "ra")
                        {
                            var reg = token.Text;
                            while (reg.StartsWith("rr") && reg.Length > 3)
                                reg = reg.Substring(1);
                            if (int.TryParse(reg.Substring(1), out int regNum))
                            {
                                if (regNum >= 0 && regNum < 16)
                                    registers[regNum] = true;
                                else L.Warning($"Register number out of range: {reg}");
                            }
                            else L.Warning($"Failed to parse register number: {reg}");
                            registers[int.Parse(reg.Substring(1))] = true;
                        }

                var drawList = ImGui.GetWindowDrawList();

                Vector2 startPos = ImGui.GetCursorScreenPos();
                Vector2 windowSize = ImGui.GetWindowSize();

                Vector2 rectSize = new Vector2(9, 9) * Settings.Scale;
                float spacing = 4.0f * Settings.Scale;

                startPos.x = ImGui.GetWindowPos().x + ImGui.GetWindowWidth() - 3 * Settings.buttonSize.x - ImGui.GetStyle().FramePadding.x * 3 - ImGui.GetStyle().ItemSpacing.x;
                startPos.y += 8.0f;

                uint colorUsed = ImGui.GetColorU32(new Vector4(1.0f, 1.0f, 0.0f, 1.0f));
                uint colorFree = ImGui.GetColorU32(new Vector4(0.0f, 1.0f, 0.0f, 1.0f));

                float x0 = startPos.x;

                for (int i = 0; i < 16; i++)
                {
                    uint color = registers[i] ? colorUsed : colorFree;
                    int xShift = i + i / 4; // add extra space every 4 registers
                    startPos.x = x0 + xShift * (rectSize.x + spacing);
                    drawList.AddCircleFilled(startPos + rectSize / 2, rectSize.x / 2, color, 12);
                }
            }

            // public override void DrawStatus(IEditor ed, TextPosition caret)
            // {
            //     var status = Lines[caret.Line].GetStatusText(caret.Col);
            //     if (!string.IsNullOrEmpty(status))
            //     {
            //         var color = status.StartsWith("Error") ? ColorError : ColorWarning;
            //         ImGui.SameLine();
            //         ImGui.PushStyleColor(ImGuiCol.Text, color);
            //         ImGui.Text(status);
            //         ImGui.PopStyleColor();
            //         ImGui.SameLine();
            //     }
            //     DrawRegistersGrid();
            // }
            //
            // public override void DrawLine(int lineIndex, string line, TextRange selection = default)
            // {
            //     if (lineIndex < 0 || lineIndex >= Lines.Count)
            //         return;
            //     var tokens = Lines[lineIndex];
            //     Vector2 pos = ImGui.GetCursorScreenPos();
            //     ImGui
            //         .GetWindowDrawList()
            //         .AddText(pos, ColorLineNumber, lineIndex.ToString().PadLeft(3) + ".");
            //
            //     pos.x += LineNumberOffset * CharWidth;
            //
            //     int selectionMin = -1,
            //         selectionMax = -1;
            //
            //     foreach (var token in tokens)
            //         if (token.Background != 0)
            //         {
            //             var tokenPos = new Vector2(pos.x + CharWidth * token.Column, pos.y);
            //             ImGui.GetWindowDrawList().AddRectFilled(
            //                 tokenPos,
            //                 new Vector2(tokenPos.x + CharWidth * token.Text.Length,
            //                             tokenPos.y + LineHeight),
            //                 token.Background);
            //         }
            //
            //     if (selection)
            //     {
            //         if (selection.Start.Line <= lineIndex && selection.End.Line >= lineIndex)
            //         {
            //             selectionMin = lineIndex == selection.Start.Line ? selection.Start.Col : 0;
            //             selectionMax =
            //                 lineIndex == selection.End.Line ? selection.End.Col : line.Length;
            //
            //             selectionMin = Mathf.Clamp(selectionMin, 0, line.Length);
            //             selectionMax = Mathf.Clamp(selectionMax, 0, line.Length);
            //
            //             Vector2 selStart = new Vector2(pos.x + (CharWidth * selectionMin), pos.y);
            //             Vector2 selEnd = new Vector2(
            //                 pos.x + (CharWidth * selectionMax),
            //                 pos.y + LineHeight);
            //
            //             ImGui
            //                 .GetWindowDrawList()
            //                 .AddRectFilled(selStart, selEnd, ColorSelection);
            //         }
            //     }
            //
            //     foreach (var token in tokens)
            //     {
            //         var tokenPos = new Vector2(pos.x + CharWidth * token.Column, pos.y);
            //         ImGui.GetWindowDrawList().AddText(tokenPos, token.Color, token.Text);
            //     }
            //
            // }

            public static uint GetBackgroundColor(IC10Token token)
            {
                if (token.DataType != DataType.Color)
                    return 0;

                return IC10Utils.Colors.TryGetValue(token.Text, out uint color) ? color : 0;

            }

            public static uint GetColor(IC10Token token)
            {
                if (token.DataType == DataType.Color)
                {
                    if (token.Text == "Color.White" ||
                       token.Text == "Color.Yellow" ||
                       token.Text == "Color.Pink" ||
                       token.Text == "Color.Green")
                        return ColorFromHTML("black");
                    return ColorFromHTML("white");
                }
                switch (token.DataType)
                {
                    case DataType.Number:
                        return ColorNumber;
                    case DataType.Device:
                        return ColorDevice;
                    case DataType.Register:
                        return ColorRegister;
                    case DataType.LogicType:
                    case DataType.LogicSlotType:
                    case DataType.BatchMode:
                        return ColorLogicType;
                    case DataType.Instruction:
                    case DataType.Define:
                    case DataType.Alias:
                        return ColorInstruction;
                    case DataType.Label:
                        return ColorLabel;
                    case DataType.Comment:
                        return ColorComment;
                    case DataType.Unknown:
                        return ColorError;
                    case DataType.BasicEnum:
                        return ColorBasicEnum;
                    default:
                        return ColorDefault;
                }
            }

            public static uint ColorInstruction = ColorFromHTML("#ffff00");

            public static uint ColorDevice = ColorFromHTML("#00ff00");
            public static uint ColorLogicType = ColorFromHTML("#ff8000");
            public static uint ColorRegister = ColorFromHTML("#0080ff");
            public static uint ColorBasicEnum = ColorFromHTML("#20b2aa");

            public static uint ColorDefine = ColorNumber;
            public static uint ColorAlias = ColorFromHTML("#4d4dcc");
            public static uint ColorLabel = ColorFromHTML("#800080");

            private Dictionary<string, DataType> types = new Dictionary<string, DataType>();

            private Dictionary<string, int> defines = new Dictionary<string, int>();
            private Dictionary<string, int> regAliases = new Dictionary<string, int>();
            private Dictionary<string, int> devAliases = new Dictionary<string, int>();
            private Dictionary<string, int> labels = new Dictionary<string, int>();

            public static int FindNextWhitespace(string text, int startIndex)
            {
                bool haveQuote = false;
                while (startIndex < text.Length && (!char.IsWhiteSpace(text[startIndex]) || haveQuote))
                {
                    if (text[startIndex] == '\"')
                        haveQuote = !haveQuote;
                    startIndex++;
                }

                return startIndex;
            }

            public static int FindNextNonWhitespace(string text, int startIndex)
            {
                while (startIndex < text.Length && char.IsWhiteSpace(text[startIndex]))
                    startIndex++;

                return startIndex;
            }

            public override Line ParseLine(string text)
            {
                if (string.IsNullOrEmpty(text))
                    return new IC10Line();

                string comment = "";
                int indexOfComment = text.IndexOf('#');
                if (text.Contains("#"))
                {
                    var index = text.IndexOf('#');
                    comment = text.Substring(index);
                    text = text.Substring(0, index);
                }

                int i = 0;

                var tokens = new IC10Line();

                while (i < text.Length)
                {
                    int start = FindNextNonWhitespace(text, i);
                    int end = FindNextWhitespace(text, start);

                    if (start >= text.Length)
                        break;

                    string token = text.Substring(start, end - start);

                    tokens.Add(new IC10Token(
                        token,
                        start,
                        0));

                    i = end;
                }

                if (!string.IsNullOrEmpty(comment))
                    tokens.Add(new IC10Token(
                        comment,
                        indexOfComment,
                        ColorComment));

                tokens.SetTypes(types);

                foreach (IC10Token token in tokens)
                {
                    L.Debug($"Parsed token: {token.Text}, column: {token.Column}, color: {token.Color}, datatype: {token.DataType}, error: {token.Error}, tooltip: {token.Tooltip}, status: {token.Status}");
                }

                return tokens;
            }


            public override void ResetCode(string code)
            {
                L.Debug("IC10CodeFormatter - Reset");
                defines.Clear();
                regAliases.Clear();
                devAliases.Clear();
                labels.Clear();
                Lines.Clear();

                var lines = code.Split('\n');
                foreach (var line in lines)
                    AppendLine(line);
            }

            public void UpdateDataType(string token)
            {
                L.Debug($"UpdateDataType for token: {token}");
                int count = 0;
                DataType type = DataType.Unknown;

                if (defines.ContainsKey(token))
                {
                    L.Debug($"Token {token} is a define with count {defines[token]}");
                    count += defines[token];
                    type = DataType.Number;
                }

                if (devAliases.ContainsKey(token))
                {
                    L.Debug($"Token {token} is a device alias with count {devAliases[token]}");
                    count += 1; // multiple aliaes are allowed, thus only count as 1
                    type = DataType.Device;
                }
                if (regAliases.ContainsKey(token))
                {
                    L.Debug($"Token {token} is a register alias with count {regAliases[token]}");
                    count += 1; // multiple aliaes are allowed, thus only count as 1
                    type = DataType.Register;
                }

                if (labels.ContainsKey(TrimToken(token)))
                {
                    L.Debug($"Token {token} is a label with count {labels[token]}");
                    count += labels[token];
                    type = DataType.Label;
                }

                if (IC10Utils.Instructions.ContainsKey(token))
                {
                    L.Debug($"Token {token} is an instruction");
                    count += 1;
                    type = DataType.Instruction;
                }

                if (count > 1)
                {
                    L.Warning($"Token {token} has multiple definitions, marking as error");
                    type = DataType.Unknown;
                }

                if (types.ContainsKey(token) && types[token] != type)
                {
                    L.Warning($"Token {token} changed type from {types[token]} to {type}");
                }
                types[token] = type;

                foreach (IC10Line line in Lines)
                    line.SetTypes(types);
            }

            private void AddDictEntry(Dictionary<string, int> dict, string key, DataType type)
            {
                L.Debug($"AddDictEntry: adding key {key} to dictionary");
                if (!dict.ContainsKey(key))
                    dict[key] = 0;

                dict[key]++;

                if (!types.ContainsKey(key) || types[key] != type)
                    UpdateDataType(key);
            }

            private void RemoveDictEntry(Dictionary<string, int> dict, string key, DataType type)
            {
                if (!dict.ContainsKey(key))
                {
                    L.Warning($"RemoveDictEntry: trying to remove non-existing key {key} from dictionary");
                    return;
                }
                L.Debug($"RemoveDictEntry: removing key {key} from dictionary");
                dict[key]--;
                if (dict[key] == 0)
                {
                    dict.Remove(key);
                    types.Remove(key);
                }
                else if (types.ContainsKey(key) && types[key] != type)
                    UpdateDataType(key);
            }

            public override void AppendLine(string line)
            {
                InsertLine(Lines.Count, line);
            }

            public override void InsertLine(int index, string line)
            {
                L.Debug($"Formatter: insert line at index {index}/{Lines.Count}, text: '{line}'");
                var ic10line = ParseLine(line) as IC10Line;

                Lines.Insert(index, ic10line);

                if (ic10line.IsLabel)
                    AddDictEntry(labels, TrimToken(ic10line[0].Text), DataType.Label);
                else if (ic10line.IsNumAlias)
                    AddDictEntry(regAliases, ic10line[1].Text, DataType.Number);
                else if (ic10line.IsDevAlias)
                    AddDictEntry(devAliases, ic10line[1].Text, DataType.Device);
                else if (ic10line.IsDefine)
                    AddDictEntry(defines, ic10line[1].Text, DataType.Number);
            }

            public override void RemoveLine(int index)
            {
                L.Debug($"Formatter: removing line at index {index}/{Lines.Count}");
                var line = Lines[index] as IC10Line;
                Lines.RemoveAt(index);

                if (line.Count == 0)
                    return;

                if (line.IsLabel)
                    RemoveDictEntry(labels, TrimToken(line[0].Text), DataType.Label);
                else if (line.IsNumAlias)
                    RemoveDictEntry(regAliases, line[1].Text, DataType.Number);
                else if (line.IsDevAlias)
                    RemoveDictEntry(devAliases, line[1].Text, DataType.Device);
                else if (line.IsDefine)
                    RemoveDictEntry(defines, line[1].Text, DataType.Number);
            }

            // public static void DrawColoredText(List<ColoredTextSegment> input)
            // {
            //     var pos = ImGui.GetCursorScreenPos();
            //     var list = ImGui.GetWindowDrawList();
            //     foreach (var segment in input)
            //         list.AddText(
            //             pos + segment.Pos,
            //             segment.Color,
            //             segment.Text);
            // }
            //
            // public static void ParseAndDrawColoredText(string input)
            // {
            //     float width = 0.0f;
            //     DrawColoredText(ParseColoredText(input, ref width));
            // }

            public static FormattedText ParseColoredText(string input, ref float width)
            {
                var result = new FormattedText();

                var lines = input.Split('\n');

                var C = (string color) => ColorFromHTML(color);

                foreach (var line in lines)
                {
                    var regex = new Regex(@"<color=(.*?)>(.*?)</color>", RegexOptions.Singleline);
                    int lastIndex = 0;
                    int column = 0;
                    var resultLine = new Line();

                    foreach (Match match in regex.Matches(line))
                    {
                        if (match.Index > lastIndex)
                        {
                            string rawText = input.Substring(lastIndex, match.Index - lastIndex);
                            if (!string.IsNullOrEmpty(rawText.Trim()))
                                resultLine.Add(new Token(rawText, column, C("#ffffff")));
                            column += rawText.Length;
                        }

                        string color = match.Groups[1].Value;
                        string text = match.Groups[2].Value;
                        if (!string.IsNullOrEmpty(text.Trim()))
                            resultLine.Add(new Token(text, column, C(color)));
                        column += text.Length;

                        lastIndex = match.Index + match.Length;
                    }

                    if (lastIndex < input.Length)
                    {
                        var rawText = input.Substring(lastIndex);
                        if (!string.IsNullOrEmpty(rawText.Trim()))
                            resultLine.Add(new Token(rawText, column, C("#ffffff")));
                        column += rawText.Length;
                    }
                    result.Add(resultLine);
                }


                return result;
            }

            private string _suggestion = null;

            public string xxxGetAutocompleteSuggestion()
            {
                return _suggestion;
            }

            public void xxxDrawAutocomplete(IEditor ed, TextPosition caret, Vector2 pos)
            {
                if (ed.KeyMode != KeyMode.Insert)
                    return;

                _suggestion = null;
                if (!ed.IsWordEnd(caret) && caret.Col < Lines[caret.Line].Length)
                    return;

                if (char.IsWhiteSpace(ed[caret]))
                    caret.Col--;

                var token = Lines.GetTokenAtPosition(caret) as IC10Token;
                if (token == null)
                    return;

                // if (string.IsNullOrEmpty(token.Tooltip))
                //     return;

                var line = Lines[caret.Line] as IC10Line;
                var index = line.IndexOf(token);
                if (index > 0 && !line[0].IsInstruction)
                    return;

                IC10.ArgType argType = DataType.Instruction;

                if (index > 0)
                {
                    var opcode = IC10Utils.Instructions[line[0].Text];
                    argType = opcode.ArgumentTypes[index - 1].Compat;
                }

                var suggestions = new List<string>();

                foreach (var entry in IC10Utils.Types)
                    if (!entry.Key.StartsWith("rr") && !entry.Key.StartsWith("dr"))
                        if (argType.Has(entry.Value) && entry.Key.StartsWith(token.Text))
                            suggestions.Add(entry.Key);

                foreach (var entry in types)
                    if (argType.Has(entry.Value) && entry.Key.StartsWith(token.Text))
                        suggestions.Add(entry.Key);

                var n = suggestions.Count;
                if (n == 0)
                    return;

                if (n == 1 && suggestions[0] == token.Text)
                    return;

                _suggestion = "";

                string commonPrefix = string.Empty;  // Start with an empty string, not null

                foreach (var suggestion in suggestions)
                {
                    var rest = suggestion.Substring(token.Text.Length);
                    if (string.IsNullOrEmpty(commonPrefix))
                        commonPrefix = rest;
                    else
                    {
                        int len = Math.Min(commonPrefix.Length, rest.Length);
                        int i = 0;
                        for (; i < len; i++)
                            if (commonPrefix[i] != rest[i])
                                break;

                        commonPrefix = commonPrefix.Substring(0, i);
                    }

                    if (string.IsNullOrEmpty(commonPrefix))
                        break;
                }

                if (string.IsNullOrEmpty(commonPrefix) == false)
                    _suggestion = commonPrefix;

                var width = 0.0f;
                const int maxSuggestions = 20;
                if (n > maxSuggestions)
                {
                    suggestions = suggestions.GetRange(0, maxSuggestions);
                    width = ImGui.CalcTextSize($"... and {n - maxSuggestions} more").x;
                }

                foreach (var suggestion in suggestions)
                    width = Math.Max(ImGui.CalcTextSize(suggestion).x, width);

                var completeSize = new Vector2(10.0f + width, 5.0f + LineHeight * (suggestions.Count + (n > maxSuggestions ? 1 : 0)));
                float bottomSize = ImGui.GetContentRegionAvail().y - LineHeight - 5.0f + ImGui.GetScrollY();
                if (bottomSize < completeSize.y)
                {
                    pos.y -= completeSize.y - bottomSize;
                    pos.x += CharWidth * 2;
                }

                var list = ImGui.GetWindowDrawList();
                list.AddRectFilled(
                    pos,
                    pos + completeSize,
                    ImGui.ColorConvertFloat4ToU32(new Vector4(0.2f, 0.2f, 0.2f, 0.9f)),
                    5.0f);

                pos.x += 5.0f;
                foreach (var suggestion in suggestions)
                {
                    list.AddText(
                        pos,
                        ICodeFormatter.ColorDefault,
                        suggestion);
                    pos.y += LineHeight;
                }

                if (n > maxSuggestions)
                {
                    list.AddText(
                        pos,
                        ICodeFormatter.ColorDefault,
                        $"... and {n - maxSuggestions} more");
                }
            }

            public override void UpdateStatus()
            {
                if (CaretPos.Line < 0 || CaretPos.Line >= Lines.Count)
                    return;

                var tokenAtCaret = Lines.GetTokenAtPosition(CaretPos) as IC10Token;

                if (tokenAtCaret == null && Lines[CaretPos.Line].Count > 0)
                    tokenAtCaret = Lines[0][0] as IC10Token;

                if (tokenAtCaret == null)
                    return;

                var statusLine = new Line();

                if (tokenAtCaret?.Error != null)
                    statusLine.Add(new Token(tokenAtCaret.Error, 0, ICodeFormatter.ColorError));
                else if (tokenAtCaret?.Status != null)
                    statusLine.Add(new Token(tokenAtCaret.Status, 0, ICodeFormatter.ColorDefault));
                else if (tokenAtCaret?.Tooltip != null)
                    statusLine.Add(new Token(tokenAtCaret.Tooltip, 0, ICodeFormatter.ColorDefault));

                _status = new FormattedText();
                _status.Add(statusLine);
            }

            public override void UpdateTooltip(TextPosition mouseTextPos)
            {
                _tooltip = null;

                if (!(bool)mouseTextPos)
                    return;

                var token = Lines.GetTokenAtPosition(mouseTextPos) as IC10Token;

                if (token == null)
                    return;

                var tooltip = new FormattedText();

                if (!string.IsNullOrEmpty(token?.Error))
                    tooltip.Add(new Line(token.Error, ColorError));

                if (!string.IsNullOrEmpty(token?.Tooltip))
                    tooltip.Add(new Line(token.Tooltip));

                if (token.IsInstruction)
                {
                    if (tooltip.Count > 0)
                        tooltip.Add(new Line(""));
                    tooltip.AddRange(IC10Utils.Instructions[token.Text].Tooltip);
                }

                _tooltip = tooltip;
            }

            public override void UpdateAutocomplete()
            {
            }
        }
    }
}
