using System.Collections.Generic;
using System.Linq;

namespace ToyIncrementalParser.Syntax;

public sealed class ExpressionListSyntax : SyntaxNode
{
    public ExpressionListSyntax(IEnumerable<ExpressionSyntax> expressions, IEnumerable<SyntaxToken> separators)
        : base(Interleave(expressions, separators))
    {
        Expressions = expressions.ToArray();
        Separators = separators.ToArray();
    }

    public IReadOnlyList<ExpressionSyntax> Expressions { get; }
    public IReadOnlyList<SyntaxToken> Separators { get; }

    public override NodeKind Kind => NodeKind.ExpressionList;

    private static IEnumerable<SyntaxNode> Interleave(IEnumerable<ExpressionSyntax> expressions, IEnumerable<SyntaxToken> separators)
    {
        using var exprEnumerator = expressions.GetEnumerator();
        using var sepEnumerator = separators.GetEnumerator();

        var hasExpression = exprEnumerator.MoveNext();
        var hasSeparator = sepEnumerator.MoveNext();

        while (hasExpression || hasSeparator)
        {
            if (hasExpression)
            {
                yield return exprEnumerator.Current;
                hasExpression = exprEnumerator.MoveNext();
            }

            if (hasSeparator)
            {
                yield return sepEnumerator.Current;
                hasSeparator = sepEnumerator.MoveNext();
            }
        }
    }
}

