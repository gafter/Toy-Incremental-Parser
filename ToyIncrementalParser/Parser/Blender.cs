using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using ToyIncrementalParser.Syntax;
using ToyIncrementalParser.Syntax.Green;
using ToyIncrementalParser.Text;
using static ToyIncrementalParser.Text.SpecialCharacters;

namespace ToyIncrementalParser.Parser;

internal sealed class Blender : ISymbolStream, ICharacterSource
{
    private readonly IText _newText;
    private readonly Stack<SymbolEntry> _symbolStack = new();

    // _currentPosition is the position in the new text for the lexer, as represented by the symbol stack.
    // It ignores _pendingToken.
    private int _currentPosition;
    private Lexer? _lexer;
    private SymbolToken? _pendingToken;

    public Blender(GreenProgramNode oldRoot, IText newText, TextChange change)
    {
        _newText = newText;
        _currentPosition = 0;
        BuildInitialStack(oldRoot, newText, change);
        
        // After building the initial stack, verify synchronization
        if (_symbolStack.Count > 0)
        {
            var top = _symbolStack.Peek();
            System.Console.WriteLine($"[Blender constructor] After BuildInitialStack: stackCount={_symbolStack.Count}, _currentPosition={_currentPosition}, top={(top.IsText ? $"Text {top.TextStart}..{top.TextEnd}" : $"{top.Node.Kind} at {top.NewStart}")}");
        }
        AssertPositionSynchronized("Blender constructor (after BuildInitialStack)");
    }

    public SymbolToken PeekToken()
    {
        if (_pendingToken == null)
        {
            EnsureToken();
        }
        
        var token = _pendingToken!.Value;
        System.Console.WriteLine($"[PeekToken] {token.Token.Kind} at {token.FullStart}..{token.FullEnd}, position={_currentPosition}");
        return token;
    }

    public SymbolToken ConsumeToken()
    {
        if (_pendingToken == null)
        {
            EnsureToken();
        }
        
        var token = _pendingToken!.Value;
        _pendingToken = null;
        
        System.Console.WriteLine($"[ConsumeToken] {token.Token.Kind} at {token.FullStart}..{token.FullEnd}, position={_currentPosition} (after consume)");
        
        // After consuming token, verify synchronization with next symbol (if any)
        AssertPositionSynchronized("ConsumeToken (after consuming)");
        
        return token;
    }

    public void PushBackToken(SymbolToken token)
    {
        // If there's already a pending token, we can't push back
        if (_pendingToken != null)
        {
            throw new InvalidOperationException("Cannot push back token: there is already a pending token.");
        }

        if (_currentPosition != token.FullEnd)
        {
            throw new InvalidOperationException(
                $"Cannot push back token: current position {_currentPosition} is not at token end {token.FullEnd}.");
        }
        
        // Just restore the token as the pending token - don't modify stack or position
        _pendingToken = token;
    }

    public bool TryPeekNonTerminal(out NodeKind kind, out GreenNode node)
    {
        kind = default;
        node = null!;

        // If there's a pending token, we can't peek a non-terminal
        if (_pendingToken != null || _symbolStack.Count == 0)
        {
            return false;
        }

        var top = _symbolStack.Peek();

        if (top.IsText || top.Node is GreenToken)
        {
            return false;
        }

        // The head of the stack should always be synchronized with _currentPosition
        // If it isn't, that's a bug
        AssertPositionSynchronized("TryPeekNonTerminal");

        // Found a synchronized non-terminal
        kind = top.Node.Kind;
        node = top.Node;
        return true;
    }

