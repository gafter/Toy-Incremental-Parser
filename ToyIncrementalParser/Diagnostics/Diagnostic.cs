using ToyIncrementalParser.Text;

namespace ToyIncrementalParser.Diagnostics;

public sealed class Diagnostic
{
    public Diagnostic(DiagnosticSeverity severity, string message, TextSpan span)
    {
        Severity = severity;
        Message = message;
        Span = span;
    }

    public DiagnosticSeverity Severity { get; }
    public string Message { get; }
    public TextSpan Span { get; }

    public override string ToString() => $"{Severity}: {Message} @ {Span}";
}

