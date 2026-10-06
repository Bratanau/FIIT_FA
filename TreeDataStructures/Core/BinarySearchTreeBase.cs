using System.Collections;
using System.Diagnostics.CodeAnalysis;
using TreeDataStructures.Interfaces;

namespace TreeDataStructures.Core;

public abstract class BinarySearchTreeBase<TKey, TValue, TNode>(IComparer<TKey>? comparer = null)
    : ITree<TKey, TValue>
    where TNode : Node<TKey, TValue, TNode>
{
    protected TNode? Root;

    public IComparer<TKey> Comparer { get; protected set; } = comparer ?? Comparer<TKey>.Default;
    public int Count { get; protected set; }
    public bool IsReadOnly => false;

    public ICollection<TKey> Keys => InOrder().Select(entry => entry.Key).ToArray();
    public ICollection<TValue> Values => InOrder().Select(entry => entry.Value).ToArray();

    public virtual void Add(TKey key, TValue value)
    {
        if (Root == null)
        {
            Root = CreateNode(key, value);
            Count = 1;
            OnNodeAdded(Root);
            return;
        }

        TNode? current = Root;
        TNode? parent = null;
        int comparison = 0;

        while (current != null)
        {
            parent = current;
            comparison = Comparer.Compare(key, current.Key);

            if (comparison == 0) return;

            current = comparison < 0 ? current.Left : current.Right;
        }

        TNode newNode = CreateNode(key, value);
        newNode.Parent = parent;

        if (comparison < 0)
        {
            parent!.Left = newNode;
        }
        else
        {
            parent!.Right = newNode;
        }

        Count++;
        OnNodeAdded(newNode);
    }

    public virtual bool Remove(TKey key)
    {
        TNode? node = FindNode(key);
        if (node == null)
        {
            return false;
        }

        RemoveNode(node);
        Count--;
        return true;
    }

    protected virtual void RemoveNode(TNode node)
    {
        TNode? hookParent;
        TNode? hookChild;

        if (node.Left == null)
        {
            hookParent = node.Parent;
            hookChild = node.Right;
            Transplant(node, node.Right);
        }
        else if (node.Right == null)
        {
            hookParent = node.Parent;
            hookChild = node.Left;
            Transplant(node, node.Left);
        }
        else
        {
            TNode successor = Minimum(node.Right);

            if (successor.Parent != node)
            {
                hookParent = successor.Parent;
                hookChild = successor.Right;
                Transplant(successor, successor.Right);
                successor.Right = node.Right;
                successor.Right.Parent = successor;
            }
            else
            {
                hookParent = successor;
                hookChild = successor.Right;
            }

            Transplant(node, successor);
            successor.Left = node.Left;
            successor.Left.Parent = successor;
        }

        node.Left = null;
        node.Right = null;
        node.Parent = null;

        OnNodeRemoved(hookParent, hookChild);
    }

    public virtual bool ContainsKey(TKey key) => FindNode(key) != null;

    public virtual bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        TNode? node = FindNode(key);
        if (node != null)
        {
            value = node.Value;
            return true;
        }

        value = default;
        return false;
    }

    public TValue this[TKey key]
    {
        get => TryGetValue(key, out TValue? value) ? value : throw new KeyNotFoundException();
        set => Add(key, value);
    }

    #region Hooks

    protected virtual void OnNodeAdded(TNode newNode) {}

    protected virtual void OnNodeRemoved(TNode? parent, TNode? child) {}

    #endregion

    #region Helpers

    protected abstract TNode CreateNode(TKey key, TValue value);

    protected TNode? FindNode(TKey key)
    {
        TNode? current = Root;
        while (current != null)
        {
            int comparison = Comparer.Compare(key, current.Key);
            if (comparison == 0)
            {
                return current;
            }

            current = comparison < 0 ? current.Left : current.Right;
        }
        return null;
    }

    protected TNode RotateLeft(TNode node)
    {
        TNode? pivot = node.Right;
        if (pivot == null) return node;

        node.Right = pivot.Left;
        if (pivot.Left != null) pivot.Left.Parent = node;

        pivot.Parent = node.Parent;
        if (node.Parent == null) Root = pivot;

        else if (node.IsLeftChild) node.Parent.Left = pivot;

        else node.Parent.Right = pivot;

        pivot.Left = node;
        node.Parent = pivot;

        return pivot;
    }

    protected TNode RotateRight(TNode node)
    {
        TNode? pivot = node.Left;
        if (pivot == null) return node;

        node.Left = pivot.Right;
        if (pivot.Right != null) pivot.Right.Parent = node;

        pivot.Parent = node.Parent;
        if (node.Parent == null) Root = pivot;

        else if (node.IsLeftChild) node.Parent.Left = pivot;
    
        else node.Parent.Right = pivot;

        pivot.Right = node;
        node.Parent = pivot;

        return pivot;
    }

    protected TNode RotateBigLeft(TNode node) => RotateDoubleLeft(node);
    protected TNode RotateBigRight(TNode node) => RotateDoubleRight(node);

    protected TNode RotateDoubleLeft(TNode node)
    {
        if (node.Right != null)
        {
            RotateRight(node.Right);
        }

        return RotateLeft(node);
    }

    protected TNode RotateDoubleRight(TNode node)
    {
        if (node.Left != null)
        {
            RotateLeft(node.Left);
        }

        return RotateRight(node);
    }

    protected void Transplant(TNode source, TNode? replacement)
    {
        if (source.Parent == null)
        {
            Root = replacement;
        }
        else if (source.IsLeftChild)
        {
            source.Parent.Left = replacement;
        }
        else
        {
            source.Parent.Right = replacement;
        }

        if (replacement != null)
        {
            replacement.Parent = source.Parent;
        }
    }

    protected TNode Minimum(TNode node)
    {
        TNode current = node;
        while (current.Left != null)
        {
            current = current.Left;
        }
        return current;
    }

    private Dictionary<TNode, int> BuildHeightMap()
    {
        Dictionary<TNode, int> heights = new();
        if (Root == null)
        {
            return heights;
        }

        Stack<(TNode Node, bool Expanded)> stack = new();
        stack.Push((Root, false));

        while (stack.Count > 0)
        {
            (TNode node, bool expanded) = stack.Pop();

            if (!expanded)
            {
                stack.Push((node, true));

                if (node.Right != null)
                {
                    stack.Push((node.Right, false));
                }

                if (node.Left != null)
                {
                    stack.Push((node.Left, false));
                }

                continue;
            }

            int leftHeight = node.Left != null ? heights[node.Left] : 0;
            int rightHeight = node.Right != null ? heights[node.Right] : 0;
            heights[node] = Math.Max(leftHeight, rightHeight) + 1;
        }

        return heights;
    }

    private List<TreeEntry<TKey, TValue>> BuildTraversal(TraversalStrategy strategy)
    {
        List<TreeEntry<TKey, TValue>> result = [];
        if (Root == null)
        {
            return result;
        }

        Dictionary<TNode, int> heights = BuildHeightMap();

        switch (strategy)
        {
            case TraversalStrategy.InOrder:
            case TraversalStrategy.InOrderReverse:
                BuildInOrder(result, heights, strategy == TraversalStrategy.InOrderReverse);
                break;
            case TraversalStrategy.PreOrder:
            case TraversalStrategy.PreOrderReverse:
                BuildPreOrder(result, heights, strategy == TraversalStrategy.PreOrderReverse);
                break;
            case TraversalStrategy.PostOrder:
            case TraversalStrategy.PostOrderReverse:
                BuildPostOrder(result, heights, strategy == TraversalStrategy.PostOrderReverse);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(strategy), strategy, null);
        }

        return result;
    }

    private void BuildInOrder(List<TreeEntry<TKey, TValue>> result, IReadOnlyDictionary<TNode, int> heights, bool reverse)
    {
        Stack<TNode> stack = new();
        TNode? current = Root;

        while (current != null || stack.Count > 0)
        {
            while (current != null)
            {
                stack.Push(current);
                current = reverse ? current.Right : current.Left;
            }

            current = stack.Pop();
            result.Add(new TreeEntry<TKey, TValue>(current.Key, current.Value, heights[current]));
            current = reverse ? current.Left : current.Right;
        }
    }

    private void BuildPreOrder(List<TreeEntry<TKey, TValue>> result, IReadOnlyDictionary<TNode, int> heights, bool reverse)
    {
        Stack<TNode> stack = new();
        stack.Push(Root!);

        while (stack.Count > 0)
        {
            TNode node = stack.Pop();
            result.Add(new TreeEntry<TKey, TValue>(node.Key, node.Value, heights[node]));

            TNode? right = node.Right;
            TNode? left = node.Left;

            if (right != null)
            {
                stack.Push(right);
            }

            if (left != null)
            {
                stack.Push(left);
            }
        }

        if (reverse)
        {
            result.Reverse();
        }
    }

    private void BuildPostOrder(List<TreeEntry<TKey, TValue>> result, IReadOnlyDictionary<TNode, int> heights, bool reverse)
    {
        Stack<(TNode Node, bool Expanded)> stack = new();
        stack.Push((Root!, false));

        while (stack.Count > 0)
        {
            (TNode node, bool expanded) = stack.Pop();

            if (expanded)
            {
                result.Add(new TreeEntry<TKey, TValue>(node.Key, node.Value, heights[node]));
                continue;
            }

            stack.Push((node, true));

            TNode? right = node.Right;
            TNode? left = node.Left;

            if (right != null)
            {
                stack.Push((right, false));
            }

            if (left != null)
            {
                stack.Push((left, false));
            }
        }

        if (reverse)
        {
            result.Reverse();
        }
    }

    #endregion

    public IEnumerable<TreeEntry<TKey, TValue>> InOrder() => new TreeIterator(this, TraversalStrategy.InOrder);
    public IEnumerable<TreeEntry<TKey, TValue>> PreOrder() => new TreeIterator(this, TraversalStrategy.PreOrder);
    public IEnumerable<TreeEntry<TKey, TValue>> PostOrder() => new TreeIterator(this, TraversalStrategy.PostOrder);
    public IEnumerable<TreeEntry<TKey, TValue>> InOrderReverse() => new TreeIterator(this, TraversalStrategy.InOrderReverse);
    public IEnumerable<TreeEntry<TKey, TValue>> PreOrderReverse() => new TreeIterator(this, TraversalStrategy.PreOrderReverse);
    public IEnumerable<TreeEntry<TKey, TValue>> PostOrderReverse() => new TreeIterator(this, TraversalStrategy.PostOrderReverse);

    private sealed class TreeIterator(BinarySearchTreeBase<TKey, TValue, TNode> tree, TraversalStrategy strategy)
        : IEnumerable<TreeEntry<TKey, TValue>>, IEnumerator<TreeEntry<TKey, TValue>>
    {
        private readonly IReadOnlyList<TreeEntry<TKey, TValue>> _entries = tree.BuildTraversal(strategy);
        private int _index = -1;

        public IEnumerator<TreeEntry<TKey, TValue>> GetEnumerator() => new TreeIterator(tree, strategy);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public TreeEntry<TKey, TValue> Current =>
            _index >= 0 && _index < _entries.Count
                ? _entries[_index]
                : throw new InvalidOperationException("Iterator is not positioned on an element.");

        object IEnumerator.Current => Current;

        public bool MoveNext()
        {
            if (_index + 1 >= _entries.Count)
            {
                return false;
            }

            _index++;
            return true;
        }

        public void Reset()
        {
            _index = -1;
        }

        public void Dispose()
        {
        }
    }

    private enum TraversalStrategy
    {
        InOrder,
        PreOrder,
        PostOrder,
        InOrderReverse,
        PreOrderReverse,
        PostOrderReverse
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => new DictionaryIterator(this);

    private sealed class DictionaryIterator(BinarySearchTreeBase<TKey, TValue, TNode> tree)
        : IEnumerator<KeyValuePair<TKey, TValue>>
    {
        private readonly IEnumerator<TreeEntry<TKey, TValue>> _inner = new TreeIterator(tree, TraversalStrategy.InOrder);

        public KeyValuePair<TKey, TValue> Current
        {
            get
            {
                TreeEntry<TKey, TValue> entry = _inner.Current;
                return new KeyValuePair<TKey, TValue>(entry.Key, entry.Value);
            }
        }

        object IEnumerator.Current => Current;

        public bool MoveNext() => _inner.MoveNext();

        public void Reset() => _inner.Reset();

        public void Dispose() => _inner.Dispose();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

    public void Clear()
    {
        Root = null;
        Count = 0;
    }

    public bool Contains(KeyValuePair<TKey, TValue> item)
    {
        return TryGetValue(item.Key, out TValue? value)
               && EqualityComparer<TValue>.Default.Equals(value, item.Value);
    }

    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        ArgumentNullException.ThrowIfNull(array);

        if (arrayIndex < 0 || arrayIndex > array.Length)
            throw new ArgumentOutOfRangeException(nameof(arrayIndex));

        if (array.Length - arrayIndex < Count)
            throw new ArgumentException("Destination array is too small.", nameof(array));

        foreach (KeyValuePair<TKey, TValue> pair in this)
        {
            array[arrayIndex++] = pair;
        }
    }

    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
        return Contains(item) && Remove(item.Key);
    }
}
