using ToyIncrementalParser.Syntax.Green;

namespace ToyIncrementalParser.Syntax;

public abstract class FunctionBodySyntax : SyntaxNode
{
    internal FunctionBodySyntax(SyntaxTree syntaxTree, SyntaxNode? parent, GreenNode green, int position)
        : base(syntaxTree, parent, green, position)
    {
    }
}
