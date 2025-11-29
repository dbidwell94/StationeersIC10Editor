namespace StationeersIC10Editor.IC10;

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

using Assets.Scripts;
using Assets.Scripts.Atmospherics;
using Assets.Scripts.Objects;
using Assets.Scripts.Objects.Electrical;
using Assets.Scripts.Objects.Entities;
using Assets.Scripts.Objects.Motherboards;
using Assets.Scripts.Objects.Pipes;

using Objects.Rockets;

public enum DataType : uint
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
    Color = 0x2000,
    DeviceId = 0x4000,
    BasicEnum = 0x8000,
}

public class IC10Utils
{
    private static HashSet<string> _registers = new HashSet<string>();
    private static HashSet<string> _logicTypes = new HashSet<string>();
    private static HashSet<string> _logicSlotTypes = new HashSet<string>();
    private static HashSet<string> _devices = new HashSet<string>();
    private static HashSet<string> _constants = new HashSet<string>();
    private static HashSet<string> _batchModes = new HashSet<string>();
    private static HashSet<string> _basicEnums = new HashSet<string>();
    private static Dictionary<string, IC10OpCode> _instructions =
        new Dictionary<string, IC10OpCode>();
    private static Dictionary<string, ArgType> _types = new Dictionary<string, ArgType>();

    public static Dictionary<string, uint> Colors = new Dictionary<string, uint>
    {
        { "Color.Blue", ICodeFormatter.ColorFromHTML("blue") },
        { "Color.Gray", ICodeFormatter.ColorFromHTML("gray") },
        { "Color.Green", ICodeFormatter.ColorFromHTML("green") },
        { "Color.Orange", ICodeFormatter.ColorFromHTML("orange") },
        { "Color.Red", ICodeFormatter.ColorFromHTML("red") },
        { "Color.Yellow", ICodeFormatter.ColorFromHTML("yellow") },
        { "Color.White", ICodeFormatter.ColorFromHTML("white") },
        { "Color.Black", ICodeFormatter.ColorFromHTML("black") },
        { "Color.Brown", ICodeFormatter.ColorFromHTML("brown") },
        { "Color.Khaki", ICodeFormatter.ColorFromHTML("khaki") },
        { "Color.Pink", ICodeFormatter.ColorFromHTML("pink") },
        { "Color.Purple", ICodeFormatter.ColorFromHTML("purple") },
    };

    public static bool IsBuiltin(string name)
    {
        return Types.ContainsKey(name);
    }

    public static ArgType GetType(string name)
    {
        if (Types.TryGetValue(name, out ArgType type))
            return type;
        return DataType.Unknown;
    }

    private static void _addType(string name, DataType type)
    {
        if (!_types.ContainsKey(name))
            _types[name] = type;
        else
        {
            var t = _types[name];
            t.Add(type);
            _types[name] = t;
        }
    }

    static void _addEnum<T>(string name = "")
    {
        if (!string.IsNullOrEmpty(name))
            name = name + ".";

        foreach (var enumName in Enum.GetNames(typeof(T)))
        {
            _addType(name + enumName, DataType.BasicEnum);
            _basicEnums.Add(name + enumName);
        }
    }

    public static Dictionary<string, ArgType> Types
    {
        get
        {
            if (_types.Count == 0)
            {
                _addEnum<SoundAlert>("Sound");
                _addEnum<LogicTransmitterMode>("TransmitterMode");
                _addEnum<ElevatorMode>("ElevatorMode");
                _addEnum<ColorType>("Color");
                _addEnum<EntityState>("EntityState");
                _addEnum<AirControlMode>("AirControl");
                _addEnum<DaylightSensor.DaylightSensorMode>("DaylightSensorMode");
                _addEnum<ConditionOperation>();
                _addEnum<AirConditioningMode>("AirCon");
                _addEnum<VentDirection>("Vent");
                _addEnum<PowerMode>("PowerMode");
                _addEnum<RobotMode>("RobotMode");
                _addEnum<SortingClass>("SortingClass");
                _addEnum<Slot.Class>("SlotClass");
                _addEnum<Chemistry.GasType>("GasType");
                _addEnum<RocketMode>("RocketMode");
                _addEnum<ReEntryProfile>("ReEntryProfile");
                _addEnum<SorterInstruction>("SorterInstruction");
                _addEnum<PrinterInstruction>("PrinterInstruction");
                _addEnum<TraderInstruction>("TraderInstruction");
                _addEnum<ShuttleType>("ShuttleType");
                _addEnum<HashType>("HashType");
                _addEnum<LogicDisplay.DisplayMode>("DisplayMode");
                _addEnum<SettingDisplayMode>("SettingDisplayMode");

                foreach (var reg in Registers)
                    _addType(reg, DataType.Register);
                foreach (var dev in Devices)
                    _addType(dev, DataType.Device);
                foreach (var lt in LogicTypes)
                    _addType(lt, DataType.LogicType);
                foreach (var lt in LogicSlotTypes)
                    _addType(lt, DataType.LogicSlotType);
                foreach (var constant in Constants)
                    _addType(constant, DataType.Number);
                foreach (var bm in BatchModes)
                    _addType(bm, DataType.BatchMode);
                foreach (var instr in Instructions.Keys)
                    _addType(instr, DataType.Instruction);
                foreach (var color in Colors.Keys)
                    _addType(color, DataType.Color);

                _types["define"] = DataType.Define;
                _types["alias"] = DataType.Alias;
            }
            return _types;
        }
    }

