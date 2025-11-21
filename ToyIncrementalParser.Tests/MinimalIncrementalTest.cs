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
        
        // Verify we have the expected number of statements
        var incrementalStatements = incrementalTree.Root.Statements.Statements;
        Assert.Equal(3, incrementalStatements.Count);
        
        // Note: Nodes at boundaries may be reused because the lexer can peek at characters
        // in the buffer without crumbling, so we don't assert that they must be different.
    }
}

