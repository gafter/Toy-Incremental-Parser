using System.Collections.Generic;
using ToyIncrementalParser.Diagnostics;

namespace ToyIncrementalParser.Syntax;

public abstract class StatementSyntax : SyntaxNode
{
    protected StatementSyntax(IEnumerable<SyntaxNode> children, IEnumerable<Diagnostic>? diagnostics = null)
        : base(children, diagnostics)
    {
    }
}

