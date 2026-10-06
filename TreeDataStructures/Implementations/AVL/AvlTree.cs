using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.AVL;

public class AvlTree<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, AvlNode<TKey, TValue>>
{
    protected override AvlNode<TKey, TValue> CreateNode(TKey key, TValue value)
        => new(key, value);

    protected override void OnNodeAdded(AvlNode<TKey, TValue> newNode)
    {
        RebalanceUpwards(newNode);
    }

    protected override void OnNodeRemoved(AvlNode<TKey, TValue>? parent, AvlNode<TKey, TValue>? child) {}

    protected override void RemoveNode(AvlNode<TKey, TValue> node)
    {
        if (node.Left == null)
        {
            AvlNode<TKey, TValue>? rebalanceStart = node.Parent;
            Transplant(node, node.Right);
            node.Left = null;
            node.Right = null;
            node.Parent = null;
            RebalanceUpwards(rebalanceStart);
            return;
        }

        if (node.Right == null)
        {
            AvlNode<TKey, TValue>? rebalanceStart = node.Parent;
            Transplant(node, node.Left);
            node.Left = null;
            node.Right = null;
            node.Parent = null;
            RebalanceUpwards(rebalanceStart);
            return;
        }

        AvlNode<TKey, TValue> replacement = Minimum(node.Right);
        AvlNode<TKey, TValue>? oldReplacementParent = replacement.Parent;

        if (replacement.Parent != node)
        {
            Transplant(replacement, replacement.Right);
            replacement.Right = node.Right;
            replacement.Right.Parent = replacement;
        }

        Transplant(node, replacement);
        replacement.Left = node.Left;
        replacement.Left.Parent = replacement;
        UpdateHeight(replacement);

        node.Left = null;
        node.Right = null;
        node.Parent = null;

        if (oldReplacementParent != null && oldReplacementParent != node)
        {
            RebalanceUpwards(oldReplacementParent);
        }

        RebalanceUpwards(replacement);
    }

    private void RebalanceUpwards(AvlNode<TKey, TValue>? start)
    {
        AvlNode<TKey, TValue>? current = start;

        while (current != null)
        {
            UpdateHeight(current);
            AvlNode<TKey, TValue> subtreeRoot = RebalanceNode(current);
            UpdateHeight(subtreeRoot);
            current = subtreeRoot.Parent;
        }
    }

    private AvlNode<TKey, TValue> RebalanceNode(AvlNode<TKey, TValue> node)
    {
        int balance = GetBalance(node);

        if (balance > 1)
        {
            if (GetBalance(node.Left) < 0)
            {
                AvlNode<TKey, TValue> leftChild = node.Left!;
                AvlNode<TKey, TValue> leftPivot = RotateLeft(leftChild);
                UpdateHeight(leftChild);
                UpdateHeight(leftPivot);
            }

            AvlNode<TKey, TValue> newRoot = RotateRight(node);
            UpdateHeight(node);
            UpdateHeight(newRoot);
            return newRoot;
        }

        if (balance < -1)
        {
            if (GetBalance(node.Right) > 0)
            {
                AvlNode<TKey, TValue> rightChild = node.Right!;
                AvlNode<TKey, TValue> rightPivot = RotateRight(rightChild);
                UpdateHeight(rightChild);
                UpdateHeight(rightPivot);
            }

            AvlNode<TKey, TValue> newRoot = RotateLeft(node);
            UpdateHeight(node);
            UpdateHeight(newRoot);
            return newRoot;
        }

        return node;
    }

    private static int GetHeight(AvlNode<TKey, TValue>? node) => node?.Height ?? 0;

    private static void UpdateHeight(AvlNode<TKey, TValue> node)
    {
        node.Height = Math.Max(GetHeight(node.Left), GetHeight(node.Right)) + 1;
    }

    private static int GetBalance(AvlNode<TKey, TValue>? node)
    {
        return node == null ? 0 : GetHeight(node.Left) - GetHeight(node.Right);
    }
}
