using System.Collections.Generic;
using ToyIncrementalParser.Diagnostics;

namespace ToyIncrementalParser.Syntax;

public abstract class ExpressionSyntax : SyntaxNode
{
    protected ExpressionSyntax(IEnumerable<SyntaxNode> children, IEnumerable<Diagnostic>? diagnostics = null)
        : base(children, diagnostics)
    {
    }
}

