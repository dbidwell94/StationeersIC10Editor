namespace StationeersIC10Editor
{
    public class PlainTextFormatter : ICodeFormatter
    {
        public override Line ParseLine(string line)
        {
            // every line is a single token with no special formatting
            return new Line(line);
        }
    }
}
