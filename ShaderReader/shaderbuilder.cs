
class Type
{
    protected readonly int TypeTag;

    static bool operator ==(Type a, Type b)
    {
        return a.TypeTag == b.TypeTag;
    }

    static bool operator !=(Type a, Type b)
    {
        return a.TypeTag != b.TypeTag;
    }
}

class BoolType : Type
{
    public BoolType()
    {
        TypeTag = 1;
    }
}

class FloatType : Type
{
    public FloatType()
    {
        TypeTag = 2;
    }
}

class IntType : Type
{
    public IntType()
    {
        TypeTag = 3;
    }
}

class UIntType : Type
{
    public UIntType()
    {
        TypeTag = 4;
    }
}

class SamplerType : Type
{
    public SamplerType()
    {
        TypeTag = 5;
    }
}

class DerivedType : Type
{
    public readonly Type BaseType;
}

class VectorType : DerivedType
{
    public readonly int Dimension;

    public VectorType(Type baseType, int dimension)
    {
        Dimension = dimension;
        TypeTag = 100;
        BaseType = baseType;
    }
}

class ArrayType : DerivedType
{
    public ArrayType(Type baseType)
    {
        TypeTag = 101;
        BaseType = baseType;
    }
}

class MatrixType : DerivedType
{
    public readonly int Dimension;

    public MatrixType(Type baseType, int dimension)
    {
        Debug.Assert(baseType is VectorType);
        TypeTag = 102;
        Dimension = dimension;
    }
}

enum Semantic
{
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
    COLOR,
    FOG,
    POSITION,
    PSIZE,
    TESSFACTOR,
    TEXCOORD,
    INDEX,

    // Pixel shader semantics
    COLOR,
    TEXCOORD,
    VFACE,
    VPOS,
    COLOR,
    DEPTH
}

class StructType : Type
{
    public struct StructField
    {
        public StructField(FieldType type, Semantic semantic)
        {
            FieldType = type;
            FieldSemantic = semantic;
        }

        public Type FieldType;
        public Semantic FieldSemantic;
    }

    public readonly StructField[] Fields;

    public StructType(StructField[] fields)
    {
        TypeTag = 103;
        Fields = fields;
    }
}


class Value
{
    protected Type ValueType;
    string Name;
}

class GlobalValue : Value
{
}

class User : Value
{
    List<Value> Operands;
}

class Insn : User
{
    public Ops Op;

    public enum Ops
    {
        FIRST_TERMOP,
        BREAK,
        CONTINUE,
        SWITCH,
        LAST_TERMOP,

        FIRST_UNARYOP,
        CAST,
        NEGATE,
        LAST_UNARYOP, 

        FIRST_LOGICALOP,
        NOT,
        SHL,
        SHR,
        AND,
        OR,
        XOR,
        LAST_LOGICALOP,

        FIRST_BOOLOP,
        BOOL_AND,
        BOOL_OR,
        LAST_BOOLOP,

        FIRST_BINOP,
        ADD,
        SUB,
        MUL,
        DIV,
        MOD,
        LAST_BINOP,

        SWIZZLE,
        STRUCT_MEMBER_ACCESS,
    }
}

class BinOpInsn : Insn
{
    public BinOpInsn(Insn.Ops op, Value lhs, Value rhs)
    {
        Debug.Assert(lhs.ValueType == rhs.ValueType);
        Debug.Assert(op > Insn.Ops.FIRST_BINOP && ops < Insn.Ops.LAST_BINOP);
        ValueType = lhs.ValueType;
        Operands.Add(lhs);
        Operands.Add(rhs);
        Op = op;
    }
}

class CastInsn : Insn
{
    public CastInsn(Type toType, Value v)
    {
        ValueType = toType;
        Operands.Add(v);
        Op = Insn.Ops.CAST;
    }
}

class SwizzleInsn : Insn
{
    public SwizzleInsn(Value v, Value[] swizzle)
    {
        Debug.Assert(v.ValueType is VectorType);
        Debug.Assert(v.ValueType.Dimension == swizzle.Length);
        ValueType = v.ValueType;
        Operands.Add(v);
        for (int i = 0; i < swizzle.Length; ++i)
            Operands.Add(swizzle[i]);
        Op = Insn.Ops.SWIZZLE;
    }
}

class StructMemberAccess : INSN

class BasicBlock : Value
{
    Insn LastInsn;
}

class Function : GlobalValue
{
    List<BasicBlock> BasicBlockList;
    List<Value> Arguments;
}

