namespace StationeersIC10Editor
{
    using System;
    using System.Reflection;
    using System.Runtime.CompilerServices;
    using System.Text.RegularExpressions;
    using System.Text;
    using System.Collections.Generic;
    using Assets.Scripts;
    using Assets.Scripts.Objects;
    using Assets.Scripts.Objects.Electrical;
    using Assets.Scripts.Objects.Motherboards;
    using ImGuiNET;
    using UnityEngine;


    public enum DataType
    {
        Unknown = 0x00,
        Register = 0x01,
        Device = 0x02,
        LogicType = 0x04,
        Number = 0x08,
        Label = 0x10,
        Instruction = 0x20,
        BatchMode = 0x40,
        Comment = 0x80,
        Identifier = 0x100,
        Alias = 0x200,
        Define = 0x400,
        LogicSlotType = 0x800,
        ReagentMode = 0x1000,
    }

    public struct ArgType
    {
        uint Value = 0;

        public ArgType()
        { }

        public bool Has(DataType b)
        {
            return (Value & (uint)b) != 0;
        }

        public void Add(DataType b, DataType b2 = 0, DataType b3 = 0)
        {
            Value |= (uint)b;
            Value |= (uint)b2;
            Value |= (uint)b3;
        }

        public string Description
        {
            get
            {
                List<DataType> types = new List<DataType>();

                foreach (DataType dt in Enum.GetValues(typeof(DataType)))
                {
                    if ((Value & (uint)dt) != 0)
                        types.Add(dt);
                }

                if (types.Count == 0)
                    return "Unknown";

                if (types.Count == 1)
                    return types[0].ToString();

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < types.Count; i++)
                {
                    sb.Append(types[i].ToString());
                    if (i < types.Count - 1)
                        sb.Append(" or ");

                }

                return sb.ToString();
            }

        }
    }

    public struct ColoredTextSegment
    {
        public uint Color;
        public string Text;
        public Vector2 Pos;

        public ColoredTextSegment(string color, string text, Vector2 pos)
        {
            Color = ICodeFormatter.ColorFromHTML(color);
            Text = text;
            Pos = pos;
        }
    }


    public class IC10OpCode
    {
        public string Name = String.Empty;
        public string Description = String.Empty;
        public List<ArgType> ArgumentTypes = new List<ArgType>();
        public bool IsBuiltin = true;

        public float TooltipWidth = 0.0f;
        public List<ColoredTextSegment> TooltipHeader = new List<ColoredTextSegment>();
        public List<ColoredTextSegment> TooltipExample = new List<ColoredTextSegment>();


        public IC10OpCode(string name, string description, string example)
        {
            IsBuiltin = true;
            Name = name;
            Description = description;
            float w = 0.0f;
            float wmax = 0.0f;
            TooltipHeader = IC10CodeFormatter.ParseColoredText($"Instruction: <color=yellow>{name}</color>", ref wmax);
            TooltipExample = IC10CodeFormatter.ParseColoredText($"    {example}", ref w);
            wmax = Math.Max(wmax, w);
            TooltipWidth = Math.Max(w + 40, 500.0f);

            int i = 1;
            while (i < TooltipExample.Count)
            {
                var s = TooltipExample[i].Text;
                if (s.Contains("(") || s.Contains(")"))
                {
                    i++;
                    continue;
                }

                while (i + 2 < TooltipExample.Count && TooltipExample[i + 1].Text.Trim() == "|")
                {
                    s += TooltipExample[i + 1].Text;
                    s += TooltipExample[i + 2].Text;
                    i += 2;
                }

                i++;

                ArgType argType = new ArgType();

                foreach (var token in s.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = token.Trim().TrimEnd(',', ')');
                    switch (trimmed)
                    {
                        case "r?":
                            argType.Add(DataType.Register);
                            if (ArgumentTypes.Count > 0)
                                argType.Add(DataType.Number, DataType.Label);
                            break;
                        case "d?":
                            argType.Add(DataType.Device);
                            break;
                        case "id":
                        case "num":
                        case "int":
                        case "deviceHash":
                        case "nameHash":
                        case "slotIndex":
                            argType.Add(DataType.Number, DataType.Label);
                            break;
                        case "logicType":
                            argType.Add(DataType.LogicType);
                            break;
                        case "logicSlotType":
                            argType.Add(DataType.LogicSlotType);
                            break;
                        case "batchMode":
                            argType.Add(DataType.BatchMode);
                            break;
                        case "reagentMode":
                            argType.Add(DataType.BatchMode);
                            break;
                        case "str":
                            argType.Add(DataType.Identifier, DataType.Number, DataType.Label);
                            break;
                        default:
                            L.Warning($"Unknown argument token '{trimmed}' in opcode {name}");
                            break;
                    }
                }

                ArgumentTypes.Add(argType);
            }
        }

        public void DrawTooltip()
        {
            IC10CodeFormatter.DrawColoredText(TooltipHeader);
            ImGui.NewLine();
            IC10CodeFormatter.DrawColoredText(TooltipExample);
            ImGui.NewLine();
            ImGui.NewLine();
            ImGui.TextWrapped(Description);
        }
    }

    public class IC10Token
    {

        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private static void _initInstructions()
        {
            foreach (ScriptCommand cmd in EnumCollections.ScriptCommands.Values)
            {
                string cmdName = Enum.GetName(typeof(ScriptCommand), cmd);
                var description = ProgrammableChip.GetCommandDescription(cmd);
                var example = ProgrammableChip.GetCommandExample(cmd);
                _instructions[cmdName] = new IC10OpCode(cmdName, description, example);
            }
            Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                string message = assembly.GetName().Name;
                if (message.Equals("IC10Extender"))
                {
                    var opcodesObj = assembly.GetType("IC10_Extender.IC10Extender")?
    .GetProperty("OpCodes", BindingFlags.Public | BindingFlags.Static)?
    .GetValue(null);

                    if (opcodesObj == null)
                    {
                        L.Warning("IC10Extender: could not get OpCodes property");
                        continue;
                    }


                    var keysProp = opcodesObj.GetType().GetProperty("Keys");
                    var opcodes = keysProp?.GetValue(opcodesObj) as IEnumerable<string>;
                    if (opcodes == null) continue;

                    var indexer = opcodesObj.GetType().GetProperty("Item");

                    foreach (var name in opcodes)
                    {

                        var opcode = indexer.GetValue(opcodesObj, new[] { name });
                        L.Info($"IC10Extender: Found opcode {name}, type: {opcode.GetType().FullName}");
                        var example = name + " " + opcode.GetType().GetMethod("CommandExample", new[] { typeof(int), typeof(string) })?.Invoke(opcode, new object[] { 0, null }) as string;
                        L.Info($"  Example: {example}");
                        var description = opcode.GetType().GetMethod("Description")?.Invoke(opcode, null) as string;
                        L.Info($"  Description: {description}");
                        _instructions[name] = new IC10OpCode(name, description, example);
                    }
                    break;
                }
            }
        }
        private static Dictionary<string, IC10OpCode> _instructions = new Dictionary<string, IC10OpCode>();
        public static Dictionary<string, IC10OpCode> Instructions
        {
            get
            {
                if (_instructions.Count == 0)
                {
                    _initInstructions();
                }
                return _instructions;
            }
        }

        private static HashSet<string> _registers = new HashSet<string>();
        public static HashSet<string> Registers
        {
            get
            {
                if (_registers.Count == 0)
                {
                    for (int i = 0; i < 16; i++)
                        _registers.Add($"r{i}");
                    _registers.Add("sp");
                    _registers.Add("ra");
                }
                return _registers;
            }
        }

        private static HashSet<string> _devices = new HashSet<string>();
        public static HashSet<string> Devices
        {
            get
            {
                if (_devices.Count == 0)
                {
                    for (int i = 0; i < 6; i++)
                        _devices.Add($"d{i}");
                    for (int i = 0; i < 16; i++)
                    {
                        _devices.Add($"dr{i}");
                        _devices.Add($"drr{i}");
                        _devices.Add($"drrr{i}");
                        _devices.Add($"drrrr{i}");
                    }
                    _devices.Add("db");
                }
                return _devices;
            }
        }

        private static HashSet<string> _logicTypes = new HashSet<string>();
        public static HashSet<string> LogicTypes
        {
            get
            {
                if (_logicTypes.Count == 0)
                {
                    foreach (LogicType lt in EnumCollections.LogicTypes.Values)
                        _logicTypes.Add(Enum.GetName(typeof(LogicType), lt));
                    foreach (var batchMode in new string[] { "Average", "Sum", "Min", "Max" })
                        _logicTypes.Add(batchMode);
                }
                return _logicTypes;
            }
        }

        private static HashSet<string> _constants = new HashSet<string>();
        public static HashSet<string> Constants
        {
            get
            {
                if (_constants.Count == 0)
                {
                    foreach (var constant in ProgrammableChip.AllConstants)
                        _constants.Add(constant.Literal);
                }
                return _constants;
            }
        }


        public string Text;
        public uint Column;
        public uint Color;
        public uint Background;
        public DataType DataType;
        public string Tooltip;

        public bool IsComment => Text.StartsWith("#");
        public bool IsLabel => Text.EndsWith(":");

        public bool IsInstruction => Instructions.ContainsKey(Text);
        public bool IsValidIdentifier => Regex.IsMatch(Text, @"^[a-zA-Z_][a-zA-Z0-9_]*$");

        public IC10Token(string text, uint column, uint color = 0, uint background = 0)
        {
            Column = column;
            Text = text;
            Color = color;
            Background = background;
            Tooltip = String.Empty;
        }

    }


    public class IC10Line : List<IC10Token>
    {
        public int Length => this.Count == 0 ? 0 : (int)(this[Count - 1].Column + this[Count - 1].Text.Length);

        public bool IsLabel => NumCodeTokens == 1 && this[0].IsLabel;
        public bool IsAlias => NumCodeTokens == 3 && this[0].DataType == DataType.Alias;
        public bool IsNumAlias => IsAlias && (this[2].DataType == DataType.Number || this[2].DataType == DataType.Register);
        public bool IsDevAlias => IsAlias && this[2].DataType == DataType.Device;
        public bool IsDefine => NumCodeTokens == 3 && this[0].DataType == DataType.Define && this[2].DataType == DataType.Number;
        public bool IsInstruction => NumCodeTokens > 0 && this[0].IsInstruction;

        public int NumCodeTokens
        {
            get
            {
                if (Count == 0)
                    return 0;

                if (this[Count - 1].IsComment)
                    return Count - 1;
                return Count;
            }
        }

        public static bool IsHashExpression(string text)
        {
            return text.StartsWith("HASH(\"") && text.EndsWith("\")");
        }

        public static bool IsStringExpression(string text)
        {
            return text.StartsWith("STR(\"") && text.EndsWith("\")");
        }

        public void SetTypes(Dictionary<string, DataType> types)
        {
            foreach (var token in this)
            {
                token.Tooltip = String.Empty;

                if (token.IsLabel)
                    token.DataType = DataType.Number;
                else if (token.IsComment)
                    token.DataType = DataType.Comment;
                else if (types.TryGetValue(token.Text, out DataType type))
                    token.DataType = type;
                else if (double.TryParse(token.Text, out double number))
                {
                    token.DataType = DataType.Number;
                    if (Int32.TryParse(token.Text, out int hash))
                    {
                        var thing = Prefab.Find<Thing>(hash);
                        if (thing != null)
                            token.Tooltip = thing.PrefabName + "\n";
                    }
                }
                else if (token.Text.StartsWith("0x") && int.TryParse(token.Text.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out int hexNumber))
                    token.DataType = DataType.Number;
                else if (IsHashExpression(token.Text) || IsStringExpression(token.Text))
                    token.DataType = DataType.Number;
                else
                    token.DataType = DataType.Unknown;

                token.Color = IC10CodeFormatter.GetColor(token);
            }

            if (IsInstruction)
            {
                var opcode = IC10Token.Instructions[this[0].Text];
                var arguments = opcode.ArgumentTypes;

                if (arguments.Count != NumCodeTokens - 1)
                {
                    this[0].Color = ICodeFormatter.ColorError;
                    this[0].Tooltip += $"Error: expected {arguments.Count} arguments, got {NumCodeTokens - 1}\n";

                    for (int i = arguments.Count + 1; i < NumCodeTokens; i++)
                    {
                        this[i].Color = ICodeFormatter.ColorError;
                        this[i].Tooltip += this[0].Tooltip;
                    }
                }
                for (int i = 0; i < Math.Min(arguments.Count, NumCodeTokens - 1); i++)
                {
                    var expectedType = arguments[i];
                    var actualType = this[i + 1].DataType;
                    if (!expectedType.Has(actualType))
                    {
                        this[i + 1].Color = ICodeFormatter.ColorError;
                        string expectedTypeStr = arguments[i].Description;
                        this[i + 1].Tooltip += $"Error: invalid argument type {actualType}, expected {expectedTypeStr}";
                    }
                }
            }
        }
    }

    public abstract class ICodeFormatter
    {
        public static uint ColorError = ColorFromHTML("#ff0000");
        public static uint ColorComment = ColorFromHTML("#808080");
        public static uint ColorLineNumber = ColorFromHTML("#808080");
        public static uint ColorDefault = ColorFromHTML("#ffffff");
        public static uint ColorSelection = ColorFromHTML("#1a44b0ff");
        public static uint ColorNumber = ColorFromHTML("#20b2aa");

        public static Dictionary<string, uint> IC10Colors = new Dictionary<string, uint>
        {
            { "Color.Blue", ColorFromHTML("blue") },
            { "Color.Gray", ColorFromHTML("gray") },
            { "Color.Green", ColorFromHTML("green") },
            { "Color.Orange", ColorFromHTML("orange") },
            { "Color.Red", ColorFromHTML("red") },
            { "Color.Yellow", ColorFromHTML("yellow") },
            { "Color.White", ColorFromHTML("white") },
            { "Color.Black", ColorFromHTML("black") },
            { "Color.Brown", ColorFromHTML("brown") },
            { "Color.Khaki", ColorFromHTML("khaki") },
            { "Color.Pink", ColorFromHTML("pink") },
            { "Color.Purple", ColorFromHTML("purple") },
        };

        public const int LineNumberOffset = 5;

        public abstract void ResetCode(string code);
        public abstract void RemoveLine(int index);
        public abstract void InsertLine(int index, string line);
        public abstract void AppendLine(string line);
        public abstract uint GetColor(string token);
        public abstract uint GetBackground(string token);
        public abstract bool DrawTooltip(string line, TextPosition caret, Vector2 pos);

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

        public void ReplaceLine(int index, string newLine)
        {
            RemoveLine(index);
            InsertLine(index, newLine);
        }

        public string TrimToken(string token)
        {
            return token.TrimEnd(':');
        }

        public abstract void DrawLine(int lineIndex, string line, TextRange selection = default);
        public abstract void DrawStatus();

        public static List<string> Tokenize(string text, bool keepWhitespace = false)
        {
            var tokens = new List<string>();
            if (string.IsNullOrEmpty(text))
                return tokens;

            int i = 0;
            while (i < text.Length)
            {
                int start = i;
                bool isWhitespace = char.IsWhiteSpace(text[i]);
                i++;
                while (i < text.Length && char.IsWhiteSpace(text[i]) == isWhitespace)
                    i++;

                if (keepWhitespace || !isWhitespace)
                    tokens.Add(text.Substring(start, i - start));
            }

            return tokens;
        }

    }

    public class IC10CodeFormatter : ICodeFormatter
    {
        private void DrawRegistersGrid()
        {
            // todo: store this information, update only when code changes
            HashSet<string> usedRegisters = new HashSet<string>();
            HashSet<string> freeRegisters = new HashSet<string>(IC10Token.Registers);

            foreach (var line in Code)
            {
                foreach (var token in line)
                {
                    if (token.DataType == DataType.Register)
                    {
                        var reg = token.Text;
                        usedRegisters.Add(token.Text);
                        freeRegisters.Remove(reg);
                    }
                }
            }

            var drawList = ImGui.GetWindowDrawList();

            Vector2 startPos = ImGui.GetCursorScreenPos();
            Vector2 windowSize = ImGui.GetWindowSize();

            Vector2 rectSize = new Vector2(9, 9);
            float spacing = 4.0f;

            startPos.x = ImGui.GetWindowPos().x + ImGui.GetWindowWidth() - 3 * 85.0f - ImGui.GetStyle().FramePadding.x * 3 - ImGui.GetStyle().ItemSpacing.x;
            startPos.y += 8.0f;
            // startPos.y -= ImGui.GetTextLineHeightWithSpacing() + 4.0f;

            uint colorUsed = ImGui.GetColorU32(new Vector4(1.0f, 1.0f, 0.0f, 1.0f));
            uint colorFree = ImGui.GetColorU32(new Vector4(0.0f, 1.0f, 0.0f, 1.0f));

            float x0 = startPos.x;

            for (int i = 0; i < 16; i++)
            {
                uint color = freeRegisters.Contains($"r{i}") ? colorFree : colorUsed;
                int xShift = i + i / 4; // add extra space every 4 registers
                startPos.x = x0 + xShift * (rectSize.x + spacing);
                // drawList.AddRectFilled(startPos, startPos + rectSize, color, 2);
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

        public static uint GetColor(IC10Token token)
        {
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
        private HashSet<string> logicTypes = new HashSet<string>();
        private HashSet<string> registers = new HashSet<string>();
        private HashSet<string> devices = new HashSet<string>();

        private Dictionary<string, uint> builtins = new Dictionary<string, uint>();

        private List<IC10Line> Code = new List<IC10Line>();

        private void _addBuiltin(string name, uint color, HashSet<string> hashSet = null)
        {
            if (hashSet != null)
                hashSet.Add(name);
            builtins[name] = color;
        }

        public IC10CodeFormatter()
        {
            L.Info("IC10CodeFormatter - Constructor");
            // foreach (ScriptCommand cmd in EnumCollections.ScriptCommands.Values)
            // {
            //     string cmdName = Enum.GetName(typeof(ScriptCommand), cmd);
            //     instructions[cmdName] = cmd;
            //     builtins[cmdName] = ColorInstruction;
            // }

            foreach (LogicType lt in EnumCollections.LogicTypes.Values)
                _addBuiltin(Enum.GetName(typeof(LogicType), lt), ColorLogicType, logicTypes);

            foreach (var batchMode in new string[] { "Average", "Sum", "Min", "Max" })
                _addBuiltin(batchMode, ColorLogicType, logicTypes);

            for (int i = 0; i < 16; i++)
                _addBuiltin($"r{i}", ColorRegister, registers);

            _addBuiltin($"sp", ColorRegister, registers);
            _addBuiltin($"ra", ColorRegister, registers);

            for (int i = 0; i < 6; i++)
                _addBuiltin($"d{i}", ColorDevice, devices);

            _addBuiltin($"db", ColorDevice, devices);

            foreach (var constant in ProgrammableChip.AllConstants)
                _addBuiltin(constant.Literal, ColorNumber);


            // make sure the tokens are still readable with the background color
            var black = ColorFromHTML("black");
            _addBuiltin("Color.White", black);
            _addBuiltin("Color.Yellow", black);
            _addBuiltin("Color.Pink", black);
            _addBuiltin("Color.Green", black);

            foreach (LogicType lt in EnumCollections.LogicTypes.Values)
                types[Enum.GetName(typeof(LogicType), lt)] = DataType.LogicType;


            foreach (ColorType col in EnumCollections.ColorTypes.Values)
                types["Color." + Enum.GetName(typeof(ColorType), col)] = DataType.Number;

            foreach (var batchMode in new string[] { "Average", "Sum", "Min", "Max" })
                types[batchMode] = DataType.BatchMode;

            foreach (var name in registers)
                types[name] = DataType.Register;

            foreach (var name in devices)
                types[name] = DataType.Device;

            foreach (var instr in IC10Token.Instructions.Keys)
                types[instr] = DataType.Instruction;

            types["define"] = DataType.Define;
            types["alias"] = DataType.Alias;

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

            if (IC10Token.Instructions.ContainsKey(token))
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

        private void AddDictEntry(Dictionary<string, int> dict, string key)
        {
            L.Info($"AddDictEntry: adding key {key} to dictionary");
            if (!dict.ContainsKey(key))
                dict[key] = 0;

            dict[key]++;
            if (dict[key] <= 2)
                UpdateDataType(key);
        }

        private void RemoveDictEntry(Dictionary<string, int> dict, string key)
        {
            if (!dict.ContainsKey(key))
            {
                L.Warning($"RemoveDictEntry: trying to remove non-existing key {key} from dictionary");
                return;
            }
            L.Info($"RemoveDictEntry: removing key {key} from dictionary");
            dict[key]--;
            if (dict[key] == 0)
                dict.Remove(key);
            else if (dict[key] < 2)
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
                AddDictEntry(labels, TrimToken(ic10line[0].Text));
            else if (ic10line.IsNumAlias)
                AddDictEntry(regAliases, ic10line[1].Text);
            else if (ic10line.IsDevAlias)
                AddDictEntry(devAliases, ic10line[1].Text);
            else if (ic10line.IsDefine)
                AddDictEntry(defines, ic10line[1].Text);
        }

        public override void RemoveLine(int index)
        {
            L.Info($"Formatter: removing line at index {index}/{Code.Count}");
            var line = Code[index];
            Code.RemoveAt(index);

            if (line.Count == 0)
                return;

            if (line.IsLabel)
                RemoveDictEntry(labels, TrimToken(line[0].Text));
            else if (line.IsNumAlias)
                RemoveDictEntry(regAliases, line[1].Text);
            else if (line.IsDevAlias)
                RemoveDictEntry(devAliases, line[1].Text);
            else if (line.IsDefine)
                RemoveDictEntry(defines, line[1].Text);
        }

        public override uint GetBackground(string token)
        {
            if (IC10Colors.TryGetValue(token, out uint color))
                return color;
            return 0;
        }

        public override uint GetColor(string token)
        {
            if (double.TryParse(token, out double number))
            {
                return ColorNumber;
            }

            token = TrimToken(token);
            if (errors.Contains(token))
                return ColorError;
            else if (defines.ContainsKey(token))
                return ColorDefine;
            else if (regAliases.ContainsKey(token))
                return ColorAlias;
            else if (devAliases.ContainsKey(token))
                return ColorAlias;
            else if (labels.ContainsKey(token))
                return ColorLabel;
            else if (builtins.TryGetValue(token, out uint color))
                return color;
            else
                return ColorDefault;
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
            string instructionDescription = "";

            float width = 0.0f;

            if (tokenAtCaret.Tooltip != null)
            {
                float w = ImGui.CalcTextSize(tokenAtCaret.Tooltip).x + 20.0f;
                width = Math.Min(w, 500.0f);
            }

            if (tokenAtCaret.IsInstruction)
            {
                var instruction = tokenAtCaret.Text;
                var command = IC10Token.Instructions[instruction];
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
                IC10Token.Instructions[tokenAtCaret.Text].DrawTooltip();
            ImGui.EndTooltip();

            return false;
        }
    }
}
