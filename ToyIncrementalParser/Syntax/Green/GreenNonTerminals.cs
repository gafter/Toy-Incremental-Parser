using System.Collections.Generic;
using System.Linq;
using ToyIncrementalParser.Diagnostics;

namespace ToyIncrementalParser.Syntax.Green;

internal sealed class GreenProgramNode : GreenInternalNode
{
    public GreenProgramNode(GreenNode statements, GreenToken endOfFile)
        : base(NodeKind.Program, new GreenNode?[] { statements, endOfFile })
    {
    }

    public GreenNode Statements => Children[0]!;

    public GreenToken EndOfFileToken => (GreenToken)Children[1]!;
}

internal sealed class GreenStatementListNode : GreenInternalNode
{
    public GreenStatementListNode(IReadOnlyList<GreenNode> statements)
        : base(NodeKind.StatementList, statements.ToArray())
    {
    }

    public IReadOnlyList<GreenNode> Statements => Children.Cast<GreenNode>().ToArray();
}

internal sealed class GreenPrintStatementNode : GreenInternalNode
{
    public GreenPrintStatementNode(GreenToken printKeyword, GreenNode expression, GreenToken semicolon)
        : base(NodeKind.PrintStatement, new GreenNode?[] { printKeyword, expression, semicolon })
    {
    }

    public GreenToken PrintKeyword => (GreenToken)Children[0]!;
    public GreenNode Expression => Children[1]!;
    public GreenToken SemicolonToken => (GreenToken)Children[2]!;
}

internal sealed class GreenReturnStatementNode : GreenInternalNode
{
    public GreenReturnStatementNode(GreenToken returnKeyword, GreenNode expression, GreenToken semicolon)
        : base(NodeKind.ReturnStatement, new GreenNode?[] { returnKeyword, expression, semicolon })
    {
    }

    public GreenToken ReturnKeyword => (GreenToken)Children[0]!;
    public GreenNode Expression => Children[1]!;
    public GreenToken SemicolonToken => (GreenToken)Children[2]!;
}

internal sealed class GreenAssignmentStatementNode : GreenInternalNode
{
    public GreenAssignmentStatementNode(GreenToken letKeyword, GreenToken identifier, GreenToken equalsToken, GreenNode expression, GreenToken semicolon)
        : base(NodeKind.AssignmentStatement, new GreenNode?[] { letKeyword, identifier, equalsToken, expression, semicolon })
    {
    }

    public GreenToken LetKeyword => (GreenToken)Children[0]!;
    public GreenToken Identifier => (GreenToken)Children[1]!;
    public GreenToken EqualsToken => (GreenToken)Children[2]!;
    public GreenNode Expression => Children[3]!;
    public GreenToken SemicolonToken => (GreenToken)Children[4]!;
}

internal sealed class GreenExpressionBodyNode : GreenInternalNode
{
    public GreenExpressionBodyNode(GreenToken equalsToken, GreenNode expression, GreenToken semicolon)
        : base(NodeKind.ExpressionBody, new GreenNode?[] { equalsToken, expression, semicolon })
    {
    }

    public GreenToken EqualsToken => (GreenToken)Children[0]!;
    public GreenNode Expression => Children[1]!;
    public GreenToken SemicolonToken => (GreenToken)Children[2]!;
}

internal sealed class GreenStatementBodyNode : GreenInternalNode
{
    public GreenStatementBodyNode(GreenToken beginToken, GreenNode statements, GreenToken endToken)
        : base(NodeKind.StatementBody, new GreenNode?[] { beginToken, statements, endToken })
    {
    }

    public GreenToken BeginKeyword => (GreenToken)Children[0]!;
    public GreenNode Statements => Children[1]!;
    public GreenToken EndKeyword => (GreenToken)Children[2]!;
}

internal sealed class GreenFunctionDefinitionNode : GreenInternalNode
{
    public GreenFunctionDefinitionNode(
        GreenToken defineKeyword,
        GreenToken identifier,
        GreenToken openParen,
        GreenNode parameters,
        GreenToken closeParen,
        GreenNode body)
        : base(NodeKind.FunctionDefinition, new GreenNode?[] { defineKeyword, identifier, openParen, parameters, closeParen, body })
    {
    }

