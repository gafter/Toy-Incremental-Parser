using System.Collections.Generic;
using System.Collections.Generic;
using ToyIncrementalParser.Diagnostics;
using ToyIncrementalParser.Syntax.Green;
using ToyIncrementalParser.Syntax.Incremental;
using ToyIncrementalParser.Text;

namespace ToyIncrementalParser.Syntax;

public sealed class SyntaxTree
{
    private ProgramSyntax? _root;
    private IReadOnlyList<Diagnostic>? _diagnostics;

    private SyntaxTree(string text, GreenProgramNode root)
    {
        Text = text;
        GreenRoot = root;
    }

    public string Text { get; }

    internal GreenProgramNode GreenRoot { get; }

    public ProgramSyntax Root => _root ??= (ProgramSyntax)SyntaxNodeFactory.Create(this, parent: null, GreenRoot, position: 0);

    public IReadOnlyList<Diagnostic> Diagnostics => _diagnostics ??= Root.Diagnostics;

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
        var blendedRoot = IncrementalBlender.Blend(GreenRoot, Text, updatedText, change);
        return new SyntaxTree(updatedText, blendedRoot);
    }

    public SyntaxTree WithChange(TextSpan span, string newText)
    {
        var change = new TextChange(span, newText);
        return WithChange(change);
    }

    internal SyntaxToken CreateToken(SyntaxNode? parent, GreenToken token, int position) =>
        SyntaxToken.Create(this, parent, token, position);
}

