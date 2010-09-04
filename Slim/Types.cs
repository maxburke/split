using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Slim
{
    public struct IntVector3
    {
        public int X;
        public int Y;
        public int Z;

        public IntVector3(int x, int y, int z)
        {
            X = x;
            Y = y;
            Z = z;
        }
    }

    public struct Pair<T, U>
    {
        public T first;
        public U second;

        public Pair(T _first, U _second)
        {
            first = _first;
            second = _second;
        }

        public override string ToString()
        {
            return string.Format("[{0}, {1}]", first.ToString(), second.ToString());
        }
    }
}
