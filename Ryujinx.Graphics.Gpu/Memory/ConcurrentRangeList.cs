using System.Collections.Generic;

namespace Ryujinx.Graphics.Gpu.Memory
{
    class ConcurrentRangeList<T> where T : IRange<T>
    {
        private List<T> _items;

        public int Count => _items.Count;

        public ConcurrentRangeList()
        {
            _items = new List<T>();
        }

        public void Add(T item)
        {
            lock (_items)
            {
                int index = BinarySearch(item.Address);

                if (index < 0)
                {
                    index = ~index;
                }

                _items.Insert(index, item);
            }
        }

        public bool Remove(T item)
        {
            lock (_items)
            {
                int index = BinarySearch(item.Address);

                if (index >= 0)
                {
                    while (index > 0 && _items[index - 1].Address == item.Address)
                    {
                        index--;
                    }

                    while (index < _items.Count)
                    {
                        if (_items[index].Equals(item))
                        {
                            _items.RemoveAt(index);

                            return true;
                        }

                        if (_items[index].Address > item.Address)
                        {
                            break;
                        }

                        index++;
                    }
                }
            }

            return false;
        }

        public T FindFirstOverlap(T item)
        {
            return FindFirstOverlap(item.Address, item.Size);
        }

        public T FindFirstOverlap(ulong address, ulong size)
        {
            lock (_items)
            {
                int index = BinarySearch(address, size);

                if (index < 0)
                {
                    return default(T);
                }

                return _items[index];
            }
        }

        public T[] FindOverlaps(T item)
        {
            return FindOverlaps(item.Address, item.Size);
        }

        public T[] FindOverlaps(ulong address, ulong size)
        {
            List<T> overlapsList = new List<T>();

            ulong endAddress = address + size;

            lock (_items)
            {
                foreach (T item in _items)
                {
                    if (item.Address >= endAddress)
                    {
                        break;
                    }

                    if (item.OverlapsWith(address, size))
                    {
                        overlapsList.Add(item);
                    }
                }
            }

            return overlapsList.ToArray();
        }

        public T[] FindOverlaps(ulong address)
        {
            List<T> overlapsList = new List<T>();

            lock (_items)
            {
                int index = BinarySearch(address);

                if (index >= 0)
                {
                    while (index > 0 && _items[index - 1].Address == address)
                    {
                        index--;
                    }

                    while (index < _items.Count)
                    {
                        T overlap = _items[index++];

                        if (overlap.Address != address)
                        {
                            break;
                        }

                        overlapsList.Add(overlap);
                    }
                }
            }

            return overlapsList.ToArray();
        }

        private int BinarySearch(ulong address)
        {
            int left  = 0;
            int right = _items.Count - 1;

            while (left <= right)
            {
                int range = right - left;

                int middle = left + (range >> 1);

                T item = _items[middle];

                if (item.Address == address)
                {
                    return middle;
                }

                if (address < item.Address)
                {
                    right = middle - 1;
                }
                else
                {
                    left = middle + 1;
                }
            }

            return ~left;
        }

        private int BinarySearch(ulong address, ulong size)
        {
            int left  = 0;
            int right = _items.Count - 1;

            while (left <= right)
            {
                int range = right - left;

                int middle = left + (range >> 1);

                T item = _items[middle];

                if (item.OverlapsWith(address, size))
                {
                    return middle;
                }

                if (address < item.Address)
                {
                    right = middle - 1;
                }
                else
                {
                    left = middle + 1;
                }
            }

            return ~left;
        }
    }
}