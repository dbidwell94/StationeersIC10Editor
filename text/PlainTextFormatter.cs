namespace StationeersIC10Editor;

public class PlainTextFormatter : ICodeFormatter
{
    public override Line ParseLine(string line)
    {
        var l = new Line(line);
        // Plain text has no semantic tokens, so it will be drawn using the default logic (white/default color).
        return l;
    }
}
