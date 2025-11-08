using System.Collections.Generic;
using ToyIncrementalParser.Diagnostics;
using ToyIncrementalParser.Text;

namespace ToyIncrementalParser.Syntax;

public sealed class SyntaxTree
{
    private SyntaxTree(string text, ProgramSyntax root)
    {
        Text = text;
        Root = root;
        Diagnostics = root.Diagnostics;
    }

    public string Text { get; }
    public ProgramSyntax Root { get; }
    public IReadOnlyList<Diagnostic> Diagnostics { get; }

    public static SyntaxTree Parse(string text)
    {
        var parser = new Parser(text);
        var root = parser.ParseProgram();
        return new SyntaxTree(text, root);
    }

    /// <summary>
    /// Produces a new <see cref="SyntaxTree"/> that reflects a single change applied to the current tree's text.
    /// This API intentionally handles one change at a time to keep the implementation simple; it can be extended
    /// in the future to support batching.
    /// </summary>
    public SyntaxTree WithChange(TextChange change)
    {
        var updatedText = change.ApplyTo(Text);
        return Parse(updatedText);
    }

    public SyntaxTree WithChange(TextSpan span, string newText)
    {
        var change = new TextChange(span, newText);
        return WithChange(change);
    }
}

