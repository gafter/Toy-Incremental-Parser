using System.Text;
using ToyIncrementalParser.Syntax.Green;

namespace ToyIncrementalParser.Syntax;

public sealed class StringLiteralExpressionSyntax : ExpressionSyntax
{
    private SyntaxToken? _stringToken;
    private string? _value;

    internal StringLiteralExpressionSyntax(SyntaxTree syntaxTree, SyntaxNode? parent, GreenStringLiteralExpressionNode green, int position)
        : base(syntaxTree, parent, green, position)
    {
    }

    public SyntaxToken StringToken => GetRequiredToken(ref _stringToken, 0);

    public string Value => _value ??= DecodeValue(StringToken.Text);

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

