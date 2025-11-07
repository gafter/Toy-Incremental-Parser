using System.Collections.Generic;
using ToyIncrementalParser.Diagnostics;

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
}

