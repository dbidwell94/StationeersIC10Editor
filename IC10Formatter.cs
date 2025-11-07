namespace StationeersIC10Editor
{
    using System;
    using UI.Tooltips;
    using System.Text.RegularExpressions;
    using Assets.Scripts.Util;
    using System.Text;
    using System.Collections.Generic;
    using Assets.Scripts;
    using Assets.Scripts.Objects;
    using Assets.Scripts.Objects.Electrical;
    using Assets.Scripts.Objects.Motherboards;
    using Assets.Scripts.UI;
    using ImGuiNET;
    using UnityEngine;

    public enum IC10DataType
    {
        Unknown = 0,
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
    }

    public class IC10Token
    {
        private static Dictionary<string, ScriptCommand> _instructions = new Dictionary<string, ScriptCommand>();
        public static Dictionary<string, ScriptCommand> Instructions
        {
            get
            {
                if (_instructions.Count == 0)
                {
                    foreach (ScriptCommand cmd in EnumCollections.ScriptCommands.Values)
                    {
                        string cmdName = Enum.GetName(typeof(ScriptCommand), cmd);
                        _instructions[cmdName] = cmd;
                        String description = ProgrammableChip.GetCommandDescription(cmd);
                    }
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
        public IC10DataType DataType;
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

        public static List<IC10DataType> GetCommandArguments(ScriptCommand command)
        {

            var reg = IC10DataType.Register;
            var num = IC10DataType.Number | IC10DataType.Register | IC10DataType.Label;
            var dev = IC10DataType.Device;
            var log = IC10DataType.LogicType;
            var batch = IC10DataType.BatchMode | IC10DataType.Number;
            var name = IC10DataType.Identifier;

            switch (command)
            {
                case ScriptCommand.clr:
                    return new List<IC10DataType> { dev };
                case ScriptCommand.clrd:
                    return new List<IC10DataType> { num };
                case ScriptCommand.get:
                    return new List<IC10DataType> { reg, dev | num, num };
                case ScriptCommand.put:
                    return new List<IC10DataType> { dev | num, num };
                case ScriptCommand.getd:
                    return new List<IC10DataType> { reg, num, num };
                case ScriptCommand.putd:
                    return new List<IC10DataType> { num, num, num };
                case ScriptCommand.l:
                    return new List<IC10DataType> { reg, dev | num, log };
                case ScriptCommand.ld:
                    return new List<IC10DataType> { reg, num, log };
                case ScriptCommand.s:
                    return new List<IC10DataType> { dev | num, log, num };
                case ScriptCommand.sd:
                    return new List<IC10DataType> { num, log, num };
                case ScriptCommand.ss:
                    return new List<IC10DataType> { num, log, num, num };
                case ScriptCommand.sbs:
                    return new List<IC10DataType> { num, num, log, num };
                case ScriptCommand.lb:
                    return new List<IC10DataType> { reg, num, log, batch };
                case ScriptCommand.lbn:
                    return new List<IC10DataType> { reg, num, num, log, batch };
                case ScriptCommand.lbs:
                    return new List<IC10DataType> { reg, num, num, log, batch };
                case ScriptCommand.lbns:
                    return new List<IC10DataType> { reg, num, num, num, log, batch };
                case ScriptCommand.sb:
                    return new List<IC10DataType> { num, log, num };
                case ScriptCommand.sbn:
                    return new List<IC10DataType> { num, num, log, num };
                case ScriptCommand.ls:
                    return new List<IC10DataType> { reg, dev | num, num, log };
                case ScriptCommand.lr:
                    return new List<IC10DataType> { reg, dev | num, num, num };
                case ScriptCommand.define:
                    return new List<IC10DataType> { num | name, IC10DataType.Number };
                case ScriptCommand.alias:
                    return new List<IC10DataType> { reg | name | dev, dev | reg };
                case ScriptCommand.add:
                case ScriptCommand.sub:
                case ScriptCommand.slt:
                case ScriptCommand.sgt:
                case ScriptCommand.sle:
                case ScriptCommand.sge:
                case ScriptCommand.seq:
                case ScriptCommand.sne:
                case ScriptCommand.and:
                case ScriptCommand.or:
                case ScriptCommand.xor:
                case ScriptCommand.nor:
                case ScriptCommand.mul:
                case ScriptCommand.div:
                case ScriptCommand.mod:
                case ScriptCommand.max:
                case ScriptCommand.min:
                case ScriptCommand.sapz:
                case ScriptCommand.snaz:
                case ScriptCommand.atan2:
                case ScriptCommand.srl:
                case ScriptCommand.sra:
                case ScriptCommand.sll:
                case ScriptCommand.sla:
                case ScriptCommand.pow:
                    return new List<IC10DataType> { reg, num, num };
                case ScriptCommand.sap:
                case ScriptCommand.sna:
                case ScriptCommand.select:
                case ScriptCommand.ext:
                case ScriptCommand.ins:
                    return new List<IC10DataType> { reg, num, num, num };
                case ScriptCommand.j:
                case ScriptCommand.jal:
                case ScriptCommand.jr:
                    return new List<IC10DataType> { num };
                case ScriptCommand.bltz:
                case ScriptCommand.bgez:
                case ScriptCommand.blez:
                case ScriptCommand.bgtz:
                case ScriptCommand.bltzal:
                case ScriptCommand.bgezal:
                case ScriptCommand.blezal:
                case ScriptCommand.bgtzal:
                case ScriptCommand.brltz:
                case ScriptCommand.brgez:
                case ScriptCommand.brlez:
                case ScriptCommand.brgtz:
                case ScriptCommand.beqz:
                case ScriptCommand.bnez:
                case ScriptCommand.breqz:
                case ScriptCommand.brnez:
                case ScriptCommand.beqzal:
                case ScriptCommand.bnezal:
                case ScriptCommand.brnan:
                case ScriptCommand.bnan:
                case ScriptCommand.poke:
                    return new List<IC10DataType> { num, num };
                case ScriptCommand.beq:
                case ScriptCommand.bne:
                case ScriptCommand.beqal:
                case ScriptCommand.bneal:
                case ScriptCommand.breq:
                case ScriptCommand.brne:
                case ScriptCommand.blt:
                case ScriptCommand.bgt:
                case ScriptCommand.ble:
                case ScriptCommand.bge:
                case ScriptCommand.brlt:
                case ScriptCommand.brgt:
                case ScriptCommand.brle:
                case ScriptCommand.brge:
                case ScriptCommand.bltal:
                case ScriptCommand.bgtal:
                case ScriptCommand.bleal:
                case ScriptCommand.bgeal:
                case ScriptCommand.bapz:
                case ScriptCommand.bnaz:
                case ScriptCommand.brapz:
                case ScriptCommand.brnaz:
                case ScriptCommand.bapzal:
                case ScriptCommand.bnazal:
                    return new List<IC10DataType> { num, num, num };
                case ScriptCommand.bap:
                case ScriptCommand.bna:
                case ScriptCommand.brap:
                case ScriptCommand.brna:
                case ScriptCommand.bapal:
                case ScriptCommand.bnaal:
                    return new List<IC10DataType> { num, num, num, num };
                case ScriptCommand.lerp:
                    return new List<IC10DataType> { reg, num, num };
                case ScriptCommand.move:
                case ScriptCommand.sqrt:
                case ScriptCommand.round:
                case ScriptCommand.trunc:
                case ScriptCommand.ceil:
                case ScriptCommand.floor:
                case ScriptCommand.abs:
                case ScriptCommand.log:
                case ScriptCommand.exp:
                case ScriptCommand.sltz:
                case ScriptCommand.sgtz:
                case ScriptCommand.slez:
                case ScriptCommand.sgez:
                case ScriptCommand.seqz:
                case ScriptCommand.snez:
                case ScriptCommand.sin:
                case ScriptCommand.asin:
                case ScriptCommand.tan:
                case ScriptCommand.atan:
                case ScriptCommand.cos:
                case ScriptCommand.acos:
                case ScriptCommand.snan:
                case ScriptCommand.snanz:
                case ScriptCommand.not:
                    return new List<IC10DataType> { reg, num };
                case ScriptCommand.rand:
                    return new List<IC10DataType> { reg };
                case ScriptCommand.yield:
                case ScriptCommand.hcf:
                    return new List<IC10DataType> { };
                case ScriptCommand.label:
                    return new List<IC10DataType> { dev, name };
                case ScriptCommand.push:
                case ScriptCommand.sleep:
                    return new List<IC10DataType> { num };
                case ScriptCommand.peek:
                case ScriptCommand.pop:
                    return new List<IC10DataType> { reg };
                case ScriptCommand.sdse:
                case ScriptCommand.sdns:
                    return new List<IC10DataType> { reg, dev | num };
                case ScriptCommand.bdse:
                case ScriptCommand.bdns:
                case ScriptCommand.brdse:
                case ScriptCommand.brdns:
                case ScriptCommand.bdseal:
                case ScriptCommand.bdnsal:
                    return new List<IC10DataType> { dev | num, num };
                case ScriptCommand.rmap:
                    return new List<IC10DataType> { reg, dev, num };
                case ScriptCommand.bdnvl:
                case ScriptCommand.bdnvs:
                    return new List<IC10DataType> { num | dev, log, num };
                default:
                    throw new ArgumentOutOfRangeException(Localization.GetInterface("ScriptCommandCommand"), command, null);
            }
        }
    }


    public class IC10Line : List<IC10Token>
    {
        public int Length => this.Count == 0 ? 0 : (int)(this[Count - 1].Column + this[Count - 1].Text.Length);

        public bool IsLabel => NumCodeTokens == 1 && this[0].IsLabel;
        public bool IsAlias => NumCodeTokens == 3 && this[0].DataType == IC10DataType.Alias;
        public bool IsNumAlias => IsAlias && (this[2].DataType == IC10DataType.Number || this[2].DataType == IC10DataType.Register);
        public bool IsDevAlias => IsAlias && this[2].DataType == IC10DataType.Device;
        public bool IsDefine => NumCodeTokens == 3 && this[0].DataType == IC10DataType.Define && this[2].DataType == IC10DataType.Number;
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

        public void SetTypes(Dictionary<string, IC10DataType> types)
        {
            foreach (var token in this)
            {
                token.Tooltip = String.Empty;

                if (token.IsLabel)
                    token.DataType = IC10DataType.Label;
                else if (token.IsComment)
                    token.DataType = IC10DataType.Comment;
                else if (types.TryGetValue(token.Text, out IC10DataType type))
                    token.DataType = type;
                else if (double.TryParse(token.Text, out double number))
                {
                    token.DataType = IC10DataType.Number;
                    if (Int32.TryParse(token.Text, out int hash))
                    {
                        var thing = Prefab.Find<Thing>(hash);
                        if (thing != null)
                            token.Tooltip = thing.PrefabName + "\n";
                    }
                }
                else if (token.Text.StartsWith("0x") && int.TryParse(token.Text.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out int hexNumber))
                    token.DataType = IC10DataType.Number;
                else if (IsHashExpression(token.Text) || IsStringExpression(token.Text))
                    token.DataType = IC10DataType.Number;
                else
                    token.DataType = IC10DataType.Unknown;

                token.Color = IC10CodeFormatter.GetColor(token);
            }

            if (IsInstruction)
            {
                var arguments = IC10Token.GetCommandArguments(IC10Token.Instructions[this[0].Text]);
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
                    if ((expectedType & actualType) == 0)
                    {
                        this[i + 1].Color = ICodeFormatter.ColorError;
                        string expectedTypeStr = "";
                        if (arguments[i] == (IC10DataType.Number | IC10DataType.Register | IC10DataType.Label))
                            expectedTypeStr = "Number, Register or Label";
                        else if (arguments[i] == (IC10DataType.Number | IC10DataType.Register))
                            expectedTypeStr = "Number or Register";
                        else if (arguments[i] == (IC10DataType.Device | IC10DataType.Number))
                            expectedTypeStr = "Device or Number";
                        // else
                        //     expectedTypeStr = ""; arguments[i].ToString();
                        this[i + 1].Tooltip += $"Error: invalid argument type '{actualType}'";
                        if (!string.IsNullOrEmpty(expectedTypeStr))
                            this[i + 1].Tooltip += $", expected {expectedTypeStr}";
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
                    if (token.DataType == IC10DataType.Register)
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

            uint colorUsed = ImGui.GetColorU32(new Vector4(1.0f, 1.0f, 0.0f, 1.0f));
            uint colorFree = ImGui.GetColorU32(new Vector4(0.0f, 1.0f, 0.0f, 1.0f));

            float x0 = startPos.x;

            for (int i = 0; i < 16; i++)
            {
                uint color = freeRegisters.Contains($"r{i}") ? colorFree : colorUsed;
                int xShift = i + i / 4; // add extra space every 4 registers
                startPos.x = x0 + xShift * (rectSize.x + spacing);
                drawList.AddRectFilled(startPos, startPos + rectSize, color);
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
                case IC10DataType.Number:
                    return ColorNumber;
                case IC10DataType.Device:
                    return ColorDevice;
                case IC10DataType.Register:
                    return ColorRegister;
                case IC10DataType.LogicType:
                case IC10DataType.BatchMode:
                    return ColorLogicType;
                case IC10DataType.Instruction:
                case IC10DataType.Define:
                case IC10DataType.Alias:
                    return ColorInstruction;
                case IC10DataType.Label:
                    return ColorLabel;
                case IC10DataType.Comment:
                    return ColorComment;
                case IC10DataType.Unknown:
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

        private Dictionary<string, IC10DataType> types = new Dictionary<string, IC10DataType>();

        private Dictionary<string, int> defines = new Dictionary<string, int>();
        private Dictionary<string, int> regAliases = new Dictionary<string, int>();
        private Dictionary<string, int> devAliases = new Dictionary<string, int>();
        private Dictionary<string, int> labels = new Dictionary<string, int>();
        private Dictionary<string, ScriptCommand> instructions = new Dictionary<string, ScriptCommand>();
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
            foreach (ScriptCommand cmd in EnumCollections.ScriptCommands.Values)
            {
                string cmdName = Enum.GetName(typeof(ScriptCommand), cmd);
                instructions[cmdName] = cmd;
                builtins[cmdName] = ColorInstruction;
            }

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
                types[Enum.GetName(typeof(LogicType), lt)] = IC10DataType.LogicType;


            foreach (ColorType col in EnumCollections.ColorTypes.Values)
                types["Color." + Enum.GetName(typeof(ColorType), col)] = IC10DataType.Number;

            foreach (var batchMode in new string[] { "Average", "Sum", "Min", "Max" })
                types[batchMode] = IC10DataType.BatchMode;

            foreach (var name in registers)
                types[name] = IC10DataType.Register;

            foreach (var name in devices)
                types[name] = IC10DataType.Device;

            foreach (var instr in instructions.Keys)
                types[instr] = IC10DataType.Instruction;

            types["define"] = IC10DataType.Define;
            types["alias"] = IC10DataType.Alias;

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
            IC10DataType type = IC10DataType.Unknown;

            if (defines.ContainsKey(token))
            {
                L.Info($"Token {token} is a define with count {defines[token]}");
                count += defines[token];
                type = IC10DataType.Number;
            }

            if (devAliases.ContainsKey(token))
            {
                L.Info($"Token {token} is a device alias with count {devAliases[token]}");
                count += 1; // multiple aliaes are allowed, thus only count as 1
                type = IC10DataType.Device;
            }
            if (regAliases.ContainsKey(token))
            {
                L.Info($"Token {token} is a register alias with count {regAliases[token]}");
                count += 1; // multiple aliaes are allowed, thus only count as 1
                type = IC10DataType.Register;
            }

            if (labels.ContainsKey(TrimToken(token)))
            {
                L.Info($"Token {token} is a label with count {labels[token]}");
                count += labels[token];
                type = IC10DataType.Label;
            }

            if (instructions.ContainsKey(token))
            {
                L.Info($"Token {token} is an instruction");
                count += 1;
                type = IC10DataType.Instruction;
            }

            if (count > 1)
            {
                L.Warning($"Token {token} has multiple definitions, marking as error");
                type = IC10DataType.Unknown;
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

        public struct ColoredTextSegment
        {
            public uint Color;
            public string Text;
            public Vector2 Pos;

            public ColoredTextSegment(string color, string text, Vector2 pos)
            {
                Color = ColorFromHTML(color);
                Text = text;
                Pos = pos;
            }
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
                    result.Add(new ColoredTextSegment("#ffffff", rawText, pos));
                    pos.x += ImGui.CalcTextSize(rawText).x;
                }

                string color = match.Groups[1].Value;
                string text = match.Groups[2].Value;
                result.Add(new ColoredTextSegment(color, text, pos));
                pos.x += ImGui.CalcTextSize(text).x;

                lastIndex = match.Index + match.Length;
            }

            if (lastIndex < input.Length)
            {
                result.Add(new ColoredTextSegment("#ffffff", input.Substring(lastIndex), pos));
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
                ScriptCommand scriptCommand = instructions[instruction];
                String example = ProgrammableChip.GetCommandExample(scriptCommand);
                instructionDescription = ProgrammableChip.GetCommandDescription(scriptCommand);

                float w = 0.0f;
                instructionHeader = ParseColoredText($"Instruction: <color=yellow>{tokenAtCaret.Text}</color>", ref w);
                width = Math.Max(width, w);

                instructionExample = ParseColoredText($"    {example}", ref w);
                width = Math.Max(width, w);
                width = Math.Max(width + 40, 500.0f);
            }


            ImGui.SetNextWindowSize(new Vector2(width, 0));
            ImGui.BeginTooltip();

            if (tokenAtCaret.Tooltip != null)
            {
                ImGui.TextWrapped(tokenAtCaret.Tooltip);
                if (tokenAtCaret.IsInstruction)
                    ImGui.Separator();
            }
            if (tokenAtCaret.IsInstruction)
            {
                DrawColoredText(instructionHeader);
                ImGui.NewLine();
                DrawColoredText(instructionExample);
                ImGui.NewLine();
                ImGui.TextWrapped(instructionDescription);
            }
            ImGui.EndTooltip();

            return false;
        }
    }
}
