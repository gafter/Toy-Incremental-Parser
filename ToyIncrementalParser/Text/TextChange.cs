using System;

namespace ToyIncrementalParser.Text;

/// <summary>
/// Represents a single change to a source text.
/// Contains the span of deleted text and the length of the replacement text.
/// </summary>
public readonly struct TextChange
{
    public TextChange(Range span, int newLength)
    {
        Span = span;
        ArgumentOutOfRangeException.ThrowIfNegative(newLength);
        NewLength = newLength;
    }

    public Range Span { get; }

    public int NewLength { get; }

    public TextChange(int start, int length, int newLength)
        : this(start..(start + length), newLength)
    {
    }

    public IText ApplyTo(IText text, IText newText)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(newText);

        if (newText.Length != NewLength)
            throw new ArgumentException($"New text length {newText.Length} does not match expected length {NewLength}.", nameof(newText));

        var (offset, length) = Span.GetOffsetAndLength(text.Length);
        var end = offset + length;

        if (end > text.Length)
            throw new ArgumentOutOfRangeException(nameof(Span), "Change span exceeds the length of the text.");

        var textRope = Rope.ForText(text);
        var before = textRope.SubText(0, offset);
        var newTextRope = Rope.ForText(newText);
        var after = textRope.SubText(end, text.Length - end);

        return Rope.Concat(Rope.Concat(before, newTextRope), after);
    }

    public Rope ApplyTo(Rope text, Rope newText)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(newText);
        var result = ApplyTo((IText)text, (IText)newText);
        return (Rope)result;
    }

    // public static TextChange FromTextDifference(IText oldText, IText newText)
    // {
    //     ArgumentNullException.ThrowIfNull(oldText);
    //     ArgumentNullException.ThrowIfNull(newText);

    //     if (oldText.Length == 0 && newText.Length == 0)
    //         return new TextChange(0..0, 0);

    //     // Compare texts for equality
    //     if (oldText.Length == newText.Length)
    //     {
    //         bool equal = true;
    //         for (int i = 0; i < oldText.Length; i++)
    //         {
    //             if (oldText[i] != newText[i])
    //             {
    //                 equal = false;
    //                 break;
    //             }
    //         }
    //         if (equal)
    //             return new TextChange(oldText.Length..oldText.Length, 0);
    //     }

    //     var start = 0;
    //     var oldLength = oldText.Length;
    //     var newLength = newText.Length;
    //     var max = Math.Min(oldLength, newLength);

    //     // Discard common prefix.
    //     while (start < max && oldText[start] == newText[start])
    //         start++;

    //     var oldEnd = oldLength;
    //     var newEnd = newLength;

    //     // Discard common suffix.
    //     while (oldEnd > start && newEnd > start && oldText[oldEnd - 1] == newText[newEnd - 1])
    //     {
    //         oldEnd--;
    //         newEnd--;
    //     }

    //     var span = start..oldEnd;
    //     var replacementLength = newEnd - start;
    //     return new TextChange(span, replacementLength);
    // }

    // public static TextChange FromTextDifference(Rope oldText, Rope newText)
    // {
    //     ArgumentNullException.ThrowIfNull(oldText);
    //     ArgumentNullException.ThrowIfNull(newText);
    //     return FromTextDifference((IText)oldText, (IText)newText);
    // }
}