    public static HashSet<string> BatchModes
    {
        get
        {
            if (_batchModes.Count == 0)
                foreach (LogicType lt in EnumCollections.LogicBatchMethods.Values)
                    _batchModes.Add(Enum.GetName(typeof(LogicBatchMethod), lt));
            return _batchModes;
        }
    }

    public static HashSet<string> Registers
    {
        get
        {
            if (_registers.Count == 0)
            {
                for (int i = 0; i < 16; i++)
                {
                    _registers.Add($"r{i}");
                    _registers.Add($"rr{i}");
                    _registers.Add($"rrr{i}");
                    _registers.Add($"rrrr{i}");
                }
                _registers.Add("sp");
                _registers.Add("ra");
            }
            return _registers;
        }
    }

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

    public static HashSet<string> BasicEnums => _basicEnums;

    public static HashSet<string> LogicTypes
    {
        get
        {
            if (_logicTypes.Count == 0)
                foreach (LogicType lt in EnumCollections.LogicTypes.Values)
                    _logicTypes.Add(Enum.GetName(typeof(LogicType), lt));
            return _logicTypes;
        }
    }

    public static HashSet<string> LogicSlotTypes
    {
        get
        {
            if (_logicSlotTypes.Count == 0)
                foreach (LogicSlotType lt in EnumCollections.LogicSlotTypes.Values)
                    _logicSlotTypes.Add(Enum.GetName(typeof(LogicSlotType), lt));
            return _logicSlotTypes;
        }
    }

    public static HashSet<string> Constants
    {
        get
        {
            if (_constants.Count == 0)
                foreach (var constant in ProgrammableChip.AllConstants)
                    _constants.Add(constant.Literal);
            return _constants;
        }
    }

    public static Dictionary<string, IC10OpCode> Instructions
    {
        get
        {
            if (_instructions.Count == 0)
                _initInstructions();
            return _instructions;
        }
    }

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
                var opcodesObj = assembly
                    .GetType("IC10_Extender.IC10Extender")
                    ?.GetProperty("OpCodes", BindingFlags.Public | BindingFlags.Static)
                    ?.GetValue(null);

                if (opcodesObj == null)
                {
                    L.Warning("IC10Extender: could not get OpCodes property");
                    continue;
                }

                var keysProp = opcodesObj.GetType().GetProperty("Keys");
                var opcodes = keysProp?.GetValue(opcodesObj) as IEnumerable<string>;
                if (opcodes == null)
                    continue;

                var indexer = opcodesObj.GetType().GetProperty("Item");

                foreach (var name in opcodes)
                {
                    var opcode = indexer.GetValue(opcodesObj, new[] { name });
                    L.Debug(
                        $"IC10Extender: Found opcode {name}, type: {opcode.GetType().FullName}"
                    );
                    var example =
                        name
                            + " "
                            + opcode
                                .GetType()
                                .GetMethod("CommandExample", new[] { typeof(int), typeof(string) })
                                ?.Invoke(opcode, new object[] { 0, null })
                        as string;
                    L.Debug($"  Example: {example}");
                    var description =
                        opcode.GetType().GetMethod("Description")?.Invoke(opcode, null) as string;
                    L.Debug($"  Description: {description}");
                    IC10Utils.Instructions[name] = new IC10OpCode(name, description, example);
                }
                break;
            }
        }
    }
}

public struct ArgType
{
    uint Value = 0;

    public ArgType() { }

    public static implicit operator ArgType(DataType b)
    {
        ArgType at = new ArgType();
        at.Value = (uint)b;
        return at;
    }

    public bool Has(DataType b)
    {
        return (Value & (uint)b) != 0;
    }

    public bool Has(ArgType b)
    {
        return (Value & b.Value) != 0;
    }

