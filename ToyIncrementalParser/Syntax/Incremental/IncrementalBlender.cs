using System;
using System.Collections.Generic;
using ToyIncrementalParser.Text;
using ToyIncrementalParser.Syntax.Green;

namespace ToyIncrementalParser.Syntax.Incremental;

internal static class IncrementalBlender
{
    public static GreenProgramNode Blend(GreenProgramNode oldRoot, string oldText, string newText, TextChange change)
    {
        var parser = new Parser(newText);
        var newRoot = parser.ParseProgram();

        var oldStatements = CollectStatements(oldRoot, oldText);
        var newStatements = CollectStatements(newRoot, newText);

        var prefixCount = CountPrefixReusable(oldStatements, newStatements);
        var suffixCount = CountSuffixReusable(oldStatements, newStatements, prefixCount);

        var combined = new List<GreenNode>(newStatements.Count);
        for (var i = 0; i < newStatements.Count; i++)
        {
            if (i < prefixCount)
            {
                combined.Add(oldStatements[i].Node);
                continue;
            }

            if (i >= newStatements.Count - suffixCount)
            {
                var suffixIndex = i - (newStatements.Count - suffixCount);
                var oldIndex = oldStatements.Count - suffixCount + suffixIndex;
                combined.Add(oldStatements[oldIndex].Node);
                continue;
            }

            combined.Add(newStatements[i].Node);
        }

        var mergedStatementList = GreenFactory.StatementList(combined);
        return GreenFactory.Program(mergedStatementList, newRoot.EndOfFileToken);
    }

    private static List<StatementInfo> CollectStatements(GreenProgramNode root, string text)
    {
        var list = new List<StatementInfo>();
        var statements = ((GreenStatementListNode)root.Statements).Statements;
        var position = 0;

        for (var i = 0; i < statements.Count; i++)
        {
            var node = statements[i];
            var fullWidth = node.FullWidth;
            var info = new StatementInfo(node, position, position + fullWidth, text.Substring(position, fullWidth));
            list.Add(info);
            position += fullWidth;
        }

        return list;
    }

    private static int CountPrefixReusable(List<StatementInfo> oldStatements, List<StatementInfo> newStatements)
    {
        var count = 0;
        while (count < oldStatements.Count && count < newStatements.Count)
        {
            if (!string.Equals(oldStatements[count].Text, newStatements[count].Text, StringComparison.Ordinal))
                break;
            count++;
        }

        return count;
    }

    private static int CountSuffixReusable(List<StatementInfo> oldStatements, List<StatementInfo> newStatements, int prefixCount)
    {
        var count = 0;
        var oldIndex = oldStatements.Count - 1;
        var newIndex = newStatements.Count - 1;

        while (oldIndex >= prefixCount && newIndex >= prefixCount)
        {
            if (!string.Equals(oldStatements[oldIndex].Text, newStatements[newIndex].Text, StringComparison.Ordinal))
                break;
            count++;
            oldIndex--;
            newIndex--;
        }

        return count;
    }

    private readonly struct StatementInfo
    {
        public StatementInfo(GreenNode node, int fullStart, int fullEnd, string text)
        {
            Node = node;
            FullStart = fullStart;
            FullEnd = fullEnd;
            Text = text;
        }

        public GreenNode Node { get; }
        public int FullStart { get; }
        public int FullEnd { get; }
        public string Text { get; }
    }
}