    public bool TryTakeNonTerminal(NodeKind kind, out GreenNode node)
    {
        node = null!;

        while (true)
        {
            if (_pendingToken != null || _symbolStack.Count == 0)
            {
                return false;
            }

            var top = _symbolStack.Peek();

            // Assert that _currentPosition is synchronized with the top of the stack
            AssertPositionSynchronized($"TryTakeNonTerminal({kind})");
            
            if (top.IsText)
            {
                return false;
            }

            if (top.Node.Kind == kind && !top.Node.ContainsDiagnostics)
            {
                // _currentPosition should always be synchronized with the top of the stack
                AssertPositionSynchronized("TryTakeNonTerminal (after crumbling)");
                
                var popped = PopSymbolAndAdvance();
                node = popped.Node;
                return true;
            }

            // If the top is a token, we can't crumble it and it doesn't match - return false
            if (top.Node is GreenToken)
            {
                return false;
            }

            AssertPositionSynchronized("TryTakeNonTerminal (before crumbling)");
            CrumbleTopSymbol();
        }
    }

    char ICharacterSource.PeekCharacter()
    {
        EnsureCharacterAvailable();
        if (_currentPosition >= _newText.Length)
            return EndOfFile;
        
        if (_symbolStack.Count == 0)
            return EndOfFile;

        var top = _symbolStack.Peek();
        if (!top.IsText)
            throw new InvalidOperationException(
                $"Current position {_currentPosition} is not at the start of a text segment.");
        return _newText[_currentPosition];
    }

    char ICharacterSource.ConsumeCharacter()
    {
        var ch = ((ICharacterSource)this).PeekCharacter();
        _currentPosition++;
        return ch;
    }

    void ICharacterSource.PushBack(char ch)
    {
        if (_currentPosition <= 0)
            throw new InvalidOperationException("Cannot push back at the beginning of the text.");

        var previousChar = _newText[_currentPosition - 1];
        if (previousChar != ch)
            throw new InvalidOperationException($"Cannot push back character '{ch}'; expected '{previousChar}' at position {_currentPosition - 1}.");

        _currentPosition--;
        
        // After pushing back, if there's a text segment on the stack, ensure we're still within its bounds
        if (_symbolStack.Count > 0)
        {
            var top = _symbolStack.Peek();
            if (top.IsText)
            {
                if (_currentPosition < top.TextStart || _currentPosition >= top.TextEnd)
                {
                    throw new InvalidOperationException(
                        $"After pushing back, current position {_currentPosition} is outside text segment {top.TextStart}..{top.TextEnd}.");
                }
            }
        }
    }

    int ICharacterSource.CurrentPosition => _currentPosition;

