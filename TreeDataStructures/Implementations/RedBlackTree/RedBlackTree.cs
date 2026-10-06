using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.RedBlackTree;

public class RedBlackTree<TKey, TValue> : BinarySearchTreeBase<TKey, TValue, RbNode<TKey, TValue>>
{
    protected override RbNode<TKey, TValue> CreateNode(TKey key, TValue value) => new(key, value);

    protected override void OnNodeAdded(RbNode<TKey, TValue> newNode)
    {
        InsertFixup(newNode);
        if (Root != null)
        {
            Root.Color = RbColor.Black;
        }
    }

    protected override void OnNodeRemoved(RbNode<TKey, TValue>? parent, RbNode<TKey, TValue>? child)
    {
    }

    protected override void RemoveNode(RbNode<TKey, TValue> node)
    {
        RbNode<TKey, TValue> removedOrMoved = node;
        RbColor removedOriginalColor = removedOrMoved.Color;
        RbNode<TKey, TValue>? replacement;
        RbNode<TKey, TValue>? replacementParent;

        if (node.Left == null)
        {
            replacement = node.Right;
            replacementParent = node.Parent;
            Transplant(node, node.Right);
        }
        else if (node.Right == null)
        {
            replacement = node.Left;
            replacementParent = node.Parent;
            Transplant(node, node.Left);
        }
        else
        {
            removedOrMoved = Minimum(node.Right);
            removedOriginalColor = removedOrMoved.Color;
            replacement = removedOrMoved.Right;

            if (removedOrMoved.Parent == node)
            {
                replacementParent = removedOrMoved;
            }
            else
            {
                replacementParent = removedOrMoved.Parent;
                Transplant(removedOrMoved, removedOrMoved.Right);
                removedOrMoved.Right = node.Right;
                removedOrMoved.Right.Parent = removedOrMoved;
            }

            Transplant(node, removedOrMoved);
            removedOrMoved.Left = node.Left;
            removedOrMoved.Left.Parent = removedOrMoved;
            removedOrMoved.Color = node.Color;
        }

        node.Left = null;
        node.Right = null;
        node.Parent = null;

        if (removedOriginalColor == RbColor.Black)
        {
            DeleteFixup(replacement, replacementParent);
        }

        if (Root != null)
        {
            Root.Color = RbColor.Black;
        }
    }

    private void InsertFixup(RbNode<TKey, TValue> node)
    {
        RbNode<TKey, TValue> current = node;

        while (current.Parent != null && current.Parent.Color == RbColor.Red)
        {
            if (current.Parent.IsLeftChild)
            {
                RbNode<TKey, TValue>? uncle = current.Parent.Parent?.Right; 
                if (GetColor(uncle) == RbColor.Red)
                {
                    current.Parent.Color = RbColor.Black;
                    if (uncle != null)
                    {
                        uncle.Color = RbColor.Black;
                    }

                    current.Parent.Parent!.Color = RbColor.Red;
                    current = current.Parent.Parent;
                }
                else
                {
                    if (current.IsRightChild)
                    {
                        current = current.Parent;
                        RotateLeft(current);
                    }

                    current.Parent!.Color = RbColor.Black;
                    current.Parent.Parent!.Color = RbColor.Red;
                    RotateRight(current.Parent.Parent);
                }
            }
            else
            {
                RbNode<TKey, TValue>? uncle = current.Parent.Parent?.Left;
                if (GetColor(uncle) == RbColor.Red)
                {
                    current.Parent.Color = RbColor.Black;
                    if (uncle != null)
                    {
                        uncle.Color = RbColor.Black;
                    }

                    current.Parent.Parent!.Color = RbColor.Red;
                    current = current.Parent.Parent;
                }
                else
                {
                    if (current.IsLeftChild)
                    {
                        current = current.Parent;
                        RotateRight(current);
                    }

                    current.Parent!.Color = RbColor.Black;
                    current.Parent.Parent!.Color = RbColor.Red;
                    RotateLeft(current.Parent.Parent);
                }
            }
        }
    }

    private void DeleteFixup(RbNode<TKey, TValue>? node, RbNode<TKey, TValue>? parent)
    {
        RbNode<TKey, TValue>? current = node;
        RbNode<TKey, TValue>? currentParent = parent;

        while (current != Root && GetColor(current) == RbColor.Black)
        {
            if (currentParent == null)
            {
                break;
            }

            bool isLeftChild = currentParent.Left == current;
            RbNode<TKey, TValue>? sibling = isLeftChild ? currentParent.Right : currentParent.Left;

            if (GetColor(sibling) == RbColor.Red)
            {
                sibling!.Color = RbColor.Black;
                currentParent.Color = RbColor.Red;

                if (isLeftChild)
                {
                    RotateLeft(currentParent);
                    sibling = currentParent.Right;
                }
                else
                {
                    RotateRight(currentParent);
                    sibling = currentParent.Left;
                }
            }

            if (GetColor(sibling?.Left) == RbColor.Black && GetColor(sibling?.Right) == RbColor.Black)
            {
                if (sibling != null)
                {
                    sibling.Color = RbColor.Red;
                }

                current = currentParent;
                currentParent = current.Parent;
                continue;
            }

            if (isLeftChild)
            {
                if (GetColor(sibling?.Right) == RbColor.Black)
                {
                    if (sibling?.Left != null)
                    {
                        sibling.Left.Color = RbColor.Black;
                    }

                    if (sibling != null)
                    {
                        sibling.Color = RbColor.Red;
                        RotateRight(sibling);
                    }

                    sibling = currentParent.Right;
                }

                if (sibling != null)
                {
                    sibling.Color = currentParent.Color;
                }

                currentParent.Color = RbColor.Black;
                if (sibling?.Right != null)
                {
                    sibling.Right.Color = RbColor.Black;
                }

                RotateLeft(currentParent);
            }
            else
            {
                if (GetColor(sibling?.Left) == RbColor.Black)
                {
                    if (sibling?.Right != null)
                    {
                        sibling.Right.Color = RbColor.Black;
                    }

                    if (sibling != null)
                    {
                        sibling.Color = RbColor.Red;
                        RotateLeft(sibling);
                    }

                    sibling = currentParent.Left;
                }

                if (sibling != null)
                {
                    sibling.Color = currentParent.Color;
                }

                currentParent.Color = RbColor.Black;
                if (sibling?.Left != null)
                {
                    sibling.Left.Color = RbColor.Black;
                }

                RotateRight(currentParent);
            }

            current = Root;
            break;
        }

        if (current != null)
        {
            current.Color = RbColor.Black;
        }
    }

    private static RbColor GetColor(RbNode<TKey, TValue>? node)
    {
        return node?.Color ?? RbColor.Black;
    }
}
