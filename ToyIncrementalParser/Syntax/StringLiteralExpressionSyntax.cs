using System;
using System.Text;

namespace ToyIncrementalParser.Syntax;

public sealed class StringLiteralExpressionSyntax : ExpressionSyntax
{
    public StringLiteralExpressionSyntax(SyntaxToken stringToken)
        : base(new SyntaxNode[] { stringToken })
    {
        StringToken = stringToken ?? throw new ArgumentNullException(nameof(stringToken));
        Value = DecodeValue(stringToken.Text);
    }

    public SyntaxToken StringToken { get; }

    public string Value { get; }

    public override NodeKind Kind => NodeKind.StringLiteralExpression;

    private static string DecodeValue(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length < 2 || text[0] != '"' || text[^1] != '"')
            return string.Empty;

        var builder = new StringBuilder(text.Length - 2);
        for (var i = 1; i < text.Length - 1; i++)
        {
            var ch = text[i];
            if (ch == '\\' && i + 1 < text.Length - 1)
            {
                var escape = text[++i];
                builder.Append(escape switch
                {
                    '"' => '"',
                    '\\' => '\\',
                    'n' => '\n',
                    _ => escape
                });
            }
            else
            {
                builder.Append(ch);
            }
        }

        return builder.ToString();
    }
}

