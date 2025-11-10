namespace StationeersIC10Editor
{
    using System;
    using System.Text.RegularExpressions;
    using System.Collections.Generic;
    using Assets.Scripts;
    using Assets.Scripts.Objects.Electrical;
    using Assets.Scripts.Objects.Motherboards;
    using ImGuiNET;
    using UnityEngine;

    namespace IC10
    {


        public class IC10CodeFormatter : ICodeFormatter
        {
            private void DrawRegistersGrid()
            {
                // todo: store this information, update only when code changes
                bool[] registers = new bool[16];
                for (int i = 0; i < 16; i++)
                    registers[i] = false;

                foreach (var line in Code)
                    foreach (var token in line)
                        if (token.DataType == DataType.Register)
                        {
                            var reg = token.Text;
                            while (reg.StartsWith("rr") && reg.Length > 3)
                                reg = reg.Substring(1);
                            registers[int.Parse(reg.Substring(1))] = true;
                        }

                var drawList = ImGui.GetWindowDrawList();

                Vector2 startPos = ImGui.GetCursorScreenPos();
                Vector2 windowSize = ImGui.GetWindowSize();

                Vector2 rectSize = new Vector2(9, 9) * IC10Editor.Scale;
                float spacing = 4.0f * IC10Editor.Scale;

                startPos.x = ImGui.GetWindowPos().x + ImGui.GetWindowWidth() - 3 * IC10Editor.buttonSize.x - ImGui.GetStyle().FramePadding.x * 3 - ImGui.GetStyle().ItemSpacing.x;
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

            public override void DrawStatus()
            {
                DrawRegistersGrid();
                return;
            }

            public override void DrawLine(int lineIndex, string line, TextRange selection = default)
            {
                float charWidth = ImGui.CalcTextSize("M").x;
                if (lineIndex < 0 || lineIndex >= Code.Count)
                    return;
                var tokens = Code[lineIndex];
                Vector2 pos = ImGui.GetCursorScreenPos();
                ImGui
                    .GetWindowDrawList()
                    .AddText(pos, ColorLineNumber, lineIndex.ToString().PadLeft(3) + ".");
                pos.x += LineNumberOffset * charWidth;

                int selectionMin = -1,
                    selectionMax = -1;

                foreach (var token in tokens)
                    if (token.Background != 0)
                    {
                        var tokenPos = new Vector2(pos.x + charWidth * token.Column, pos.y);
                        ImGui.GetWindowDrawList().AddRectFilled(
                            tokenPos,
                            new Vector2(tokenPos.x + charWidth * token.Text.Length,
                                        tokenPos.y + ImGui.GetTextLineHeightWithSpacing()),
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

                        Vector2 selStart = new Vector2(pos.x + (charWidth * selectionMin), pos.y);
                        Vector2 selEnd = new Vector2(
                            pos.x + (charWidth * selectionMax),
                            pos.y + lineHeight);

                        ImGui
                            .GetWindowDrawList()
                            .AddRectFilled(selStart, selEnd, ICodeFormatter.ColorSelection);
                    }
                }

                foreach (var token in tokens)
                {
                    var tokenPos = new Vector2(pos.x + charWidth * token.Column, pos.y);
                    ImGui.GetWindowDrawList().AddText(tokenPos, token.Color, token.Text);
                }

            }

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
                    default:
                        return ColorDefault;
                }
            }

            public static uint ColorInstruction = ColorFromHTML("#ffff00");

            // todo: recognize data type of tokens
            public static uint ColorDevice = ColorFromHTML("#00ff00");
            public static uint ColorLogicType = ColorFromHTML("#ff8000");
            public static uint ColorRegister = ColorFromHTML("#0080ff");

            public static uint ColorDefine = ColorNumber;
            public static uint ColorAlias = ColorFromHTML("#4d4dcc");
            public static uint ColorLabel = ColorFromHTML("#800080");

            private HashSet<string> errors = new HashSet<string>(); // tokens that are marked as errors (used as alias and define for instance)

            private Dictionary<string, DataType> types = new Dictionary<string, DataType>();

            private Dictionary<string, int> defines = new Dictionary<string, int>();
            private Dictionary<string, int> regAliases = new Dictionary<string, int>();
            private Dictionary<string, int> devAliases = new Dictionary<string, int>();
            private Dictionary<string, int> labels = new Dictionary<string, int>();
            // private Dictionary<string, ScriptCommand> instructions = new Dictionary<string, ScriptCommand>();
            // private HashSet<string> logicTypes = new HashSet<string>();
            // private HashSet<string> registers = new HashSet<string>();
            // private HashSet<string> devices = new HashSet<string>();

            // private Dictionary<string, uint> builtins = new Dictionary<string, uint>();

            private List<IC10Line> Code = new List<IC10Line>();

            // private void _addBuiltin(string name, uint color, HashSet<string> hashSet = null)
            // {
            //     if (hashSet != null)
            //         hashSet.Add(name);
            //     builtins[name] = color;
            // }

            public IC10CodeFormatter()
            {
                L.Info("IC10CodeFormatter - Constructor");
                // foreach (ScriptCommand cmd in EnumCollections.ScriptCommands.Values)
                // {
                //     string cmdName = Enum.GetName(typeof(ScriptCommand), cmd);
                //     instructions[cmdName] = cmd;
                //     builtins[cmdName] = ColorInstruction;
                // }

                // foreach (LogicType lt in EnumCollections.LogicTypes.Values)
                //     _addBuiltin(Enum.GetName(typeof(LogicType), lt), ColorLogicType, logicTypes);
                //
                // foreach (var batchMode in new string[] { "Average", "Sum", "Min", "Max" })
                //     _addBuiltin(batchMode, ColorLogicType, logicTypes);
                //
                // for (int i = 0; i < 16; i++)
                // {
                //     _addBuiltin($"r{i}", ColorRegister, registers);
                //     _addBuiltin($"rr{i}", ColorRegister, registers);
                //     _addBuiltin($"rrr{i}", ColorRegister, registers);
                //     _addBuiltin($"rrrr{i}", ColorRegister, registers);
                // }
                //
                // _addBuiltin($"sp", ColorRegister, registers);
                // _addBuiltin($"ra", ColorRegister, registers);
                //
                // for (int i = 0; i < 6; i++)
                //     _addBuiltin($"d{i}", ColorDevice, devices);
                //
                // _addBuiltin($"db", ColorDevice, devices);
                //
                // foreach (var constant in ProgrammableChip.AllConstants)
                //     _addBuiltin(constant.Literal, ColorNumber);
                //
                //
                // // make sure the tokens are still readable with the background color
                // var black = ColorFromHTML("black");
                // _addBuiltin("Color.White", black);
                // _addBuiltin("Color.Yellow", black);
                // _addBuiltin("Color.Pink", black);
                // _addBuiltin("Color.Green", black);
                //
                // foreach (LogicType lt in EnumCollections.LogicTypes.Values)
                //     types[Enum.GetName(typeof(LogicType), lt)] = DataType.LogicType;
                //
                //
                // foreach (ColorType col in EnumCollections.ColorTypes.Values)
                //     types["Color." + Enum.GetName(typeof(ColorType), col)] = DataType.Color;
                //
                // foreach (var batchMode in new string[] { "Average", "Sum", "Min", "Max" })
                //     types[batchMode] = DataType.BatchMode;
                //
                // foreach (var name in registers)
                //     types[name] = DataType.Register;
                //
                // foreach (var name in devices)
                //     types[name] = DataType.Device;
                //
                // foreach (var instr in IC10Utils.Instructions.Keys)
                //     types[instr] = DataType.Instruction;
                //
                // types["define"] = DataType.Define;
                // types["alias"] = DataType.Alias;

                Code = new List<IC10Line>();
            }

            public static int FindNextWhitespace(string text, int startIndex)
            {
                while (startIndex < text.Length && !char.IsWhiteSpace(text[startIndex]))
                    startIndex++;

                return startIndex;
            }

            public static int FindNextNonWhitespace(string text, int startIndex)
            {
                while (startIndex < text.Length && char.IsWhiteSpace(text[startIndex]))
                    startIndex++;

                return startIndex;
            }

            public IC10Line ParseIC10Line(string text)
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

                var findNextNonWhitespace = new Func<int, int>((start) =>
                {
                    int j = start;
                    while (j < text.Length && char.IsWhiteSpace(text[j]))
                        j++;
                    return j;
                });

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
                        (uint)start,
                        0));

                    i = end;
                }

                if (!string.IsNullOrEmpty(comment))
                    tokens.Add(new IC10Token(
                        comment,
                        (uint)indexOfComment,
                        ColorComment));

                tokens.SetTypes(types);

                return tokens;
            }


            public override void ResetCode(string code)
            {
                L.Info("IC10CodeFormatter - Reset");
                defines.Clear();
                regAliases.Clear();
                devAliases.Clear();
                labels.Clear();
                errors.Clear();
                Code.Clear();

                var lines = code.Split('\n');
                L.Info($"ResetCode: {lines.Length} lines");
                foreach (var line in lines)
                    AppendLine(line);

            }

            public void UpdateDataType(string token)
            {
                L.Info($"UpdateDataType for token: {token}");
                int count = 0;
                DataType type = DataType.Unknown;

                if (defines.ContainsKey(token))
                {
                    L.Info($"Token {token} is a define with count {defines[token]}");
                    count += defines[token];
                    type = DataType.Number;
                }

                if (devAliases.ContainsKey(token))
                {
                    L.Info($"Token {token} is a device alias with count {devAliases[token]}");
                    count += 1; // multiple aliaes are allowed, thus only count as 1
                    type = DataType.Device;
                }
                if (regAliases.ContainsKey(token))
                {
                    L.Info($"Token {token} is a register alias with count {regAliases[token]}");
                    count += 1; // multiple aliaes are allowed, thus only count as 1
                    type = DataType.Register;
                }

                if (labels.ContainsKey(TrimToken(token)))
                {
                    L.Info($"Token {token} is a label with count {labels[token]}");
                    count += labels[token];
                    type = DataType.Label;
                }

                if (IC10Utils.Instructions.ContainsKey(token))
                {
                    L.Info($"Token {token} is an instruction");
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

                foreach (var line in Code)
                    line.SetTypes(types);
            }

            // private void AddName(IC10Line line)
            // {
            //
            //     if (line.IsLabel)
            //     {
            //         var name = TrimToken(line[0].Text);
            //         types[name] = DataType.Label;
            //         AddDictEntry(labels, name);
            //         return;
            //     }
            //     if (line.IsNumAlias)
            //     {
            //         var name = line[1].Text;
            //         types[name] = DataType.Alias;
            //         AddDictEntry(regAliases, name);
            //         return;
            //     }
            //     if (line.IsDevAlias)
            //     {
            //         var name = line[1].Text;
            //         types[name] = DataType.Alias;
            //         AddDictEntry(devAliases, name);
            //         return;
            //     }
            // }
            //
            private void AddDictEntry(Dictionary<string, int> dict, string key, DataType type)
            {
                L.Info($"AddDictEntry: adding key {key} to dictionary");
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
                L.Info($"RemoveDictEntry: removing key {key} from dictionary");
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
                InsertLine(Code.Count, line);
            }

            public override void InsertLine(int index, string line)
            {
                L.Info($"Formatter: insert line at index {index}/{Code.Count}, text: '{line}'");
                var ic10line = ParseIC10Line(line);

                Code.Insert(index, ic10line);

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
                L.Info($"Formatter: removing line at index {index}/{Code.Count}");
                var line = Code[index];
                Code.RemoveAt(index);

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

            public override uint GetBackground(string token)
            {
                if (IC10Utils.Colors.TryGetValue(token, out uint color))
                    return color;
                return 0;
            }

            public static void DrawColoredText(List<ColoredTextSegment> input)
            {
                var pos = ImGui.GetCursorScreenPos();
                var list = ImGui.GetWindowDrawList();
                foreach (var segment in input)
                    list.AddText(
                        pos + segment.Pos,
                        segment.Color,
                        segment.Text);
            }

            public static void ParseAndDrawColoredText(string input)
            {
                float width = 0.0f;
                DrawColoredText(ParseColoredText(input, ref width));
            }

            public static List<ColoredTextSegment> ParseColoredText(string input, ref float width)
            {
                var result = new List<ColoredTextSegment>();
                var regex = new Regex(@"<color=(.*?)>(.*?)</color>", RegexOptions.Singleline);
                int lastIndex = 0;
                Vector2 pos = new Vector2(0, 0);

                foreach (Match match in regex.Matches(input))
                {
                    if (match.Index > lastIndex)
                    {
                        string rawText = input.Substring(lastIndex, match.Index - lastIndex);
                        if (!string.IsNullOrEmpty(rawText.Trim()))
                            result.Add(new ColoredTextSegment("#ffffff", rawText, pos));
                        pos.x += ImGui.CalcTextSize(rawText).x;
                    }

                    string color = match.Groups[1].Value;
                    string text = match.Groups[2].Value;
                    if (!string.IsNullOrEmpty(text.Trim()))
                        result.Add(new ColoredTextSegment(color, text, pos));
                    pos.x += ImGui.CalcTextSize(text).x;

                    lastIndex = match.Index + match.Length;
                }

                if (lastIndex < input.Length)
                {
                    var rawText = input.Substring(lastIndex);
                    if (!string.IsNullOrEmpty(rawText.Trim()))
                        result.Add(new ColoredTextSegment("#ffffff", rawText, pos));
                    pos.x += ImGui.CalcTextSize(input.Substring(lastIndex)).x;
                }

                width = pos.x;

                return result;
            }

            public override bool DrawTooltip(string line, TextPosition caret, Vector2 pos)
            {
                if (caret.Line < 0 || caret.Line >= Code.Count)
                    return false;

                var codeLine = Code[caret.Line];
                IC10Token tokenAtCaret = null;
                foreach (var token in codeLine)
                {
                    if (caret.Col < token.Column)
                        break;

                    if (caret.Col >= token.Column && caret.Col < token.Column + token.Text.Length)
                    {
                        tokenAtCaret = token;
                        break;
                    }
                }

                if (tokenAtCaret == null || !tokenAtCaret.IsInstruction && string.IsNullOrEmpty(tokenAtCaret.Tooltip))
                    return false;

                List<ColoredTextSegment> instructionHeader = new List<ColoredTextSegment>();
                List<ColoredTextSegment> instructionExample = new List<ColoredTextSegment>();
                float width = 0.0f;

                if (tokenAtCaret.Tooltip != null)
                {
                    float w = ImGui.CalcTextSize(tokenAtCaret.Tooltip).x + 20.0f;
                    width = Math.Min(w, 500.0f);
                }

                if (tokenAtCaret.IsInstruction)
                {
                    var instruction = tokenAtCaret.Text;
                    var command = IC10Utils.Instructions[instruction];
                    width = Math.Max(command.TooltipWidth, width);
                }

                ImGui.SetNextWindowSize(new Vector2(width, 0));
                ImGui.BeginTooltip();

                if (string.IsNullOrEmpty(tokenAtCaret.Tooltip) == false)
                {
                    ImGui.TextWrapped(tokenAtCaret.Tooltip);
                    if (tokenAtCaret.IsInstruction)
                        ImGui.Separator();
                }
                if (tokenAtCaret.IsInstruction)
                    IC10Utils.Instructions[tokenAtCaret.Text].DrawTooltip();
                ImGui.EndTooltip();

                return false;
            }
        }
    }
}
