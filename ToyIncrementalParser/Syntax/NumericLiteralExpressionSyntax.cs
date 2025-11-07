using System;
using System.Globalization;

namespace ToyIncrementalParser.Syntax;

public sealed class NumericLiteralExpressionSyntax : ExpressionSyntax
{
    public NumericLiteralExpressionSyntax(SyntaxToken numberToken)
        : base(new SyntaxNode[] { numberToken })
    {
        NumberToken = numberToken ?? throw new ArgumentNullException(nameof(numberToken));
        Value = ParseValue(numberToken.Text);
    }

    public SyntaxToken NumberToken { get; }

    public double Value { get; }

    public override NodeKind Kind => NodeKind.NumericLiteralExpression;

    private static double ParseValue(string text)
    {
        if (double.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
            return value;

        return 0;
    }
}