    private void BuildInitialStack(GreenProgramNode oldRoot, IText newText, TextChange change)
    {
        // Extract change boundaries
        var (originalChangeStart, oldChangeLength) = change.Span.GetOffsetAndLength(int.MaxValue);
        var originalNewChangeLength = change.NewLength;
        var changeStart = originalChangeStart;
        var changeEnd = changeStart + oldChangeLength;

        // Work queue and left stack
        var workStack = new Stack<(GreenNode node, int oldPosition)>();
        var leftStack = new Stack<SymbolEntry>();
        var leftStackEndPosition = 0; // Track the end position of the left stack in NEW text coordinates
        var discardedSize = 0; // Track the total size of symbols discarded (entirely inside the change)
        var crumbledBoundaryEnd = 0; // Track the end position of crumbled tokens at the boundary (in NEW text coordinates)
        
        // Helper function to push onto left stack and update position
        void PushToLeftStack(SymbolEntry entry)
        {
            // Entry can never be text when pushing to left stack (only nodes/tokens that are entirely before the change)
            if (entry.IsText)
                throw new InvalidOperationException("PushToLeftStack: Entry cannot be a text segment.");
            
            // Assert that the entry starts exactly where the left stack ends (ensuring continuity)
            if (entry.NewStart != leftStackEndPosition)
                throw new InvalidOperationException(
                    $"PushToLeftStack: Entry start {entry.NewStart} does not match leftStackEndPosition {leftStackEndPosition}.");
            
            leftStack.Push(entry);
            leftStackEndPosition += entry.Node.FullWidth;
        }
        
        workStack.Push((oldRoot, 0));

        // First loop: process left-to-right until we hit overlap or everything is after
        while (workStack.Count > 0)
        {
            var (node, oldPosition) = workStack.Peek();
            var oldEnd = oldPosition + node.FullWidth;

            // (1) Check if entirely before change (with 2-char lookahead buffer)
            if ((oldEnd + 2) <= changeStart)
            {
                workStack.Pop();
                // Symbols entirely before the change keep their positions (change is after them)
                var entry = new SymbolEntry(node, oldPosition);
                PushToLeftStack(entry);
                System.Console.WriteLine($"[BuildInitialStack] Added to leftStack: {node.Kind} at {oldPosition}..{oldEnd} (entirely before change at {changeStart}, condition: {oldEnd}+2={oldEnd+2} <= {changeStart}), leftStackEndPosition={leftStackEndPosition}");
                continue;
            }

            // (2) Check if entirely after change - exit loop.
            // Note: Tokens starting exactly at changeEnd should be crumbled (not added to right stack)
            // because they might affect scanning of the previous token (e.g., trailing trivia)
            if (oldPosition > changeEnd)
            {
                break;
            }

            // (3) Overlap case: symbolPosition < changeEnd && symbolEnd+2 > changePosition
            // OR symbol starts exactly at changeEnd (needs to be crumbled for scanning)
            workStack.Pop();

            if (node is GreenToken)
            {
                // Token overlapping or starting at boundary - include it in the text segment (it will be rescanned)
                discardedSize += node.FullWidth;
                // Track the end position of tokens crumbled at the boundary (in NEW text coordinates)
                if (oldPosition >= changeEnd)
                {
                    var newPosition = oldPosition - oldChangeLength + originalNewChangeLength;
                    var newEnd = newPosition + node.FullWidth;
                    crumbledBoundaryEnd = Math.Max(crumbledBoundaryEnd, newEnd);
                }
            }
            else
            {
                // Non-terminal overlapping - check if entirely inside
                bool entirelyInside = oldPosition >= changeStart && oldEnd <= changeEnd;
                if (entirelyInside)
                {
                    // Discard non-terminals entirely inside the change
                    discardedSize += node.FullWidth;
                }
                else
                {
                    // Crumble non-terminals that overlap
                    CrumbleNode(node, oldPosition, workStack);
                }
            }
        }

        // Push remaining work queue items onto result (they're all after the change)
        // Work stack has leftmost at top, so we need to reverse to get right order on result
        var tempStack = new Stack<SymbolEntry>();
        var rightStackStartPosition = int.MaxValue; // Track where the right stack starts in NEW text coordinates
        while (workStack.Count > 0)
        {
            var (node, oldPosition) = workStack.Pop();
            // Adjust position using original change boundaries (not extended)
            // For insertions, oldChangeLength = 0, so positions shift by originalNewChangeLength
            // For deletions/replacements, positions shift by (originalNewChangeLength - oldChangeLength)
            var adjustedPosition = oldPosition - oldChangeLength + originalNewChangeLength;
            var entry = new SymbolEntry(node, adjustedPosition);
            tempStack.Push(entry);
            // Track the leftmost position in the right stack
            // Entry is always a node (never text) when created from workStack
            rightStackStartPosition = Math.Min(rightStackStartPosition, entry.NewStart);
        }
        // Reverse tempStack onto result (so leftmost is on top of result)
        while (tempStack.Count > 0)
        {
            _symbolStack.Push(tempStack.Pop());
        }

        // Push text segment for the change region
        // The segment starts at the end of the left stack and extends to the start of the right stack
        // This covers:
        // - Any gap from leftStackEndPosition to originalChangeStart
        // - The inserted text (which replaces the deleted text and any discarded symbols)
        // - Any gap from the end of the inserted text to the start of the right stack
        // If there's no right stack, extend to at least the end of the inserted text
        var textSegmentStart = leftStackEndPosition;
        var insertedTextStart = originalChangeStart;
        var insertedTextEnd = insertedTextStart + originalNewChangeLength;
        // Extend text segment to include crumbled tokens at the boundary
        var minTextSegmentEnd = Math.Max(insertedTextEnd, crumbledBoundaryEnd);
        var textSegmentEnd = rightStackStartPosition < int.MaxValue ? rightStackStartPosition : minTextSegmentEnd;
        var textSegmentSize = textSegmentEnd - textSegmentStart;
        if (textSegmentSize > 0)
        {
            // Assert that the text segment covers the original inserted text span
            // In NEW text coordinates, the inserted text is at originalChangeStart with length originalNewChangeLength
            if (textSegmentStart > insertedTextStart || textSegmentEnd < insertedTextEnd)
            {
                throw new InvalidOperationException(
                    $"Text segment {textSegmentStart}..{textSegmentEnd} does not cover inserted text span {insertedTextStart}..{insertedTextEnd}. " +
                    $"leftStackEndPosition={leftStackEndPosition}, rightStackStartPosition={rightStackStartPosition}");
            }
            
            System.Console.WriteLine($"[BuildInitialStack] Pushing text segment: {textSegmentStart}..{textSegmentEnd} (leftStackEnd={leftStackEndPosition}, rightStackStart={rightStackStartPosition}, inserted={originalNewChangeLength})");
            _symbolStack.Push(new SymbolEntry(textSegmentStart, textSegmentEnd));
        }

        // Push left stack onto result (leftmost should be on top, so pop and push)
        while (leftStack.Count > 0)
        {
            var entry = leftStack.Pop();
            System.Console.WriteLine($"[BuildInitialStack] Pushing from leftStack: {entry.Node?.Kind} at {entry.NewStart}..{(entry.IsText ? entry.TextEnd : entry.NewStart + entry.Node!.FullWidth)}");
            _symbolStack.Push(entry);
        }
    }

