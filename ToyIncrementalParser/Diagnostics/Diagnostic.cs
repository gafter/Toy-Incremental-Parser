using System;

namespace ToyIncrementalParser.Diagnostics;

public sealed class Diagnostic
{
    public Diagnostic(string message, Range span)
    {
        Message = message;
        Span = span;
    }

    public string Message { get; }
    public Range Span { get; }

    public override string ToString()
    {
        var (offset, length) = Span.GetOffsetAndLength(int.MaxValue);
        var end = offset + length;
        return $"Error: {Message} @ [{offset}..{end})";
    }
}

