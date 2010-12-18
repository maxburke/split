



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

