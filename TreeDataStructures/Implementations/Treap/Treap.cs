using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.Treap;

public class Treap<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, TreapNode<TKey, TValue>>
{
    public Treap()
    {
    }

    public Treap(IComparer<TKey> comparer) : base(comparer)
    {
    }

    protected virtual (TreapNode<TKey, TValue>? Left, TreapNode<TKey, TValue>? Right) Split(TreapNode<TKey, TValue>? root, TKey key)
    {
        if (root == null)
        {
            return (null, null);
        }

        if (Comparer.Compare(root.Key, key) <= 0)
        {
            (TreapNode<TKey, TValue>? leftOfRight, TreapNode<TKey, TValue>? right) = Split(root.Right, key);
            root.Right = leftOfRight;
            SetParent(root.Right, root);
            root.Parent = null;
            return (root, DetachRoot(right));
        }

        (TreapNode<TKey, TValue>? left, TreapNode<TKey, TValue>? rightOfLeft) = Split(root.Left, key);
        root.Left = rightOfLeft;
        SetParent(root.Left, root);
        root.Parent = null;
        return (DetachRoot(left), root);
    }

    protected virtual TreapNode<TKey, TValue>? Merge(TreapNode<TKey, TValue>? left, TreapNode<TKey, TValue>? right)
    {
        if (left == null)
        {
            return DetachRoot(right);
        }

        if (right == null)
        {
            return DetachRoot(left);
        }

        if (left.Priority >= right.Priority)
        {
            left.Right = Merge(left.Right, right);
            SetParent(left.Right, left);
            left.Parent = null;
            return left;
        }

        right.Left = Merge(left, right.Left);
        SetParent(right.Left, right);
        right.Parent = null;
        return right;
    }

    public override void Add(TKey key, TValue value)
    {
        TreapNode<TKey, TValue>? existing = FindNode(key);
        if (existing != null)
        {
            existing.Value = value;
            return;
        }

        TreapNode<TKey, TValue> newNode = CreateNode(key, value);
        (TreapNode<TKey, TValue>? left, TreapNode<TKey, TValue>? right) = Split(Root, key);
        Root = Merge(Merge(left, newNode), right);
        SetParent(Root, null);
        Count++;
        OnNodeAdded(newNode);
    }

    public override bool Remove(TKey key)
    {
        TreapNode<TKey, TValue>? node = FindNode(key);
        if (node == null)
        {
            return false;
        }

        TreapNode<TKey, TValue>? mergedChildren = Merge(node.Left, node.Right);

        if (node.Parent == null)
        {
            Root = mergedChildren;
        }
        else if (node.IsLeftChild)
        {
            node.Parent.Left = mergedChildren;
        }
        else
        {
            node.Parent.Right = mergedChildren;
        }

        TreapNode<TKey, TValue>? parent = node.Parent;
        SetParent(mergedChildren, parent);

        node.Left = null;
        node.Right = null;
        node.Parent = null;

        Count--;
        OnNodeRemoved(parent, mergedChildren);
        return true;
    }

    protected override TreapNode<TKey, TValue> CreateNode(TKey key, TValue value) => new(key, value);

    protected override void OnNodeAdded(TreapNode<TKey, TValue> newNode)
    {
    }

    protected override void OnNodeRemoved(TreapNode<TKey, TValue>? parent, TreapNode<TKey, TValue>? child)
    {
    }

    private static void SetParent(TreapNode<TKey, TValue>? node, TreapNode<TKey, TValue>? parent)
    {
        if (node != null)
        {
            node.Parent = parent;
        }
    }

    private static TreapNode<TKey, TValue>? DetachRoot(TreapNode<TKey, TValue>? node)
    {
        if (node != null)
        {
            node.Parent = null;
        }

        return node;
    }
}
