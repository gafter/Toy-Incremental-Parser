using System;

namespace ToyIncrementalParser.Diagnostics;

public sealed class Diagnostic
{
    public Diagnostic(string message, Range span)
    {
        if (span.Start.IsFromEnd || span.End.IsFromEnd)
            throw new ArgumentException("Span must use absolute positions, not relative end positions.", nameof(span));
        Message = message;
        Span = span;
    }

    public string Message { get; }
    public Range Span { get; }

    public override string ToString()
    {
        var offset = Span.Start.Value;
        var end = Span.End.Value;
        return $"Error: {Message} @ [{offset}..{end})";
    }
}
