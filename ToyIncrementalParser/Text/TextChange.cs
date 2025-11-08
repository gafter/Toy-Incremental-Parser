using System;

namespace ToyIncrementalParser.Text;

/// <summary>
/// Represents a single change to a source text.
/// </summary>
public readonly struct TextChange
{
    public TextChange(TextSpan span, string newText)
    {
        Span = span;
        NewText = newText ?? throw new ArgumentNullException(nameof(newText));
    }

    public TextSpan Span { get; }

    public string NewText { get; }

    public TextChange(int start, int length, string newText)
        : this(new TextSpan(start, length), newText)
    {
    }

    public string ApplyTo(string text)
    {
        if (text is null)
            throw new ArgumentNullException(nameof(text));

        if (Span.End > text.Length)
            throw new ArgumentOutOfRangeException(nameof(Span), "Change span exceeds the length of the text.");

        return string.Concat(text.AsSpan(0, Span.Start), NewText, text.AsSpan(Span.End));
    }

    public static TextChange FromTextDifference(string oldText, string newText)
    {
        if (oldText is null)
            throw new ArgumentNullException(nameof(oldText));
        if (newText is null)
            throw new ArgumentNullException(nameof(newText));

        if (oldText.Length == 0 && newText.Length == 0)
            return new TextChange(new TextSpan(0, 0), string.Empty);

        if (oldText == newText)
            return new TextChange(new TextSpan(oldText.Length, 0), string.Empty);

        var start = 0;
        var oldLength = oldText.Length;
        var newLength = newText.Length;
        var max = Math.Min(oldLength, newLength);

        while (start < max && oldText[start] == newText[start])
            start++;

        var oldEnd = oldLength;
        var newEnd = newLength;

        while (oldEnd > start && newEnd > start && oldText[oldEnd - 1] == newText[newEnd - 1])
        {
            oldEnd--;
            newEnd--;
        }

        var span = new TextSpan(start, oldEnd - start);
        var replacement = newText.Substring(start, newEnd - start);
        return new TextChange(span, replacement);
    }
}