    private void EnsureCharacterAvailable()
    {
        if (_currentPosition >= _newText.Length)
            return;

        while (_symbolStack.Count > 0)
        {
            var top = _symbolStack.Peek();
            if (top.IsText)
            {
                if (_currentPosition >= top.TextEnd)
                {
                    _symbolStack.Pop();
                    continue;
                }

                // We are inside a text segment, we're done
                return;
            }

            // If we have a token and we're at or within its span, convert it to a text segment
            // This allows the lexer to consume characters from inside the token
            if (top.Node is GreenToken token)
            {
                var tokenStart = top.NewStart;
                var tokenEnd = tokenStart + token.FullWidth;
                
                if (_currentPosition >= tokenStart && _currentPosition < tokenEnd)
                {
                    // Convert the token to a text segment
                    _symbolStack.Pop();
                    _symbolStack.Push(new SymbolEntry(tokenStart, tokenEnd));
                    return;
                }
                
                // If we're past the token, remove it
                if (_currentPosition >= tokenEnd)
                {
                    _symbolStack.Pop();
                    continue;
                }
                
                // If we're before the token, this shouldn't happen (position should be synchronized)
                throw new InvalidOperationException(
                    $"Current position {_currentPosition} is before token start {tokenStart}.");
            }

            // For non-terminals, crumble them
            CrumbleTopSymbol();
        }

        // Stack is empty - we're at EOF
        return;
    }

