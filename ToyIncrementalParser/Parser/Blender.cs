using System.Diagnostics;
using ToyIncrementalParser.Syntax;
using ToyIncrementalParser.Syntax.Green;
using ToyIncrementalParser.Text;
using static ToyIncrementalParser.Text.SpecialCharacters;

namespace ToyIncrementalParser.Parser;

internal sealed class Blender : ISymbolStream, ICharacterSource
{
    private readonly IText _newText;
    private readonly Stack<SymbolEntry> _symbolStack = new();
    private readonly BlenderCharacterSource _characterSource;

    // _currentPosition is the position in the new text for the lexer, as represented by the symbol stack.
    // It ignores _pendingToken.
    private int _currentPosition;
    private Lexer? _lexer;
    private SymbolToken? _pendingToken;

    public Blender(GreenProgramNode oldRoot, IText newText, TextChange change)
    {
        _newText = newText;
        _currentPosition = 0;
        _characterSource = new BlenderCharacterSource(this);
        BuildInitialStack(oldRoot, newText, change);
        
        // After building the initial stack, verify synchronization
        if (_symbolStack.Count > 0)
        {
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

        while (true)
        {
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

            // If the non-terminal contains diagnostics, crumble it and try again
            if (top.Node.ContainsDiagnostics)
            {
                CrumbleTopSymbol();
                continue;
            }

            // Found a synchronized non-terminal without diagnostics
            kind = top.Node.Kind;
            node = top.Node;
            return true;
        }
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

    // Core character access methods used by BlenderCharacterSource
    internal char PeekCharacterCore()
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

    internal char ConsumeCharacterCore()
    {
        var ch = PeekCharacterCore();
        _currentPosition++;
        return ch;
    }

    internal int CurrentPositionCore => _currentPosition;

    // ICharacterSource implementation delegates to the wrapper
    char ICharacterSource.PeekCharacter() => _characterSource.PeekCharacter();
    char ICharacterSource.ConsumeCharacter() => _characterSource.ConsumeCharacter();
    void ICharacterSource.PushBack(char ch) => _characterSource.PushBack(ch);
    int ICharacterSource.CurrentPosition => _characterSource.CurrentPosition;

    private void BuildInitialStack(GreenProgramNode oldRoot, IText newText, TextChange change)
    {
        // We build the initial work stack by processing the old program from left to right using a work stack.
        // We push nodes onto the left stack until we hit an overlap with the change (leaving the left stack in reverse order).
        // Then we remove nodes that are entirely inside the change from the work stack and discard them.
        // Finally, we produce the work stack by combining the left stack, the text segment, and the remaining work stack.

        var (deletedStart, deletedLength) = change.Span.GetOffsetAndLength(int.MaxValue);
        var deletedEnd = deletedStart + deletedLength;
        var delta = change.NewLength - deletedLength; // the amount the text has increased by

        // These are the bounds of the text segment in newText coordinates.
        // We discard tokens that overlap the change and extend the change span to include them.
        var newSpanStart = deletedStart;
        var newSpanEnd = deletedEnd + delta;

        var leftStack = new Stack<SymbolEntry>();
        var leftStackEndPosition = 0;

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

        var workStack = new Stack<(GreenNode node, int oldPosition)>();
        workStack.Push((oldRoot, 0));

        while (workStack.TryPop(out var item))
        {
            var (node, oldPosition) = item;

            if (node.FullWidth == 0)
                continue;

            var oldEnd = oldPosition + node.FullWidth;
            
            if ((oldEnd+2) <= deletedStart)
            {
                // Preserve text to the left of the change on the left stack
                Debug.Assert(leftStackEndPosition == oldPosition);
                PushToLeftStack(new SymbolEntry(node, oldPosition));
                continue;
            }

            if (oldPosition > deletedEnd)
            {
                // Preserve text to the right of the change
                workStack.Push((node, oldPosition));
                break;
            }

            // There is an overlap.
            if (node.IsToken)
            {
                // Overlapping tokens will be rescanned, so we discard them.

                // Check if it overlaps the start of the span. If it does, extend the span.
                if (oldPosition < newSpanStart)
                {
                    newSpanStart = oldPosition;
                }

                // Check if it overlaps the end of the span. If it does, extend the span.
                // We need to extend if the token extends to or past deletedEnd
                if (oldEnd >= deletedEnd)
                {
                    var newEnd = oldEnd + delta;
                    if (newEnd > newSpanEnd)
                    {
                        newSpanEnd = newEnd;
                    }
                }
            }
            else
            {
                // Crumble nonterminals that overlap (they will be rescanned).
                CrumbleNode(node, oldPosition, workStack);
            }
        }

        // At this point
        // 1. the left stack should end at newSpanStart,
        Debug.Assert(leftStackEndPosition == newSpanStart);
        // 2. the segment that needs to be scanned ranges from newSpanStart to newSpanEnd, and
        Debug.Assert(newSpanStart <= newSpanEnd);
        // 3. the work stack's head (adjusted by delta) should be at position newSpanEnd.
        var workStackStart = workStack.TryPeek(out var top) ? top.oldPosition+delta : newText.Length;
        Debug.Assert(workStackStart == newSpanEnd);

        // Now push the whole program, from tail to head, onto _symbolStack:
        // 1. the work queue
        // 2. a text segment from newSpanStart to newSpanEnd
        // 3. the left stack (in reverse order)

        // Push remaining work queue items onto result (they're all after the change)
        // Work stack has leftmost at top, so we need to reverse to get right order on result
        var tempStack = new Stack<SymbolEntry>();
        // Compute the start position of the first node in the work stack
        // The work stack has nodes in order (leftmost at top), and we need to compute their positions in new coordinates
        // We'll process them from right to left (pop from work stack) and compute positions working backwards
        var newPosition = newSpanEnd;
        
        // Pop the work stack into a temporary stack so we can push it to _symbolStack in order
        while (workStack.TryPop(out var item))
        {
            var (node, oldPosition) = item;
            if (node.FullWidth == 0)
                continue;
            // Because these are after the change, their position differs by delta
            Debug.Assert(oldPosition + delta == newPosition);
            tempStack.Push(new SymbolEntry(node, newPosition));
            newPosition += node.FullWidth; // work queue pops left to right.
        }
        Debug.Assert(newPosition == newText.Length);
        while (tempStack.TryPop(out var entry))
        {
            _symbolStack.Push(entry);
            newPosition -= entry.Node.FullWidth; // we push onto the result queue right to left
        }

        // Push the text segment
        Debug.Assert(newPosition == newSpanEnd);
        newPosition = newSpanStart;
        _symbolStack.Push(new SymbolEntry(newSpanStart, newSpanEnd));

        // Finally, push the left stack onto _symbolStack
        while (leftStack.TryPop(out var entry))
        {
            _symbolStack.Push(entry);
            newPosition -= entry.Node.FullWidth;
        }

        // We should have pushed the whole program onto _symbolStack
        Debug.Assert(newPosition == 0);
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

        while (true)
        {
            ProcessStackUntilToken();

            // After ProcessStackUntilToken, the top should be either text or a synchronized token
            if (_symbolStack.Count == 0)
            {
                // Stack is empty - lex the next token (which will be EOF if we're at the end)
                var lexed = LexNextToken();
                _pendingToken = lexed;
                return;
            }

            var top = _symbolStack.Peek();
            
            // If we have a synchronized token on the stack, convert it to pending token
            if (top.Node is GreenToken greenToken)
            {
                // Tokens with diagnostics should be crumbled to text segments for rescanning
                if (greenToken.ContainsDiagnostics)
                {
                    CrumbleTopSymbol();
                    continue;
                }
                
                // _currentPosition should always be synchronized with the top of the stack
                AssertPositionSynchronized("EnsureToken (token found)");
                _pendingToken = new SymbolToken(greenToken, top.NewStart, top.NewStart + greenToken.LeadingWidth);
                // Remove the token from the stack since it's now pending
                _symbolStack.Pop();
                // Advance position past the token (to keep _currentPosition reflecting stack position)
                var tokenEnd = top.NewStart + greenToken.FullWidth;
                _currentPosition = tokenEnd;
                
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
                    var lexed = LexNextToken();
                    _pendingToken = lexed;
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

            if (top.Node is GreenToken greenToken)
            {
                // Tokens with diagnostics should be crumbled to text segments for rescanning
                if (greenToken.ContainsDiagnostics)
                {
                    CrumbleTopSymbol();
                    continue;
                }
                
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
        
        // Tokens with diagnostics should be converted to text segments for rescanning
        if (node is GreenToken token)
        {
            if (token.ContainsDiagnostics)
            {
                // Convert token to text segment for rescanning
                var tokenEnd = newPosition + token.FullWidth;
                _symbolStack.Push(new SymbolEntry(newPosition, tokenEnd));
                _currentPosition = newPosition;
                return;
            }
            else
            {
                throw new InvalidOperationException(
                    $"Cannot crumble token {node.Kind} at {newPosition} - tokens have no children and no diagnostics.");
            }
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

    /// <summary>
    /// Wrapper around Blender's character access that handles pushback.
    /// This isolates the pushback logic from the complex position tracking in the Blender.
    /// </summary>
    private sealed class BlenderCharacterSource : ICharacterSource
    {
        private readonly Blender _blender;
        private char? _pushBack;

        public BlenderCharacterSource(Blender blender)
        {
            _blender = blender;
        }

        public char PeekCharacter()
        {
            if (_pushBack.HasValue)
                return _pushBack.Value;
            
            return _blender.PeekCharacterCore();
        }

        public char ConsumeCharacter()
        {
            if (_pushBack.HasValue)
            {
                var pushedBack = _pushBack.Value;
                _pushBack = null;
                return pushedBack;
            }
            
            return _blender.ConsumeCharacterCore();
        }

        public void PushBack(char ch)
        {
            if (_pushBack.HasValue)
                throw new InvalidOperationException("Cannot push back more than one character.");
            _pushBack = ch;
        }

        public int CurrentPosition => _pushBack.HasValue ? _blender.CurrentPositionCore - 1 : _blender.CurrentPositionCore;
    }
}



