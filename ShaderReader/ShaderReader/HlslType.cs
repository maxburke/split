using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Hlsl
{
    class Type
    {
        int TypeTag;
        public int Tag { get { return TypeTag; } }

        protected Type(int typeTag)
        {
            TypeTag = typeTag;
        }

        public static bool operator ==(Type a, Type b)
        {
            return a.TypeTag == b.TypeTag;
        }

        public static bool operator !=(Type a, Type b)
        {
            return a.TypeTag != b.TypeTag;
        }
    }

    class BoolType : Type
    {
        public BoolType()
            : base(1)
        {
        }
    }

    class FloatType : Type
    {
        public FloatType()
            : base(2)
        {
        }
    }

    class IntType : Type
    {
        public IntType()
            : base(3)
        {
        }
    }

    class UIntType : Type
    {
        public UIntType()
            : base(4)
        {
        }
    }

    class SamplerType : Type
    {
        public SamplerType()
            : base(5)
        {
        }
    }

    class UndefinedType : Type
    {
        public UndefinedType()
            : base(6)
        { }
    }

    class DerivedType : Type
    {
        public readonly Type BaseType;

        protected DerivedType(int typeTag, Type baseType)
            : base(typeTag)
        {
            BaseType = baseType;
        }
    }

    class VectorType : DerivedType
    {
        public readonly int Dimension;

        public VectorType(Type baseType, int dimension)
            : base((1 << 8) | baseType.Tag, baseType)
        {
            Dimension = dimension;
        }
    }

    class ArrayType : DerivedType
    {
        public ArrayType(Type baseType)
            : base((2 << 8) | baseType.Tag, baseType)
        {
        }
    }

    class MatrixType : DerivedType
    {
        public readonly int Dimension;

        public MatrixType(Type baseType, int dimension)
            : base((3 << 8) | baseType.Tag, baseType)
        {
            Debug.Assert(baseType is VectorType);
            Dimension = dimension;
        }
    }

    enum Semantic
    {
        NONE,

        // Vertex shader semantics
        BINORMAL,
        BLENDINDICES,
        BLENDWEIGHT,
        COLOR,
        NORMAL,
        POSITION,
        POSITIONT,
        PSIZE,
        TANGENT,
        TEXCOORD,
        FOG,
        TESSFACTOR,
        INDEX,

        // Pixel shader specific semantics
        VFACE,
        VPOS,
        DEPTH
    }

    struct StructField
    {
        public StructField(Type type, string name, Semantic semantic)
        {
            FieldType = type;
            FieldName = name;
            FieldSemantic = semantic;
        }

        public Type FieldType;
        public string FieldName;
        public Semantic FieldSemantic;
    }

    class StructType : Type
    {
        public readonly StructField[] Fields;
        public readonly string Name;

        public StructType(string name, StructField[] fields)
            : base(103)
        {
            Fields = fields;
            Name = name;
        }
    }
}
