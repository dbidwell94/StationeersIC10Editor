namespace StationeersIC10Editor;

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

/**
 * <summary>This is a thin struct which represents metadata about text at a specific
 * column and line. This also might contain tooltip text.
 * </summary>
 */
public struct SemanticToken
{
    public uint Column;
    public uint Length;
    public uint Line;
    public uint TokenType;

#nullable enable
    public string? TooltipData;

#nullable disable

    public SemanticToken(uint column, uint length, uint line, uint type)
    {
        Column = column;
        Line = line;
        Length = length;
        TokenType = type;
    }

    public SemanticToken(uint column, uint line, uint length, uint type, string tooltipData)
        : this(column, length, line, type)
    {
        TooltipData = tooltipData;
    }
}
