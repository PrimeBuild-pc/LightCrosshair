using System.Collections.Generic;

namespace LightCrosshair
{
    public sealed class UndoStack<T>
    {
        private readonly int _cap;
        private readonly LinkedList<T> _items = new();
        public UndoStack(int cap = 10) => _cap = System.Math.Max(1, cap);
        public void Push(T item)
        {
            _items.AddFirst(item);
            if (_items.Count > _cap) _items.RemoveLast();
        }
        public bool TryPop(out T item)
        {
            if (_items.First is null)
            {
                item = default!;
                return false;
            }
            item = _items.First.Value;
            _items.RemoveFirst();
            return true;
        }
    }
}
