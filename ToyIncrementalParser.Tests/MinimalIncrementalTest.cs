using ToyIncrementalParser.Syntax;
using ToyIncrementalParser.Text;
using Xunit;

namespace ToyIncrementalParser.Tests;

public sealed class MinimalIncrementalTest
{
    [Fact]
    public void Minimal_InsertStatement()
    {
        // Simplest case: insert a statement between two existing statements
        const string commonPrefix = "print 1;\n";
        const string deletedText = "";
        const string insertedText = "print 2;\n";
        const string commonSuffix = "print 3;\n";

        var (originalTree, incrementalTree) = IncrementalParsingTests.TestIncrementalChange(
            commonPrefix, deletedText, insertedText, commonSuffix);

        // First, just check that the trees are equivalent (structure matches)
        var reparsedTree = SyntaxTree.Parse(commonPrefix + insertedText + commonSuffix);
        Assert.Equal(reparsedTree.Root.Statements.Statements.Count, incrementalTree.Root.Statements.Statements.Count);
        
        // Check that nodes are not reused (both statements are at change boundaries and must be crumbled)
        // The first statement ends at the change boundary, so it's crumbled
        // The second statement starts at the change boundary and must be crumbled to check for trailing trivia
        var originalStatements = originalTree.Root.Statements.Statements;
        var incrementalStatements = incrementalTree.Root.Statements.Statements;

        Assert.Equal(3, incrementalStatements.Count);
        // Both statements are at change boundaries and are crumbled, so they won't be reused
        Assert.NotSame(((SyntaxNode)originalStatements[0]).Green, ((SyntaxNode)incrementalStatements[0]).Green);
        Assert.NotSame(((SyntaxNode)originalStatements[1]).Green, ((SyntaxNode)incrementalStatements[2]).Green);
    }
}

