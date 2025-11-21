using System.Globalization;
using ToyIncrementalParser.Syntax.Green;

namespace ToyIncrementalParser.Syntax;

public sealed class NumericLiteralExpressionSyntax : ExpressionSyntax
{
    private SyntaxToken? _numberToken;
    private double? _value;

    internal NumericLiteralExpressionSyntax(SyntaxTree syntaxTree, SyntaxNode? parent, GreenNumericLiteralExpressionNode green, int position)
        : base(syntaxTree, parent, green, position)
    {
    }

    public SyntaxToken NumberToken => GetRequiredToken(ref _numberToken, 0);

    public double Value
    {
        get
        {
            if (_value is null)
                _value = ParseValue(NumberToken.Text);
            return _value.Value;
        }
    }

    public override NodeKind Kind => NodeKind.NumericLiteralExpression;

    private static double ParseValue(string text)
    {
        if (double.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
            return value;

        return 0;
    }
}
