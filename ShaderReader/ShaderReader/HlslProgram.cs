using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace Hlsl
{
    struct Pair<T, U>
    {
        public T first;
        public U second;

        public Pair(T _first, U _second)
        {
            first = _first;
            second = _second;
        }
    }

    class ShaderDomException : Exception
    {
        public new readonly string Message;

        public ShaderDomException(string message)
        {
            Message = message;
        }
    }

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

            if (!(test.Value.ValueType is BoolType))
                throw new ShaderDomException("Test expression does not return a boolean value!");

            Initializer = initializer;
            Test = test;
            Update = update;
            Attributes = attributes;
            UnrollDepth = unrollDepth;
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
    }

    class ReturnExpr : Expr
    {
        Value ReturnValue;

        public override Value Value
        {
            get { throw new ShaderDomException("ReturnExprs have no value!"); }
        }

        public override bool HasValue()
        {
            return false;
        }

        public ReturnExpr(Value value)
        {
            ReturnValue = value;
        }
    }

    abstract class Function
    {
        public abstract int Arity { get; }
    }

    class UserDefinedFunction : Function
    {
        string Name;
        List<Expr> Expressions = new List<Expr>();
        List<Pair<Value, Semantic>> Arguments = new List<Pair<Value, Semantic>>();

        public override int Arity
        {
            get { return Arguments.Count; }
        }

        public UserDefinedFunction(string name)
        {
            Name = name;
        }

        public Value AddArgument(StructType structType)
        {
            Value v = new Value(structType, string.Format("arg{0}", Arguments.Count));
            Arguments.Add(new Pair<Value, Semantic>(v, Semantic.NONE));

            return v;
        }

        public Value AddArgument(Type argType, Semantic semantic)
        {
            Value v = new Value(argType, string.Format("arg{0}", Arguments.Count));
            Arguments.Add(new Pair<Value, Semantic>(v, semantic));

            return v;
        }

        public void AddExpr(Expr e)
        {
            Expressions.Add(e);
        }

        public Value GetArgValue(int i)
        {
            return Arguments[i].first;
        }

        Type DetermineReturnType()
        {
            Type returnType = null;

            foreach (Expr E in Expressions)
            {
                ReturnExpr RE = E as ReturnExpr;
                if (RE != null)
                {
                    if (returnType == null)
                        returnType = RE.Value.ValueType;
                    else if (returnType != RE.Value.ValueType;
                }
            }

            if (returnType == null)
                throw new ShaderDomException("Function has no return type!");

            return returnType;
        }

        public override string ToString()
        {

            return null;
        }
    }

    class BuiltInFunction : Function
    {
        Type[] ArgTypes;
        Type ReturnType;

        public BuiltInFunction(Type returnType, Type[] argTypes)
        {
            ArgTypes = argTypes;
            ReturnType = returnType;
        }

        public override int Arity
        {
            get { return ArgTypes.Length; }
        }
    }
}
