using System;
using ToyIncrementalParser.Syntax.Green;

namespace ToyIncrementalParser.Syntax;

internal static class SyntaxNodeFactory
{
    public static SyntaxNode Create(SyntaxTree syntaxTree, SyntaxNode? parent, GreenNode green, int position)
    {
        if (green is GreenToken token)
            return SyntaxToken.Create(syntaxTree, parent, token, position);

        return green.Kind switch
        {
            NodeKind.Program => new ProgramSyntax(syntaxTree, parent, (GreenProgramNode)green, position),
            NodeKind.StatementList => new StatementListSyntax(syntaxTree, parent, (GreenStatementListNode)green, position),
            NodeKind.PrintStatement => new PrintStatementSyntax(syntaxTree, parent, (GreenPrintStatementNode)green, position),
            NodeKind.ReturnStatement => new ReturnStatementSyntax(syntaxTree, parent, (GreenReturnStatementNode)green, position),
            NodeKind.AssignmentStatement => new AssignmentStatementSyntax(syntaxTree, parent, (GreenAssignmentStatementNode)green, position),
            NodeKind.ExpressionBody => new ExpressionBodySyntax(syntaxTree, parent, (GreenExpressionBodyNode)green, position),
            NodeKind.StatementBody => new StatementBodySyntax(syntaxTree, parent, (GreenStatementBodyNode)green, position),
            NodeKind.FunctionDefinition => new FunctionDefinitionSyntax(syntaxTree, parent, (GreenFunctionDefinitionNode)green, position),
            NodeKind.ConditionalStatement => new ConditionalStatementSyntax(syntaxTree, parent, (GreenConditionalStatementNode)green, position),
            NodeKind.LoopStatement => new LoopStatementSyntax(syntaxTree, parent, (GreenLoopStatementNode)green, position),
            NodeKind.ErrorStatement => new ErrorStatementSyntax(syntaxTree, parent, (GreenErrorStatementNode)green, position),
            NodeKind.ExpressionList => new ExpressionListSyntax(syntaxTree, parent, (GreenExpressionListNode)green, position),
            NodeKind.IdentifierList => new IdentifierListSyntax(syntaxTree, parent, (GreenIdentifierListNode)green, position),
            NodeKind.BinaryExpression => new BinaryExpressionSyntax(syntaxTree, parent, (GreenBinaryExpressionNode)green, position),
            NodeKind.UnaryExpression => new UnaryExpressionSyntax(syntaxTree, parent, (GreenUnaryExpressionNode)green, position),
            NodeKind.ParenthesizedExpression => new ParenthesizedExpressionSyntax(syntaxTree, parent, (GreenParenthesizedExpressionNode)green, position),
            NodeKind.CallExpression => new CallExpressionSyntax(syntaxTree, parent, (GreenCallExpressionNode)green, position),
            NodeKind.IdentifierExpression => new IdentifierExpressionSyntax(syntaxTree, parent, (GreenIdentifierExpressionNode)green, position),
            NodeKind.NumericLiteralExpression => new NumericLiteralExpressionSyntax(syntaxTree, parent, (GreenNumericLiteralExpressionNode)green, position),
            NodeKind.StringLiteralExpression => new StringLiteralExpressionSyntax(syntaxTree, parent, (GreenStringLiteralExpressionNode)green, position),
            NodeKind.MissingExpression => new MissingExpressionSyntax(syntaxTree, parent, (GreenMissingExpressionNode)green, position),
            _ => throw new InvalidOperationException($"Unhandled green node kind '{green.Kind}'.")
        };
    }
}
