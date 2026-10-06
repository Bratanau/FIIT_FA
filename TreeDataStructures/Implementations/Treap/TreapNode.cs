using TreeDataStructures.Core;

namespace TreeDataStructures.Implementations.Treap;

public class TreapNode<TKey, TValue>(TKey key, TValue value)
    : Node<TKey, TValue, TreapNode<TKey, TValue>>(key, value)
{
    public int  Priority { get; set; } = Random.Shared.Next();
}