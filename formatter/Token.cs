namespace StationeersIC10Editor
{
    // The Token class is kept for backward compatibility if other parts of the system
    // rely on it, but the new system uses SemanticToken struct primarily.
    public class Token
    {
        public string Text;
        public int Column;
        public uint Color;
        public uint Background;
        public string Tooltip;
        public string Error;
        public string Status;

        public Token(
            string text,
            int column,
            uint color = 0xFFFFFFFF,
            uint background = 0,
            string tooltip = null,
            string error = null
        )
        {
            Text = text;
            Column = column;
            Color = color;
            Background = background;
            Tooltip = tooltip;
            Error = error;
        }
    }

    /// <summary>
    /// Represents a lightweight pointer to a range in the source buffer.
    /// Does NOT hold the text content itself.
    /// </summary>
    public struct SemanticToken
    {
        public int Line; // Line index (0-based)
        public int Column; // Start column index (0-based)
        public int Length; // Length of the token
        public uint Color; // Render color (ARGB)
        public uint Background; // Background color (ARGB)
        public uint Type; // Semantic Type ID (mapped to DataType usually)

        // Metadata for tooltips/errors, managed via dictionary or side-channel in a real LSP,
        // but stored here for simplicity in this hybrid approach.
        public string Data;
        public bool IsError;

        public SemanticToken(
            int line,
            int column,
            int length,
            uint color,
            uint type,
            uint background = 0,
            string data = null,
            bool isError = false
        )
        {
            Line = line;
            Column = column;
            Length = length;
            Color = color;
            Type = type;
            Background = background;
            Data = data;
            IsError = isError;
        }
    }
}