    public void Add(DataType b, DataType b2 = 0, DataType b3 = 0, DataType b4 = 0, DataType b5 = 0)
    {
        Value |= (uint)b;
        Value |= (uint)b2;
        Value |= (uint)b3;
        Value |= (uint)b4;
        Value |= (uint)b5;
    }

    public DataType ToDataType()
    {
        foreach (DataType dt in Enum.GetValues(typeof(DataType)))
        {
            if ((Value & (uint)dt) != 0)
                return dt;
        }
        return DataType.Unknown;
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

    public ArgType Compat
    {
        get
        {
            ArgType at = new ArgType();
            at.Value = this.Value;
            if (Has(DataType.Number))
            {
                at.Add(
                    DataType.Label,
                    DataType.Register,
                    DataType.LogicType,
                    DataType.LogicSlotType,
                    DataType.BatchMode
                );
                at.Add(DataType.Color, DataType.BasicEnum);
            }
            if (Has(DataType.Label))
                at.Add(DataType.Number, DataType.Register);
            if (Has(DataType.DeviceId))
                at.Add(DataType.Number, DataType.Register);
            if (Has(DataType.BatchMode))
                at.Add(DataType.Number);
            if (Has(DataType.LogicType))
                at.Add(DataType.Number);
            return at;
        }
    }
}

public class IC10OpCode
{
    public string Name = String.Empty;
    public string Description = String.Empty;
    public List<ArgType> ArgumentTypes = new List<ArgType>();
    public bool IsBuiltin = true;

    public FormattedText Tooltip = new FormattedText();

