using System.Diagnostics.CodeAnalysis;
using TreeDataStructures.Implementations.BST;

namespace TreeDataStructures.Implementations.Splay;

public class SplayTree<TKey, TValue> : BinarySearchTree<TKey, TValue>
{
    protected override BstNode<TKey, TValue> CreateNode(TKey key, TValue value) => new(key, value);

    public override void Add(TKey key, TValue value)
    {
        if (Root == null)
        {
            Root = CreateNode(key, value);
            Count = 1;
            return;
        }

        BstNode<TKey, TValue>? current = Root;
        BstNode<TKey, TValue>? parent = null;
        int comparison = 0;

        while (current != null)
        {
            parent = current;
            comparison = Comparer.Compare(key, current.Key);

            if (comparison == 0)
            {
                current.Value = value;
                Splay(current);
                return;
            }

            current = comparison < 0 ? current.Left : current.Right;
        }

        BstNode<TKey, TValue> newNode = CreateNode(key, value);
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
        Splay(newNode);
    }

    public override bool Remove(TKey key)
    {
        BstNode<TKey, TValue>? node = FindClosestNode(key);
        if (node == null)
        {
            return false;
        }

        Splay(node);
        if (Comparer.Compare(key, node.Key) != 0)
        {
            return false;
        }

        BstNode<TKey, TValue>? left = node.Left;
        BstNode<TKey, TValue>? right = node.Right;

        if (left != null)
        {
            left.Parent = null;
        }

        if (right != null)
        {
            right.Parent = null;
        }

        node.Left = null;
        node.Right = null;
        node.Parent = null;

        Root = Join(left, right);
        Count--;
        return true;
    }

    protected override void OnNodeAdded(BstNode<TKey, TValue> newNode)
    {
        Splay(newNode);
    }

    protected override void OnNodeRemoved(BstNode<TKey, TValue>? parent, BstNode<TKey, TValue>? child)
    {
        if (parent != null)
        {
            Splay(parent);
        }
        else if (child != null)
        {
            Splay(child);
        }
    }

    public override bool ContainsKey(TKey key)
    {
        BstNode<TKey, TValue>? node = FindClosestNode(key);
        if (node == null)
        {
            return false;
        }

        Splay(node);
        return Comparer.Compare(key, node.Key) == 0;
    }

    public override bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        BstNode<TKey, TValue>? node = FindClosestNode(key);
        if (node == null)
        {
            value = default;
            return false;
        }

        Splay(node);
        if (Comparer.Compare(key, node.Key) != 0)
        {
            value = default;
            return false;
        }

        value = node.Value;
        return true;
    }

    private BstNode<TKey, TValue>? FindClosestNode(TKey key)
    {
        BstNode<TKey, TValue>? current = Root;
        BstNode<TKey, TValue>? lastVisited = null;

        while (current != null)
        {
            lastVisited = current;
            int comparison = Comparer.Compare(key, current.Key);
            if (comparison == 0)
            {
                return current;
            }

            current = comparison < 0 ? current.Left : current.Right;
        }

        return lastVisited;
    }

    private BstNode<TKey, TValue>? Join(BstNode<TKey, TValue>? left, BstNode<TKey, TValue>? right)
    {
        if (left == null)
        {
            return right;
        }

        if (right == null)
        {
            return left;
        }

        Root = left;
        BstNode<TKey, TValue> maximum = left;
        while (maximum.Right != null)
        {
            maximum = maximum.Right;
        }

        Splay(maximum);
        Root!.Right = right;
        right.Parent = Root;
        return Root;
    }

    private void Splay(BstNode<TKey, TValue> node)
    {
        while (node.Parent != null)
        {
            if (node.Parent.Parent == null)
            {
                if (node.IsLeftChild)
                {
                    RotateRight(node.Parent);
                }
                else
                {
                    RotateLeft(node.Parent);
                }

                continue;
            }

            if (node.IsLeftChild && node.Parent.IsLeftChild)
            {
                RotateRight(node.Parent.Parent);
                RotateRight(node.Parent);
            }
            else if (node.IsRightChild && node.Parent.IsRightChild)
            {
                RotateLeft(node.Parent.Parent);
                RotateLeft(node.Parent);
            }
            else if (node.IsRightChild && node.Parent.IsLeftChild)
            {
                RotateLeft(node.Parent);
                RotateRight(node.Parent);
            }
            else
            {
                RotateRight(node.Parent);
                RotateLeft(node.Parent);
            }
        }
    }
}
