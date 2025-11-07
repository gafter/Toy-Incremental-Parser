using System;

namespace ToyIncrementalParser.Text;

/// <summary>
/// Represents a span of characters in the source text.
/// </summary>
public readonly struct TextSpan : IEquatable<TextSpan>
{
    public int Start { get; }
    public int Length { get; }

    public int End => Start + Length;

    public TextSpan(int start, int length)
    {
        if (start < 0)
            throw new ArgumentOutOfRangeException(nameof(start));
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));

        Start = start;
        Length = length;
    }

    public static TextSpan FromBounds(int start, int end)
    {
        if (end < start)
            throw new ArgumentOutOfRangeException(nameof(end));

        return new TextSpan(start, end - start);
    }

    public override string ToString() => $"[{Start}..{End})";

    public bool Equals(TextSpan other) => Start == other.Start && Length == other.Length;

    public override bool Equals(object? obj) => obj is TextSpan span && Equals(span);

    public override int GetHashCode() => HashCode.Combine(Start, Length);

    public static bool operator ==(TextSpan left, TextSpan right) => left.Equals(right);

    public static bool operator !=(TextSpan left, TextSpan right) => !left.Equals(right);
}