    public GreenToken DefineKeyword => (GreenToken)Children[0]!;
    public GreenToken Identifier => (GreenToken)Children[1]!;
    public GreenToken OpenParenToken => (GreenToken)Children[2]!;
    public GreenNode Parameters => Children[3]!;
    public GreenToken CloseParenToken => (GreenToken)Children[4]!;
    public GreenNode Body => Children[5]!;
}

internal sealed class GreenConditionalStatementNode : GreenInternalNode
{
    public GreenConditionalStatementNode(
        GreenToken ifKeyword,
        GreenNode condition,
        GreenToken thenKeyword,
        GreenNode thenStatements,
        GreenToken elseKeyword,
        GreenNode elseStatements,
        GreenToken fiKeyword)
        : base(NodeKind.ConditionalStatement, new GreenNode?[] { ifKeyword, condition, thenKeyword, thenStatements, elseKeyword, elseStatements, fiKeyword })
    {
    }

    public GreenToken IfKeyword => (GreenToken)Children[0]!;
    public GreenNode Condition => Children[1]!;
    public GreenToken ThenKeyword => (GreenToken)Children[2]!;
    public GreenNode ThenStatements => Children[3]!;
    public GreenToken ElseKeyword => (GreenToken)Children[4]!;
    public GreenNode ElseStatements => Children[5]!;
    public GreenToken FiKeyword => (GreenToken)Children[6]!;
}

internal sealed class GreenLoopStatementNode : GreenInternalNode
{
    public GreenLoopStatementNode(
        GreenToken whileKeyword,
        GreenNode condition,
        GreenToken doKeyword,
        GreenNode body,
        GreenToken odKeyword)
        : base(NodeKind.LoopStatement, new GreenNode?[] { whileKeyword, condition, doKeyword, body, odKeyword })
    {
    }

    public GreenToken WhileKeyword => (GreenToken)Children[0]!;
    public GreenNode Condition => Children[1]!;
    public GreenToken DoKeyword => (GreenToken)Children[2]!;
    public GreenNode Body => Children[3]!;
    public GreenToken OdKeyword => (GreenToken)Children[4]!;
}

internal sealed class GreenErrorStatementNode : GreenInternalNode
{
    public GreenErrorStatementNode(IReadOnlyList<GreenToken> tokens, IReadOnlyList<Diagnostic>? diagnostics = null)
        : base(NodeKind.ErrorStatement, tokens.ToArray(), diagnostics)
    {
    }

    public IReadOnlyList<GreenToken> Tokens => System.Array.AsReadOnly(Children.Cast<GreenToken>().ToArray());
}

internal sealed class GreenExpressionListNode : GreenInternalNode
{
    public GreenExpressionListNode(IReadOnlyList<GreenNode> expressions, IReadOnlyList<GreenToken> separators)
        : base(NodeKind.ExpressionList, BuildChildren(expressions, separators))
    {
        Expressions = ToArray(expressions);
        Separators = ToArray(separators);
    }

    public IReadOnlyList<GreenNode> Expressions { get; }
    public IReadOnlyList<GreenToken> Separators { get; }

    private static GreenNode?[] BuildChildren(IReadOnlyList<GreenNode> expressions, IReadOnlyList<GreenToken> separators)
    {
        var total = expressions.Count + separators.Count;
        if (total == 0)
            return System.Array.Empty<GreenNode?>();

        var array = new GreenNode?[total];
        var index = 0;

        for (var i = 0; i < expressions.Count; i++)
        {
            array[index++] = expressions[i];
            if (i < separators.Count)
                array[index++] = separators[i];
        }

        for (var i = expressions.Count; i < separators.Count; i++)
            array[index++] = separators[i];

        return array;
    }

    private static GreenNode[] ToArray(IReadOnlyList<GreenNode> expressions) => expressions.ToArray();

    private static GreenToken[] ToArray(IReadOnlyList<GreenToken> tokens) => tokens.ToArray();
}

internal sealed class GreenIdentifierListNode : GreenInternalNode
{
    public GreenIdentifierListNode(IReadOnlyList<GreenToken> identifiers, IReadOnlyList<GreenToken> separators)
        : base(NodeKind.IdentifierList, BuildChildren(identifiers, separators))
    {
        Identifiers = ToArray(identifiers);
        Separators = ToArray(separators);
    }

