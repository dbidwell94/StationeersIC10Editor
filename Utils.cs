namespace StationeersIC10Editor
{
    public struct TextRange
    {
        public TextPosition Start;
        public TextPosition End;

        public TextRange(TextPosition start, TextPosition end)
        {
            Start = start;
            End = end;
        }

        public TextRange Sorted()
        {
            TextRange range = this;
            if (End < Start)
            {
                range.Start = End;
                range.End = Start;
            }

            return range;
        }

        public void Reset()
        {
            Start.Reset();
            End.Reset();
        }

        public static explicit operator bool(TextRange range)
        {
            return (bool)range.Start && (bool)range.End && (range.Start != range.End);
        }

        public static bool operator true(TextRange range)
        {
            return (bool)range;
        }

        public static bool operator false(TextRange range)
        {
            return (bool)range;
        }

        public override string ToString()
        {
            return $"[{Start} - {End}]";
        }
    }

    public struct TextPosition
    {
        public int Line;
        public int Col;

        public static bool operator ==(TextPosition a, TextPosition b)
        {
            return a.Line == b.Line && a.Col == b.Col;
        }

        public static bool operator !=(TextPosition a, TextPosition b)
        {
            return !(a == b);
        }

        public static bool operator <(TextPosition a, TextPosition b)
        {
            if (a.Line < b.Line)
            {
                return true;
            }

            if (a.Line == b.Line && a.Col < b.Col)
            {
                return true;
            }

            return false;
        }

        public static bool operator >(TextPosition a, TextPosition b)
        {
            return !(a < b) && (a != b);
        }

        public TextPosition(int line = -1, int column = -1)
        {
            Line = line;
            Col = column;
        }

        public void Reset()
        {
            Line = -1;
            Col = -1;
        }

        public static explicit operator bool(TextPosition pos)
        {
            return pos.Line >= 0 && pos.Col >= 0;
        }

        public static bool operator true(TextPosition pos)
        {
            return (bool)pos;
        }

        public static bool operator false(TextPosition pos)
        {
            return (bool)pos;
        }

        public override string ToString()
        {
            return $"({Line}, {Col})";
        }

        public override bool Equals(object obj)
        {
            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            var other = (TextPosition)obj;

            return (other.Col == this.Col && other.Line == this.Line);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 397 + Line;
                hash = hash * 397 + Col;
                return hash;
            }
        }
    }
}
