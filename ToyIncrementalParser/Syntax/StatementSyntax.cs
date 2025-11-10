using ToyIncrementalParser.Syntax.Green;

namespace ToyIncrementalParser.Syntax;

public abstract class StatementSyntax : SyntaxNode
{
    internal StatementSyntax(SyntaxTree syntaxTree, SyntaxNode? parent, GreenNode green, int position)
        : base(syntaxTree, parent, green, position)
    {
    }
}