    public IReadOnlyList<GreenToken> Identifiers { get; }
    public IReadOnlyList<GreenToken> Separators { get; }

    private static GreenNode?[] BuildChildren(IReadOnlyList<GreenToken> identifiers, IReadOnlyList<GreenToken> separators)
    {
        var count = identifiers.Count + separators.Count;
        if (count == 0)
            return System.Array.Empty<GreenNode?>();

        var array = new GreenNode?[count];
        var index = 0;
        for (var i = 0; i < identifiers.Count; i++)
        {
            array[index++] = identifiers[i];
            if (i < separators.Count)
                array[index++] = separators[i];
        }

        return array;
    }

    private static GreenToken[] ToArray(IReadOnlyList<GreenToken> tokens) => tokens.ToArray();
}

internal sealed class GreenBinaryExpressionNode : GreenInternalNode
{
    public GreenBinaryExpressionNode(GreenNode left, GreenToken operatorToken, GreenNode right)
        : base(NodeKind.BinaryExpression, new GreenNode?[] { left, operatorToken, right })
    {
    }

    public GreenNode Left => Children[0]!;
    public GreenToken OperatorToken => (GreenToken)Children[1]!;
    public GreenNode Right => Children[2]!;
}

internal sealed class GreenUnaryExpressionNode : GreenInternalNode
{
    public GreenUnaryExpressionNode(GreenToken operatorToken, GreenNode operand)
        : base(NodeKind.UnaryExpression, new GreenNode?[] { operatorToken, operand })
    {
    }

    public GreenToken OperatorToken => (GreenToken)Children[0]!;
    public GreenNode Operand => Children[1]!;
}

internal sealed class GreenParenthesizedExpressionNode : GreenInternalNode
{
    public GreenParenthesizedExpressionNode(GreenToken openParen, GreenNode expression, GreenToken closeParen)
        : base(NodeKind.ParenthesizedExpression, new GreenNode?[] { openParen, expression, closeParen })
    {
    }

    public GreenToken OpenParenToken => (GreenToken)Children[0]!;
    public GreenNode Expression => Children[1]!;
    public GreenToken CloseParenToken => (GreenToken)Children[2]!;
}

internal sealed class GreenCallExpressionNode : GreenInternalNode
{
    public GreenCallExpressionNode(GreenToken identifier, GreenToken openParen, GreenNode arguments, GreenToken closeParen)
        : base(NodeKind.CallExpression, new GreenNode?[] { identifier, openParen, arguments, closeParen })
    {
    }

    public GreenToken Identifier => (GreenToken)Children[0]!;
    public GreenToken OpenParenToken => (GreenToken)Children[1]!;
    public GreenNode Arguments => Children[2]!;
    public GreenToken CloseParenToken => (GreenToken)Children[3]!;
}

internal sealed class GreenIdentifierExpressionNode : GreenInternalNode
{
    public GreenIdentifierExpressionNode(GreenToken identifier)
        : base(NodeKind.IdentifierExpression, new GreenNode?[] { identifier })
    {
    }

    public GreenToken Identifier => (GreenToken)Children[0]!;
}

internal sealed class GreenNumericLiteralExpressionNode : GreenInternalNode
{
    public GreenNumericLiteralExpressionNode(GreenToken numberToken)
        : base(NodeKind.NumericLiteralExpression, new GreenNode?[] { numberToken })
    {
    }

    public GreenToken NumberToken => (GreenToken)Children[0]!;
}

internal sealed class GreenStringLiteralExpressionNode : GreenInternalNode
{
    public GreenStringLiteralExpressionNode(GreenToken stringToken)
        : base(NodeKind.StringLiteralExpression, new GreenNode?[] { stringToken })
    {
    }

    public GreenToken StringToken => (GreenToken)Children[0]!;
}

internal sealed class GreenMissingExpressionNode : GreenInternalNode
{
    public GreenMissingExpressionNode(GreenToken missingToken)
        : base(NodeKind.MissingExpression, new GreenNode?[] { missingToken })
    {
    }

    public GreenToken MissingToken => (GreenToken)Children[0]!;
}