    public IC10OpCode(string name, string description, string example)
    {
        IsBuiltin = true;
        Name = name;
        Description = description;
        float w = 0.0f;
        float wmax = 0.0f;
        // Parse colored text for display
        FormattedText TooltipHeader = IC10CodeFormatter.ParseColoredText(
            $"Instruction: <color=yellow>{name}</color>",
            ref wmax
        );
        Line TooltipExample = IC10CodeFormatter.ParseColoredText($"    {example}", ref w)[0];
        wmax = Math.Max(wmax, w);

        // Parse arguments from the plain text of the example string
        // Example usually looks like "move r? num" or "alias str d?"
        string plainExample = TooltipExample.Text.Trim();

        // Skip the first part (the command itself)
        int firstSpace = plainExample.IndexOf(' ');
        if (firstSpace >= 0)
        {
            string argsStr = plainExample.Substring(firstSpace).Trim();

            // The argument format might use | for alternatives, e.g., "r?|num"
            // We split by spaces to get argument chunks first
            string[] argChunks = argsStr.Split(
                new char[] { ' ', '\t' },
                StringSplitOptions.RemoveEmptyEntries
            );

            foreach (string s in argChunks)
            {
                if (string.IsNullOrWhiteSpace(s) || s == "(" || s == ")")
                    continue;

                // Handle cases where the game might output args strangely
                if (s == "|")
                    continue; // Should be handled within the arg string usually

                ArgType argType = new ArgType();

                foreach (
                    var token in s.Split(new char[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                )
                {
                    var trimmed = token.Trim().TrimEnd(',', ')');
                    switch (trimmed)
                    {
                        case "r?":
                            argType.Add(DataType.Register);
                            if (ArgumentTypes.Count > 0)
                                argType.Add(DataType.Number);
                            break;
                        case "d?":
                            argType.Add(DataType.Device);
                            break;
                        case "id":
                            argType.Add(DataType.DeviceId);
                            break;
                        case "num":
                        case "int":
                        case "deviceHash":
                        case "nameHash":
                        case "slotIndex":
                            argType.Add(DataType.Number, DataType.Register);
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
                            argType.Add(DataType.ReagentMode);
                            break;
                        case "str":
                            argType.Add(DataType.Identifier, DataType.Label, DataType.Device);
                            break;
                        default:
                            // L.Warning($"Unknown argument token '{trimmed}' in opcode {name}");
                            break;
                    }
                }
                ArgumentTypes.Add(argType);
            }
        }

        Tooltip = new FormattedText();
        Tooltip.AddRange(TooltipHeader);
        Tooltip.Add(new Line(""));
        Tooltip.Add(TooltipExample);
        Tooltip.Add(new Line(""));
        var descriptionLine = new Line(""); // Correct constructor
        int column = 0;
        foreach (var word in Description.Split(' '))
        {
            if (column + word.Length > 70)
            {
                Tooltip.Add(descriptionLine);
                descriptionLine = new Line("");
                column = 0;
            }
            // For description, we just want plain text, so we can use a token if we want coloring,
            // or just append text to the Line if Line supported append.
            // Since Line wraps content, we'll just reconstruct the content.
            // But Line is immutable-ish for text content unless we rebuild it.
            // Actually, simpler to just treat description as one big token or rebuild the string.
            // For simplicity, we just create a token for each word but need to manage the text content of the Line.
            // Current Line implementation requires full text at init.
            // So we must build the string first.
        }
        // Simpler description handling:
        var descLines = WrapText(Description, 70);
        foreach (var dl in descLines)
        {
            Tooltip.Add(new Line(dl));
        }
    }

    private static List<string> WrapText(string text, int maxLen)
    {
        var lines = new List<string>();
        string currentLine = "";
        foreach (var word in text.Split(' '))
        {
            if (currentLine.Length + word.Length + 1 > maxLen)
            {
                lines.Add(currentLine);
                currentLine = "";
            }
            if (currentLine.Length > 0)
                currentLine += " ";
            currentLine += word;
        }
        if (currentLine.Length > 0)
            lines.Add(currentLine);
        return lines;
    }
}

// IC10Line updated to work with SemanticToken logic
public class IC10Line : Line
{
    public IC10Line(string text = "")
        : base(text)
    {
        // Tokens are populated by IC10CodeFormatter
    }

    public bool IsLabel => Tokens.Count == 1 && GetDataType(0) == DataType.Label;
    public bool IsAlias => Tokens.Count == 3 && GetDataType(0) == DataType.Alias;
    public bool IsNumAlias =>
        IsAlias && (GetDataType(2) == DataType.Number || GetDataType(2) == DataType.Register);
    public bool IsDevAlias => IsAlias && GetDataType(2) == DataType.Device;
    public bool IsDefine =>
        Tokens.Count == 3 && GetDataType(0) == DataType.Define && GetDataType(2) == DataType.Number;
    public bool IsInstruction => Tokens.Count > 0 && GetDataType(0) == DataType.Instruction;

    // Counts actual semantic tokens (excluding whitespace/comments if they are treated as tokens,
    // but in SemanticToken model, everything interesting is a token.
    // We usually care about non-comment tokens for logic.)
    public int NumCodeTokens
    {
        get
        {
            int count = 0;
            foreach (var t in Tokens)
            {
                if ((DataType)t.Type != DataType.Comment)
                    count++;
            }
            return count;
        }
    }

    public DataType GetDataType(int index)
    {
        // This is tricky because Tokens list might contain comments or be sorted by column.
        // We need the i-th *code* token.
        int current = 0;
        foreach (var t in Tokens)
        {
            if ((DataType)t.Type == DataType.Comment)
                continue;
            if (current == index)
                return (DataType)t.Type;
            current++;
        }
        return DataType.Unknown;
    }

    // Get the text of the i-th code token
    public string GetTokenText(int index)
    {
        int current = 0;
        foreach (var t in Tokens)
        {
            if ((DataType)t.Type == DataType.Comment)
                continue;
            if (current == index)
                return Text.Substring(t.Column, t.Length);
            current++;
        }
        return string.Empty;
    }

    public static bool IsDeviceChannel(string text)
    {
        if (!text.Contains(":"))
            return false;
        var parts = text.Split(new char[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2)
            return false;
        bool isDevice = IC10Utils.Devices.Contains(parts[0]);
        if (!isDevice)
            return false;
        return Int32.TryParse(parts[1], out int channel) && channel >= 0 && channel <= 7;
    }

    public static bool IsHashExpression(string text) =>
        text.StartsWith("HASH(\"") && text.EndsWith("\")");

    public static bool IsStringExpression(string text) =>
        text.StartsWith("STR(\"") && text.EndsWith("\")");

    public static bool IsBinaryExpression(string text)
    {
        if (!text.StartsWith("%"))
            return false;
        string digits = text.Substring(1).Replace("_", "");
        foreach (char c in digits)
            if (c != '0' && c != '1')
                return false;
        return true;
    }

    // This method logic was moved to IC10CodeFormatter.IdentifyTypesAndAddTokens
    // We keep a simplified version here if needed for updates, or remove it.
    // Since IC10CodeFormatter calls specific logic, we can rely on that.
    // But if we want to update types dynamically (e.g. after alias change):

    public void UpdateTokenColors(Dictionary<string, DataType> types)
    {
        // We need to re-evaluate token types.
        // Since SemanticToken is a struct, we iterate by index to modify.
        for (int i = 0; i < Tokens.Count; i++)
        {
            var t = Tokens[i];
            string text = Text.Substring(t.Column, t.Length);

            // Re-resolve type if it was an identifier
            if (types.TryGetValue(text, out DataType newType))
            {
                t.Type = (uint)newType;
                t.Color = IC10CodeFormatter.GetColor(newType, text);
                t.Background = IC10CodeFormatter.GetBackgroundColor(newType, text);
                Tokens[i] = t;
            }
        }
    }
}
