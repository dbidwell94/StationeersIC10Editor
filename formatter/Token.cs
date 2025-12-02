namespace StationeersIC10Editor
{
    /// <summary>
    /// Represents a lightweight pointer to a range in the source buffer.
    /// Does NOT hold the text content itself.
    /// </summary>
    public class SemanticToken
    {
        public int Line; // Line index (0-based)
        public int Column; // Start column index (0-based)
        public int Length; // Length of the token
        public uint Type; // Semantic Type ID (mapped to DataType usually)
        public Style Style;

        // Metadata for tooltips/errors, managed via dictionary or side-channel in a real LSP,
        // but stored here for simplicity in this hybrid approach.
        public string Data;
        public bool IsError;

        public void SetError(string errorMessage)
        {
            IsError = true;
            Data = errorMessage;
        }

        public SemanticToken(
            int line,
            int column,
            int length,
            uint type,
            Style style = new Style(),
            string data = null,
            bool isError = false
        )
        {
            Line = line;
            Column = column;
            Length = length;
            Type = type;
            Style = style;
            Data = data;
            IsError = isError;
        }

        public uint Color {
            get => Style.Color;
            set => Style.Color = value;
        }
        public uint Background {
            get => Style.Background;
            set => Style.Background = value;
        }
    }
}
