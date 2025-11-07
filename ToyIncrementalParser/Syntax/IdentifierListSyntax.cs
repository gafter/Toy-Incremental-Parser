using System.Collections.Generic;
using System.Linq;

namespace ToyIncrementalParser.Syntax;

public sealed class IdentifierListSyntax : SyntaxNode
{
    public IdentifierListSyntax(IEnumerable<SyntaxToken> identifiers, IEnumerable<SyntaxToken> separators)
        : base(Interleave(identifiers, separators))
    {
        Identifiers = identifiers.ToArray();
        Separators = separators.ToArray();
    }

    public IReadOnlyList<SyntaxToken> Identifiers { get; }
    public IReadOnlyList<SyntaxToken> Separators { get; }

    public override NodeKind Kind => NodeKind.IdentifierList;

    private static IEnumerable<SyntaxNode> Interleave(IEnumerable<SyntaxToken> identifiers, IEnumerable<SyntaxToken> separators)
    {
        using var idEnumerator = identifiers.GetEnumerator();
        using var sepEnumerator = separators.GetEnumerator();

        var hasIdentifier = idEnumerator.MoveNext();
        var hasSeparator = sepEnumerator.MoveNext();

        while (hasIdentifier || hasSeparator)
        {
            if (hasIdentifier)
            {
                yield return idEnumerator.Current;
                hasIdentifier = idEnumerator.MoveNext();
            }

            if (hasSeparator)
            {
                yield return sepEnumerator.Current;
                hasSeparator = sepEnumerator.MoveNext();
            }
        }
    }
}

