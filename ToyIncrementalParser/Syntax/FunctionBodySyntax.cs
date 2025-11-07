using System.Collections.Generic;
using ToyIncrementalParser.Diagnostics;

namespace ToyIncrementalParser.Syntax;

public abstract class FunctionBodySyntax : SyntaxNode
{
    protected FunctionBodySyntax(IEnumerable<SyntaxNode> children, IEnumerable<Diagnostic>? diagnostics = null)
        : base(children, diagnostics)
    {
    }
}