    private void EnsureToken()
    {
        // If we already have a pending token, we're done
        if (_pendingToken != null)
        {
            return;
        }

        System.Console.WriteLine($"[EnsureToken] Starting, position={_currentPosition}, stackCount={_symbolStack.Count}");

        while (true)
        {
            ProcessStackUntilToken();

            // After ProcessStackUntilToken, the top should be either text or a synchronized token
            if (_symbolStack.Count == 0)
            {
                // Stack is empty - lex the next token (which will be EOF if we're at the end)
                System.Console.WriteLine($"[EnsureToken] Stack empty, lexing from position {_currentPosition}");
                var lexed = LexNextToken();
                _pendingToken = lexed;
                System.Console.WriteLine($"[EnsureToken] Lexed {lexed.Token.Kind} at {lexed.FullStart}..{lexed.FullEnd}, position now={_currentPosition}");
                return;
            }

            var top = _symbolStack.Peek();
            
            // If we have a synchronized token on the stack, convert it to pending token
            if (top.Node is GreenToken greenToken)
            {
                // _currentPosition should always be synchronized with the top of the stack
                AssertPositionSynchronized("EnsureToken (token found)");
                _pendingToken = new SymbolToken(greenToken, top.NewStart, top.NewStart + greenToken.LeadingWidth);
                System.Console.WriteLine($"[EnsureToken] Found token on stack: {greenToken.Kind} at {top.NewStart}..{top.NewStart + greenToken.FullWidth}, position={_currentPosition}");
                // Remove the token from the stack since it's now pending
                _symbolStack.Pop();
                // Advance position past the token (to keep _currentPosition reflecting stack position)
                var tokenEnd = top.NewStart + greenToken.FullWidth;
                _currentPosition = tokenEnd;
                
                // Diagnose if there's a gap to the next symbol
                if (_symbolStack.Count > 0)
                {
                    var nextTop = _symbolStack.Peek();
                    var nextStart = nextTop.IsText ? nextTop.TextStart : nextTop.NewStart;
                    if (nextStart > tokenEnd)
                    {
                        var gap = nextStart - tokenEnd;
                        System.Console.WriteLine($"[EnsureToken] WARNING: Gap of {gap} characters between token end {tokenEnd} and next symbol start {nextStart} ({(nextTop.IsText ? "text segment" : nextTop.Node.Kind.ToString())})");
                    }
                }
                
                System.Console.WriteLine($"[EnsureToken] Advanced position to {_currentPosition}");
                
                // After advancing position, verify synchronization with next symbol (if any)
                AssertPositionSynchronized("EnsureToken (after advancing position)");
                
                return;
            }
            
            // If we have a text segment, lex from it
            if (top.IsText)
            {
                // Check if we've already consumed all of this text segment
                if (_currentPosition == top.TextEnd)
                {
                    // Text segment is exhausted, remove it
                    _symbolStack.Pop();
                    continue;
                }

                if (_currentPosition < top.TextStart || _currentPosition >= top.TextEnd)
                {
                    throw new InvalidOperationException(
                        $"Current position {_currentPosition} is not within text segment {top.TextStart}..{top.TextEnd}.");
                }

                // Save the text segment bounds before lexing (since lexing might modify the stack)
                var textStart = top.TextStart;
                var textEnd = top.TextEnd;
                
                try
                {
                    // Lex a token from the text segment
                    // The lexer will use PeekCharacter/ConsumeCharacter which call EnsureCharacterAvailable,
                    // which ensures we have text available from the stack
                    System.Console.WriteLine($"[EnsureToken] Lexing from text segment {textStart}..{textEnd}, position={_currentPosition}");
                    var lexed = LexNextToken();
                    _pendingToken = lexed;
                    System.Console.WriteLine($"[EnsureToken] Lexed {lexed.Token.Kind} at {lexed.FullStart}..{lexed.FullEnd}, position now={_currentPosition}");
                    return;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to lex token from text segment {textStart}..{textEnd} at position {_currentPosition}: {ex.Message}", ex);
                }
            }

            // If we get here, top is a non-terminal - crumble it
            CrumbleTopSymbol();
        }
    }

