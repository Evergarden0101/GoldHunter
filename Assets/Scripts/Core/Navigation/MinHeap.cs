using System.Collections.Generic;

namespace GoldHunter.Core.Navigation
{
    /// <summary>Binary min-heap keyed on f-score, used as the A* open set.</summary>
    public sealed class MinHeap
    {
        private readonly List<(int node, float f)> _items = new List<(int, float)>();

        public int Count => _items.Count;

        public void Clear() => _items.Clear();

        public void Push(int node, float f)
        {
            _items.Add((node, f));
            int i = _items.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) >> 1;
                if (_items[parent].f <= _items[i].f) break;
                (_items[parent], _items[i]) = (_items[i], _items[parent]);
                i = parent;
            }
        }

        public int Pop()
        {
            int top = _items[0].node;
            var last = _items[_items.Count - 1];
            _items.RemoveAt(_items.Count - 1);
            if (_items.Count > 0)
            {
                _items[0] = last;
                int i = 0;
                while (true)
                {
                    int l = i * 2 + 1;
                    int r = l + 1;
                    int m = i;
                    if (l < _items.Count && _items[l].f < _items[m].f) m = l;
                    if (r < _items.Count && _items[r].f < _items[m].f) m = r;
                    if (m == i) break;
                    (_items[m], _items[i]) = (_items[i], _items[m]);
                    i = m;
                }
            }
            return top;
        }
    }
}
