namespace StationeersIC10Editor.IC10;

using System;
using System.Collections.Generic;

public class IC10CodeFormatter : ICodeFormatter
{
    private Dictionary<string, DataType> types = new Dictionary<string, DataType>();
    private Dictionary<string, int> defines = new Dictionary<string, int>();
    private Dictionary<string, int> regAliases = new Dictionary<string, int>();
    private Dictionary<string, int> devAliases = new Dictionary<string, int>();
    private Dictionary<string, int> labels = new Dictionary<string, int>();
    private HashSet<string> _tokensToUpdate = new HashSet<string>();

    public static double MatchingScore(string input)
    {
        // Simple heuristic: count occurrences of IC10-specific keywords
        int score = 0;
        var lines = input.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var line in lines)
        {
            var firstWord = line.TrimStart().Split(' ')[0];
            if (firstWord.EndsWith(":") || IC10Utils.Instructions.ContainsKey(firstWord))
                score++;
        }
        L.Debug($"IC10CodeFormatter MatchingScore: {score} for input with {lines.Length} lines = {(double)score / lines.Length}");
        return 1.0 * score / lines.Length;
    }

    public IC10CodeFormatter()
    {
        OnCodeChanged += () =>
        {
            UpdateDataType(null, defer: false);
        };
    }

    public string TrimToken(string token)
    {
        return token.TrimEnd(':');
    }

    public static uint GetColor(DataType type, string text)
    {
        if (type == DataType.Color)
        {
            if (
                text == "Color.White"
                || text == "Color.Yellow"
                || text == "Color.Pink"
                || text == "Color.Green"
            )
                return ColorFromHTML("black");
            return ColorFromHTML("white");
        }
        switch (type)
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

    public static uint GetBackgroundColor(DataType type, string text)
    {
        if (type != DataType.Color)
            return 0;
        return IC10Utils.Colors.TryGetValue(text, out uint color) ? color : 0;
    }

    public static uint ColorInstruction = ColorFromHTML("#ffff00");
    public static uint ColorDevice = ColorFromHTML("#00ff00");
    public static uint ColorLogicType = ColorFromHTML("#ff8000");
    public static uint ColorRegister = ColorFromHTML("#0080ff");
    public static uint ColorBasicEnum = ColorFromHTML("#20b2aa");
    public static uint ColorDefine = ColorNumber;
    public static uint ColorAlias = ColorFromHTML("#4d4dcc");
    public static uint ColorLabel = ColorFromHTML("#800080");

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
        if (text == null)
            text = string.Empty;
        var line = new IC10Line(text);

        string processingText = text;
        int commentIndex = text.IndexOf('#');
        if (commentIndex >= 0)
        {
            processingText = text.Substring(0, commentIndex);
            line.AddToken(
                new SemanticToken(
                    0,
                    commentIndex,
                    text.Length - commentIndex,
                    ColorComment,
                    (uint)DataType.Comment
                )
            );
        }

        int i = 0;
        List<SemanticToken> logicTokens = new List<SemanticToken>();
        List<string> tokenTexts = new List<string>();

        while (i < processingText.Length)
        {
            int start = FindNextNonWhitespace(processingText, i);
            int end = FindNextWhitespace(processingText, start);
            if (start >= processingText.Length)
                break;

            string tokenText = processingText.Substring(start, end - start);

            var st = new SemanticToken(0, start, end - start, 0, (uint)DataType.Unknown);
            logicTokens.Add(st);
            tokenTexts.Add(tokenText);

            i = end;
        }

        IdentifyTypesAndAddTokens(line, logicTokens, tokenTexts);
        line.UpdateTokenColors(types);
        return line;
    }

    public void IdentifyTypesAndAddTokens(
        IC10Line line,
        List<SemanticToken> tokens,
        List<string> texts
    )
    {
        if (tokens.Count == 0)
            return;

        var isInstructionLine = false;

        for (int i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            string txt = texts[i];
            DataType dt = DataType.Unknown;
            string error = null;
            string tooltip = null;

            if (IC10Utils.IsBuiltin(txt))
                dt = IC10Utils.Types[txt].ToDataType();
            else if (txt.EndsWith(":"))
                dt = DataType.Label;
            else if (types.TryGetValue(txt, out DataType type))
                dt = type;
            else if (double.TryParse(txt, out _))
            {
                dt = DataType.Number;
                if (int.TryParse(txt, out int hash)) { }
            }
            else if (txt.StartsWith("$"))
                dt = DataType.Number;
            else if (
                IC10Line.IsHashExpression(txt)
                || IC10Line.IsStringExpression(txt)
                || IC10Line.IsBinaryExpression(txt)
            )
                dt = DataType.Number;
            else if (IC10Line.IsDeviceChannel(txt))
                dt = DataType.Device;
            else
                dt = DataType.Unknown;

            if (i == 0)
            {
                isInstructionLine = dt == DataType.Instruction;
                if (
                    !isInstructionLine
                    && dt != DataType.Label
                    && dt != DataType.Alias
                    && dt != DataType.Define
                    && dt != DataType.Comment
                )
                    error = $"Unknown instruction '{txt}'";
            }

            else if (isInstructionLine)
            {
                var opcode = IC10Utils.Instructions[texts[0]];
                int argIndex = i - 1;
                if (argIndex < opcode.ArgumentTypes.Count)
                {
                    var expected = opcode.ArgumentTypes[argIndex];

                    if (!expected.Compat.Has(dt))
                    {
                        error =
                            $"Invalid argument type {dt}, expected {expected.Description}";
                        dt = DataType.Unknown;
                    }
                }
                else
                {
                    error = "Too many arguments";
                }
            }

            t.Type = (uint)dt;
            t.Color = error != null ? ColorError : GetColor(dt, txt);
            t.Background = GetBackgroundColor(dt, txt);
            t.Data = error ?? tooltip;
            t.IsError = error != null;

            line.AddToken(t);
        }

        line.UpdateTokenColors(types);
    }

    public void UpdateDataType(string newToken, bool defer = true)
    {
        if (newToken != null)
            _tokensToUpdate.Add(newToken);
        if (defer || _tokensToUpdate.Count == 0)
            return;

        foreach (var token in _tokensToUpdate)
        {
            int count = 0;
            DataType type = DataType.Unknown;
            if (defines.ContainsKey(token))
            {
                count += defines[token];
                type = DataType.Number;
            }
            if (devAliases.ContainsKey(token))
            {
                count++;
                type = DataType.Device;
            }
            if (regAliases.ContainsKey(token))
            {
                count++;
                type = DataType.Register;
            }
            if (labels.ContainsKey(TrimToken(token)))
            {
                count += labels[token];
                type = DataType.Label;
            }
            if (IC10Utils.Instructions.ContainsKey(token))
            {
                count++;
                type = DataType.Instruction;
            }

            if (count > 1)
                type = DataType.Unknown;
            types[token] = type;
        }
        // In a real implementation, we would re-parse affected lines here
        _tokensToUpdate.Clear();

        foreach (IC10Line line in Lines)
            line.UpdateTokenColors(types);
    }

    public override void ResetCode(string code)
    {
        defines.Clear();
        regAliases.Clear();
        devAliases.Clear();
        labels.Clear();
        base.ResetCode(code);
    }

    public override void InsertLine(int index, string line)
    {
        base.InsertLine(index, line);
        TrackAliases(Lines[index] as IC10Line, true);
    }

    public override void AppendLine(string line)
    {
        base.AppendLine(line);
        TrackAliases(Lines[Lines.Count - 1] as IC10Line, true);
    }

    public override void RemoveLine(int index)
    {
        if (index < 0 || index >= Lines.Count)
            return;
        string lineText = Lines[index].Text;
        var line = Lines[index] as IC10Line;
        base.RemoveLine(index);
        TrackAliases(line, false);
    }

    private void TrackAliases(IC10Line line, bool add)
    {
        if (line.NumCodeTokens == 0)
            return;

        if (line.IsLabel)
            UpdateDict(labels, TrimToken(line.GetTokenText(0)), DataType.Label, add);
        else if (line.IsNumAlias)
            UpdateDict(regAliases, line.GetTokenText(1), DataType.Number, add);
        else if (line.IsDevAlias)
            UpdateDict(devAliases, line.GetTokenText(1), DataType.Device, add);
        else if (line.IsDefine)
            UpdateDict(defines, line.GetTokenText(1), DataType.Number, add);
    }

    private void UpdateDict(Dictionary<string, int> dict, string key, DataType type, bool add)
    {
        L.Debug($"UpdateDict: {(add ? "adding" : "removing")} key {key} of type {type}");
        if (add)
        {
            if (!dict.ContainsKey(key))
                dict[key] = 0;

            dict[key]++;

            if (!types.ContainsKey(key) || types[key] != type)
                UpdateDataType(key);
        }
        else
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
    }
}