    private void ProcessStackUntilToken()
    {
        while (_symbolStack.Count > 0)
        {
            var top = _symbolStack.Peek();
            
            // Check if _currentPosition is synchronized with the top of the stack
            var expectedPosition = top.IsText ? top.TextStart : top.NewStart;
            if (_currentPosition < expectedPosition)
            {
                // We're behind - this indicates a bug in position tracking
                // _currentPosition should never be behind the top of the stack
                var symbolDesc = top.IsText ? $"text segment {top.TextStart}..{top.TextEnd}" : $"{top.Node.Kind} at {top.NewStart}";
                throw new InvalidOperationException(
                    $"Position behind expected at start of ProcessStackUntilToken: current {_currentPosition}, expected {expectedPosition} for {symbolDesc}. " +
                    $"This indicates a bug in position tracking - _currentPosition should never be behind the top of the stack.");
            }
            if (_currentPosition > expectedPosition)
            {
                // We're ahead - the top symbol is behind us, so we should crumble it
                // This can happen if we've consumed characters but the stack still has old symbols
                if (!top.IsText)
                {
                    CrumbleTopSymbol();
                    continue;
                }
                else
                {
                    // For text segments, check if we're past the end
                    if (_currentPosition >= top.TextEnd)
                    {
                        // We've consumed past the text segment - remove it
                        _symbolStack.Pop();
                        continue;
                    }
                    // Otherwise, we're still within the text segment, so we're done
                    break;
                }
            }
            if (top.IsText)
            {
                // When we stop at a text segment, _currentPosition should be synchronized with it
                // If _currentPosition is before the text segment, so this indicates a bug
                if (_currentPosition < top.TextStart)
                {
                    throw new InvalidOperationException(
                        $"Current position {_currentPosition} is before text segment start {top.TextStart}. " +
                        $"This indicates _currentPosition is not synchronized with the stack.");
                }
                break;
            }

            if (top.Node is GreenToken)
            {
                // _currentPosition should always be synchronized with the top of the stack
                AssertPositionSynchronized("ProcessStackUntilToken (token found)");
                
                // Token is synchronized, we can use it
                break;
            }

            // Non-terminal: crumble it to get to its children/tokens
            // Even if it's at _currentPosition, we need to crumble it to get a token for PeekToken()
            Debug.Assert(top.Node is GreenNode);
            CrumbleTopSymbol();
        }
    }

    private void EnqueueNode(GreenNode node, int newPosition)
    {
        _symbolStack.Push(new SymbolEntry(node, newPosition));
    }


    private void CrumbleNode(GreenNode node, int oldPosition, Stack<(GreenNode node, int oldPosition)> stack)
    {
        // If the node has an empty span, discard it (e.g. missing tokens)
        if (node.FullWidth == 0)
        {
            return;
        }

        // Calculate the total width of all children to find the position of the last child
        var totalWidth = 0;
        for (var i = 0; i < node.SlotCount; i++)
        {
            var child = node.GetSlot(i);
            if (child is not null)
            {
                totalWidth += child.FullWidth;
            }
        }

        // Push children in reverse order (right to left) so they pop left to right
        // Start from the last child's position and work backwards
        var currentPosition = oldPosition + totalWidth;
        GreenNode? firstChild = null;
        int firstChildPosition = -1;
        for (var i = node.SlotCount - 1; i >= 0; i--)
        {
            var child = node.GetSlot(i);
            if (child is not null)
            {
                // Discard children with empty spans (e.g. missing tokens)
                if (child.FullWidth == 0)
                {
                    continue;
                }
                
                currentPosition -= child.FullWidth;
                stack.Push((child, currentPosition));
                if (i == 0)
                {
                    firstChild = child;
                    firstChildPosition = currentPosition;
                }
            }
        }
        
        // Assert that the first child (index 0) starts at the same position as the parent
        // This is critical for maintaining the invariant that the top of the stack matches _currentPosition
        // But only if we actually pushed a first child
        if (firstChild is not null && firstChildPosition != oldPosition)
        {
            throw new InvalidOperationException(
                $"After crumbling {node.Kind} at {oldPosition}, first child is at {firstChildPosition}, not {oldPosition}. " +
                $"This indicates incorrect child position calculation in CrumbleNode.");
        }
    }

