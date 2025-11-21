namespace StationeersIC10Editor
{
    using System;
    using System.Collections.Generic;
    using UnityEngine;

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


    public static class CodeFormatters
    {
        private static Dictionary<string, Func<ICodeFormatter>> formatters = new Dictionary<string, Func<ICodeFormatter>>();

        public static void RegisterFormatter(string name, Func<ICodeFormatter> formatter)
        {
            L.Info($"Registering code formatter: {name}");
            if (!formatters.ContainsKey(name))
                formatters.Add(name, formatter);
        }

        public static ICodeFormatter GetFormatter(string name)
        {
            if (formatters.ContainsKey(name))
                return formatters[name]();
            L.Warning($"Code formatter not found: {name}");
            if (formatters.Count > 0)
            {
                foreach (var fmt in formatters)
                {
                    L.Warning($"Returning first available formatter: {fmt.Key}");
                    return fmt.Value();
                }
            }
            L.Warning("No code formatters registered");
            return null;
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

        public const float LineNumberOffset = 5.3f;

        public abstract void ResetCode(string code);
        public abstract void RemoveLine(int index);
        public abstract void InsertLine(int index, string line);
        public abstract void AppendLine(string line);
        public abstract bool DrawTooltip(string line, TextPosition caret, Vector2 pos);
        public abstract void DrawAutocomplete(IEditor ed, TextPosition caret, Vector2 pos);
        public abstract string GetAutocompleteSuggestion();


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
        public abstract void DrawStatus(IEditor ed, TextPosition caret);

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

}
