using System;
using System.Collections.Generic;
using ToyIncrementalParser.Diagnostics;
using ToyIncrementalParser.Parser;
using ToyIncrementalParser.Syntax.Green;
using ToyIncrementalParser.Text;

namespace ToyIncrementalParser.Syntax;

public sealed class SyntaxTree
{
    private ProgramSyntax? _root;

    private SyntaxTree(IText text, GreenProgramNode root)
    {
        Text = text;
        GreenRoot = root;
    }

    public IText Text { get; }

    internal GreenProgramNode GreenRoot { get; }

    public ProgramSyntax Root => _root ??= (ProgramSyntax)SyntaxNodeFactory.Create(this, parent: null, GreenRoot, position: 0);

    public IReadOnlyList<Diagnostic> Diagnostics => Root.Diagnostics;

    public static SyntaxTree Parse(IText text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var parser = new ToyIncrementalParser.Parser.Parser(new LexingSymbolStream(text));
        var root = parser.ParseProgram();
        return new SyntaxTree(text, root);
    }

    public static SyntaxTree Parse(Rope rope)
    {
        return Parse((IText)rope);
    }

    /// <summary>
    /// Produces a new <see cref="SyntaxTree"/> that reflects a single change applied to the current tree's text.
    /// This API intentionally handles one change at a time to keep the implementation simple; it can be extended
    /// in the future to support batching.
    /// </summary>
    public SyntaxTree WithChange(TextChange change, IText newText)
    {
        ArgumentNullException.ThrowIfNull(newText);
        var updatedText = change.ApplyTo(Text, newText);
        var stream = new Blender(GreenRoot, updatedText, change);
        var parser = new ToyIncrementalParser.Parser.Parser(stream);
        var blendedRoot = parser.ParseProgram();
        return new SyntaxTree(updatedText, blendedRoot);
    }

    public SyntaxTree WithChange(TextChange change, Rope newText)
    {
        return WithChange(change, (IText)newText);
    }

    internal SyntaxToken CreateToken(SyntaxNode? parent, GreenToken token, int position) =>
        SyntaxToken.Create(this, parent, token, position);
}