    private void CrumbleTopSymbol()
    {
        if (_symbolStack.Count == 0)
            return;

        var top = _symbolStack.Peek();
        if (top.IsText)
        {
            // Should not happen if EnsureCharacterAvailable is called correctly
            _symbolStack.Pop();
            return;
        }

        // Assert that _currentPosition matches the symbol we're about to crumble
        if (_currentPosition != top.NewStart)
        {
            throw new InvalidOperationException(
                $"CrumbleTopSymbol: Position not synchronized: current {_currentPosition}, expected {top.NewStart} for {top.Node.Kind}.");
        }

        // Now pop it
        _symbolStack.Pop();

        var node = top.Node;
        var newPosition = top.NewStart;
        
        // If the node has an empty span, discard it (e.g. missing tokens)
        if (node.FullWidth == 0)
        {
            return;
        }
        
        // Tokens can't be crumbled - they have no children
        if (node is GreenToken)
        {
            throw new InvalidOperationException(
                $"Cannot crumble token {node.Kind} at {newPosition} - tokens have no children.");
        }

        // Push children in reverse order to maintain correct order when popped
        // The first child (index 0) will be pushed last, so it will be on top
        // Start at the end position of the parent and work backwards
        var currentPosition = newPosition + node.FullWidth;
        for (var i = node.SlotCount - 1; i >= 0; i--)
        {
            var child = node.GetSlot(i);
            if (child is not null)
            {
                // Discard children with empty spans (e.g. missing tokens)
                if (child.FullWidth == 0)
                {
                    continue;
                }
                
                // Subtract the child's width to get its start position
                currentPosition -= child.FullWidth;
                EnqueueNode(child, currentPosition);
            }
        }
    }


    private SymbolToken LexNextToken()
    {
        // Reuse the lexer if we have one, otherwise create a new one
        _lexer ??= new Lexer(this);
        // The lexer will advance _currentPosition as it consumes characters
        // After lexing, _currentPosition will be at lexed.FullEnd
        var lexed = _lexer.NextToken();
        var symbolToken = new SymbolToken(lexed.Token, lexed.FullStart, lexed.SpanStart);
        return symbolToken;
    }

    /// <summary>
    /// Asserts that _currentPosition is synchronized with the given symbol entry.
    /// For text segments, checks that _currentPosition is contained within the segment bounds.
    /// For nodes/tokens, checks that _currentPosition matches the entry's start position.
    /// </summary>
    private void AssertPositionSynchronized(string? context = null)
    {
        if (_symbolStack.Count == 0)
            return; // Nothing to check if stack is empty
        
        var entry = _symbolStack.Peek();
        if (entry.IsText)
        {
            if (_currentPosition < entry.TextStart || _currentPosition > entry.TextEnd)
            {
                var ctx = context != null ? $"{context}: " : "";
                throw new InvalidOperationException(
                    $"{ctx}Current position {_currentPosition} is not within text segment {entry.TextStart}..{entry.TextEnd}.");
            }
        }
        else
        {
            if (_currentPosition != entry.NewStart)
            {
                var ctx = context != null ? $"{context}: " : "";
                throw new InvalidOperationException(
                    $"{ctx}Position not synchronized: current {_currentPosition}, expected {entry.NewStart} for {entry.Node.Kind}.");
            }
        }
    }

    /// <summary>
    /// Pops a nonterminal from the stack and advances _currentPosition by its width.
    /// Should only be called when the top of the stack is a nonterminal and there is no pending token.
    /// </summary>
    private SymbolEntry PopSymbolAndAdvance()
    {
        if (_symbolStack.Count == 0)
            throw new InvalidOperationException("Cannot pop from empty stack.");
        
        var top = _symbolStack.Pop();
        
        // Should only be called with a nonterminal
        if (top.IsText)
            throw new InvalidOperationException("PopSymbolAndAdvance should not be called with a text segment.");
        if (top.Node is GreenToken)
            throw new InvalidOperationException("PopSymbolAndAdvance should not be called with a token.");
        
        // Advance _currentPosition by the nonterminal's full width
        _currentPosition += top.Node.FullWidth;
        
        // After popping and advancing, verify synchronization with next symbol (if any)
        AssertPositionSynchronized("PopSymbolAndAdvance (after advancing)");
        
        return top;
    }

    private readonly struct SymbolEntry
    {
        public SymbolEntry(GreenNode node, int newStart)
        {
            Node = node;
            NewStart = newStart;
            IsText = false;
            TextStart = 0;
            TextEnd = 0;
        }

        public SymbolEntry(int textStart, int textEnd)
        {
            Node = null!;
            NewStart = 0;
            IsText = true;
            TextStart = textStart;
            TextEnd = textEnd;
        }

        public GreenNode Node { get; }
        public int NewStart { get; }
        public bool IsText { get; }
        public int TextStart { get; }
        public int TextEnd { get; }
    }
}



