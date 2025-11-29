namespace StationeersIC10Editor;

public class PlainTextFormatter : ICodeFormatter
{
    public static double MatchingScore(string line)
    {
        // Plain text formatter matches any line, but with low score.
        return 0.001;
    }

    public override Line ParseLine(string line)
    {
        var l = new Line(line);
        // Plain text has no semantic tokens, so it will be drawn using the default logic (white/default color).
        return l;
    }
}
