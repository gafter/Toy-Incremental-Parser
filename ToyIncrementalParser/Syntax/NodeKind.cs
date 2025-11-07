namespace ToyIncrementalParser.Syntax;

public enum NodeKind
{
    // Trivia nodes
    NewlineTrivia,
    SpacesTrivia,
    TabsTrivia,
    CommentTrivia,
    MultipleTrivia,

    // Terminal nodes
    EOFToken,
    PrintToken,
    SemicolonToken,
    ReturnToken,
    DefineToken,
    OpenParenToken,
    CloseParenToken,
    EqualsToken,
    BeginToken,
    EndToken,
    IdentifierToken,
    LetToken,
    IfToken,
    ThenToken,
    ElseToken,
    FiToken,
    WhileToken,
    DoToken,
    OdToken,
    PlusToken,
    MinusToken,
    TimesToken,
    SlashToken,
    CommaToken,
    ErrorToken,
    MissingToken,

    // Nonterminals
    Program,
    StatementList,
    PrintStatement,
    ReturnStatement,
    FunctionDefinition,
    ExpressionBody,
    StatementBody,
    AssignmentStatement,
    ConditionalStatement,
    LoopStatement,
    ErrorStatement,
    ExpressionList,
    IdentifierList,
    BinaryExpression,
    UnaryExpression,
    ParenthesizedExpression,
    CallExpression,
    IdentifierExpression,
    MissingExpression,
    MissingIdentifier
}

