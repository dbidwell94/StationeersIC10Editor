namespace StationeersIC10Editor.IC10;

using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

public class IC10CodeFormatter : ICodeFormatter
{
    private Dictionary<string, DataType> types = new Dictionary<string, DataType>();
    private Dictionary<string, int> defines = new Dictionary<string, int>();
    private Dictionary<string, int> regAliases = new Dictionary<string, int>();
    private Dictionary<string, int> devAliases = new Dictionary<string, int>();
    private Dictionary<string, int> labels = new Dictionary<string, int>();
    private HashSet<string> _tokensToUpdate = new HashSet<string>();

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

        IdentifyTypesAndAddTokens(line, logicTokens, tokenTexts, types);

        return line;
    }

    public void IdentifyTypesAndAddTokens(
        Line line,
        List<SemanticToken> tokens,
        List<string> texts,
        Dictionary<string, DataType> globalTypes
    )
    {
        if (tokens.Count == 0)
            return;

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
            else if (globalTypes.TryGetValue(txt, out DataType type))
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

            bool isInstructionLine =
                tokens.Count > 0 && IC10Utils.Instructions.ContainsKey(texts[0]);

            if (isInstructionLine)
            {
                if (i == 0)
                    dt = DataType.Instruction;

                if (i > 0)
                {
                    var opcode = IC10Utils.Instructions[texts[0]];
                    int argIndex = i - 1;
                    if (argIndex < opcode.ArgumentTypes.Count)
                    {
                        var expected = opcode.ArgumentTypes[argIndex];
                        var actualType = IC10Utils.GetType(txt);
                        actualType.Add(dt);

                        if (!expected.Compat.Has(actualType))
                        {
                            error =
                                $"Invalid argument type {actualType.Description}, expected {expected.Description}";
                            dt = DataType.Unknown;
                        }
                    }
                    else
                    {
                        error = "Too many arguments";
                    }
                }
            }
            else
            {
                if (i == 0)
                {
                    if (
                        dt != DataType.Label
                        && dt != DataType.Alias
                        && dt != DataType.Define
                        && dt != DataType.Comment
                    )
                        error = $"Unknown instruction '{txt}'";
                }
            }

            t.Type = (uint)dt;
            t.Color = error != null ? ColorError : GetColor(dt, txt);
            t.Background = GetBackgroundColor(dt, txt);
            t.Data = error ?? tooltip;
            t.IsError = error != null;

            line.AddToken(t);
        }
    }

    // Fixed ParseColoredText to return FormattedText (Collection of Lines)
    public static FormattedText ParseColoredText(string input, ref float width)
    {
        var result = new FormattedText();
        var lines = input.Split('\n');
        var C = (Func<string, uint>)((string color) => ColorFromHTML(color));

        foreach (var lineStr in lines)
        {
            var regex = new Regex(@"<color=(.*?)>(.*?)</color>", RegexOptions.Singleline);
            int lastIndex = 0;

            // We need to build the "Clean" text for the Line content
            StringBuilder cleanText = new StringBuilder();
            // We need to track where semantic tokens map to the clean text
            List<SemanticToken> tokens = new List<SemanticToken>();

            int currentColumn = 0;

            foreach (Match match in regex.Matches(lineStr))
            {
                // Plain text before match
                if (match.Index > lastIndex)
                {
                    string rawText = lineStr.Substring(lastIndex, match.Index - lastIndex);
                    cleanText.Append(rawText);
                    // Add a token for plain text with default color
                    tokens.Add(
                        new SemanticToken(0, currentColumn, rawText.Length, C("#ffffff"), 0)
                    );
                    currentColumn += rawText.Length;
                }

                // Colored text
                string colorStr = match.Groups[1].Value;
                string text = match.Groups[2].Value;
                cleanText.Append(text);
                tokens.Add(new SemanticToken(0, currentColumn, text.Length, C(colorStr), 0));
                currentColumn += text.Length;

                lastIndex = match.Index + match.Length;
            }

            // Plain text after last match
            if (lastIndex < lineStr.Length)
            {
                string rawText = lineStr.Substring(lastIndex);
                cleanText.Append(rawText);
                tokens.Add(new SemanticToken(0, currentColumn, rawText.Length, C("#ffffff"), 0));
            }

            // Create the Line with clean text
            var resultLine = new Line(cleanText.ToString());
            foreach (var t in tokens)
                resultLine.AddToken(t);

            result.Add(resultLine);
        }
        return result;
    }

    public void UpdateDataType(string newToken, bool defer = true)
    {
        if (newToken != null)
            _tokensToUpdate.Add(newToken);
        if (defer)
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
        TrackAliases(line, true);
    }

    public override void RemoveLine(int index)
    {
        if (index < 0 || index >= Lines.Count)
            return;
        string lineText = Lines[index].Text;
        base.RemoveLine(index);
        TrackAliases(lineText, false);
    }

    private void TrackAliases(string line, bool add)
    {
        string[] parts = line.Split(
            new char[] { ' ', '\t' },
            StringSplitOptions.RemoveEmptyEntries
        );
        if (parts.Length == 0)
            return;
        string cmd = parts[0];
        if (cmd == "alias" && parts.Length >= 3)
        {
            string name = parts[1];
            string target = parts[2];
            if (target.StartsWith("d"))
                UpdateDict(devAliases, name, add);
            else
                UpdateDict(regAliases, name, add);
            UpdateDataType(name, false);
        }
        else if (cmd == "define" && parts.Length >= 3)
        {
            string name = parts[1];
            UpdateDict(defines, name, add);
            UpdateDataType(name, false);
        }
        else if (cmd.EndsWith(":"))
        {
            string label = TrimToken(cmd);
            UpdateDict(labels, label, add);
            UpdateDataType(label, false);
        }
    }

    private void UpdateDict(Dictionary<string, int> dict, string key, bool add)
    {
        if (!dict.ContainsKey(key))
            dict[key] = 0;
        if (add)
            dict[key]++;
        else
            dict[key]--;
        if (dict[key] <= 0)
            dict.Remove(key);
    }
}
