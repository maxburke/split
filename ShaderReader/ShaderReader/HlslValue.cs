using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Hlsl
{
    class Value
    {
        public Type ValueType { get; private set; }
        public string Name;

        public Value(Type valueType, string name)
        {
            ValueType = valueType;
            Name = name;
        }
    }
}
