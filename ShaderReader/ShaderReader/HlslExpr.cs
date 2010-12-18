using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hlsl
{
    enum Operator
    {
        FIRST_MODIFIER_OPERATOR,
        IDENTITY,
        ADD,
        SUB,
        MUL,
        DIV,
        MOD,
        SHL,
        SHR,
        AND,
        OR,
        XOR,
        LAST_MODIFIER_OPERATOR,

        NOT,
    }

    abstract class Expr
    {
        public abstract bool HasValue();
        public abstract Value Value { get; }
    }

    class ValueExpr : Expr
    {
        Value ContainedValue;

        public override bool HasValue()
        {
            return true;
        }

        public override Value Value
        {
            get { return ContainedValue; }
        }

        public ValueExpr(Value v)
        {
            ContainedValue = v;
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }
    }

    class DeclExpr : Expr
    {
        Type Type;
        string Name;
        Value InitValue;
        static int Counter;

        public DeclExpr(Type type)
        {
            Type = type;
            Name = string.Format("var{0}", Counter++);
            InitValue = null;
        }

        public DeclExpr(Type type, string name)
        {
            Type = type;
            Name = name;
            InitValue = null;
        }

        public DeclExpr(Type type, Value value)
        {
            Type = type;
            Name = string.Format("var{0}", Counter++);
            InitValue = value;
        }

        public DeclExpr(Type type, string name, Value value)
        {
            Type = type;
            Name = name;
            InitValue = value;
        }

        public override bool HasValue()
        {
            return true;
        }

        public override Value Value
        {
            get { return new Value(Type, Name); }
        }

        public override string ToString()
        {
            StringBuilder SB = new StringBuilder();
            if (InitValue == null)
                SB.AppendFormat("{0} {1};", Type.TypeName(), Name);
            else
                SB.AppendFormat("{0} {1} = {2};", Type.TypeName(), Name, InitValue);

            return SB.ToString();
        }
    }

    class GlobalVariableExpr : Expr
    {
        public override Value Value
        {
            get { throw new NotImplementedException(); }
        }

        public override bool HasValue()
        {
            throw new NotImplementedException();
        }
    }

    class CommaExpr : Expr
    {
        public readonly Expr LHS;
        public readonly Expr RHS;

        public CommaExpr(Expr lhs, Expr rhs)
        {
            if (!lhs.HasValue() || !rhs.HasValue())
                throw new ShaderDomException("Both the left and right hand sides of the comma expression must have a value!");

            LHS = lhs;
            RHS = rhs;
        }

        public override bool HasValue()
        {
            return true;
        }

        public override Value Value
        {
            get { return RHS.Value; }
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }
    }

    class CompoundExpr : Expr
    {
        List<Expr> Body = new List<Expr>();

        public void Add(Expr e)
        {
            Body.Add(e);
        }

        public override bool HasValue()
        {
            return false;
        }

        public override Value Value
        {
            get { throw new ShaderDomException("CompoundExprs have no value!"); }
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }
    }

    class IfExpr : CompoundExpr
    {
        Expr Test;

        public override bool HasValue()
        {
            return false;
        }

        public override Value Value
        {
            get { throw new ShaderDomException("IfExprs have no value!"); }
        }

        public IfExpr(Expr test)
        {
            if (!test.HasValue())
                throw new ShaderDomException("Test expression doesn't return a value!");

            Test = test;
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }
    }

    class ElseExpr : CompoundExpr
    {
        IfExpr AssociatedIfExpr;

        public override bool HasValue()
        {
            return false;
        }

        public override Value Value
        {
            get { throw new ShaderDomException("ElseExprs have no value!"); }
        }

        public ElseExpr(IfExpr associatedIfExpr)
        {
            AssociatedIfExpr = associatedIfExpr;
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }
    }

    class WhileExpr : CompoundExpr
    {
        public override bool HasValue()
        {
            return false;
        }

        public override Value Value
        {
            get { throw new ShaderDomException("WhileExprs have no value!"); }
        }

        public WhileExpr(Expr test)
        {
            if (!test.HasValue())
                throw new ShaderDomException("Test expression doesn't return a value!");
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }
    }

    class ForExpr : CompoundExpr
    {
        public override bool HasValue()
        {
            return false;
        }

        public override Value Value
        {
            get { throw new ShaderDomException("ForExprs have no value!"); }
        }

        DeclExpr Initializer;
        Expr Test;
        Expr Update;
        int Attributes;
        int UnrollDepth;

        public enum LoopAttributes
        {
            NO_ATTRIBUTE,
            UNROLL,
            LOOP,
            FAST_OPT,
            ALLOW_UAV_CONDITION
        }

        public ForExpr(DeclExpr initializer, Expr test, Expr update)
            : this(initializer, test, update, (int)LoopAttributes.NO_ATTRIBUTE, 0)
        {
        }

        public ForExpr(DeclExpr initializer, Expr test, Expr update, int attributes)
            : this(initializer, test, update, attributes, 0)
        {
        }

        public ForExpr(DeclExpr initializer, Expr test, Expr update, int attributes, int unrollDepth)
        {
            if (!test.HasValue())
                throw new ShaderDomException("Test expression doesn't return a value!");

            if (!(test.Value.ValueType is Type.BoolType))
                throw new ShaderDomException("Test expression does not return a boolean value!");

            Initializer = initializer;
            Test = test;
            Update = update;
            Attributes = attributes;
            UnrollDepth = unrollDepth;
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }
    }

    class CallExpr : Expr
    {
        public override bool HasValue()
        {
            return true;
        }

        public override Value Value
        {
            get { throw new NotImplementedException(); }
        }

        public CallExpr(Function fn, Expr[] parameters)
        {
            if (fn.Arity != parameters.Length)
                throw new ShaderDomException("Number of parameters doesn't match function arity!");
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }
    }

    class AssignmentExpr : Expr
    {
        Operator Modifier;
        Value AssignmentValue;

        public override Value Value
        {
            get { throw new ShaderDomException("SelfModifyingExprs have no value!"); }
        }

        public override bool HasValue()
        {
            throw new NotImplementedException();
        }

        public AssignmentExpr(Value v)
            : this(v, Operator.IDENTITY)
        {
        }

        public AssignmentExpr(Value value, Operator modifier)
        {
            if (modifier <= Operator.FIRST_MODIFIER_OPERATOR || modifier >= Operator.LAST_MODIFIER_OPERATOR)
                throw new ShaderDomException(string.Format("Operator {0} cannot be used to modify an assignment!", modifier));

            AssignmentValue = value;
            Modifier = modifier;
        }

        public override string ToString()
        {
            throw new NotImplementedException();
        }
    }

    class ReturnExpr : Expr
    {
        Expr ReturnValue;

        public override Value Value
        {
            get { return ReturnValue.Value; }
        }

        public override bool HasValue()
        {
            return false;
        }

        public ReturnExpr(Expr value)
        {
            ReturnValue = value;
        }

        public override string ToString()
        {
            StringBuilder SB = new StringBuilder();
            SB.AppendFormat("return {0};", ReturnValue.Value);

            return SB.ToString();
        }
    }
}
