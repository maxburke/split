using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

    public enum ShaderType
    {
        VertexShader,
        PixelShader,
        NUM_SHADER_TYPES
    }

    public enum ShaderProfile
    {
        vs_1_1,
        ps_2_0, ps_2_x, vs_2_0, vs_2_x,
        ps_3_0, vs_3_0
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

            if (!(test.Value.ValueType is Type.BoolType))
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
            get { return ReturnValue; }
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
        public readonly string Name;

        public Function(string name)
        {
            Name = name;
        }

        public abstract int Arity { get; }
    }

    class UserDefinedFunction : Function
    {
        List<Expr> Expressions = new List<Expr>();
        List<Pair<Value, Semantic>> Arguments = new List<Pair<Value, Semantic>>();

        public override int Arity
        {
            get { return Arguments.Count; }
        }

        public UserDefinedFunction(string name)
            : base(name)
        {
        }

        public Value AddArgument(Type structType)
        {
            if (!(structType is Type.StructType))
                throw new ShaderDomException("Argument must be a struct type or have a semantic!");

            Value v = new Value(structType, string.Format("arg{0}", Arguments.Count));
            Arguments.Add(new Pair<Value, Semantic>(v, new Semantic(Semantic.SemanticType.NONE)));

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
                    else if (returnType != RE.Value.ValueType)
                        throw new ShaderDomException("Function cannot return different types");
                }
            }

            if (returnType == null)
                throw new ShaderDomException("Function has no return type!");

            return returnType;
        }

        public override string ToString()
        {
            StringBuilder SB = new StringBuilder();
            
            Type returnType = DetermineReturnType();

            /// TODO: Add support for return value sematics.
            SB.AppendFormat("{0} {1}(", returnType.TypeName(), Name);

            for (int i = 0; i < Arguments.Count; ++i)
            {
                string separator = i < Arguments.Count - 1 ? "," : "";

                Semantic none = new Semantic(Semantic.SemanticType.NONE);
                if (Arguments[i].second != none)
                    SB.AppendFormat("{0} {1} : {2}{3}", Arguments[i].first.ValueType.TypeName(), Arguments[i].first.Name, Arguments[i].second, separator);
                else
                    SB.AppendFormat("{0} {1}{2}", Arguments[i].first.ValueType.TypeName(), Arguments[i].first.Name, separator);
            }

            SB.AppendLine(") {");

            foreach (Expr E in Expressions)
                SB.AppendLine("    " + E.ToString());

            SB.AppendLine("}");

            return SB.ToString();
        }
    }

    class BuiltInFunction : Function
    {
        Type[] ArgTypes;
        Type ReturnType;

        public BuiltInFunction(string name, Type returnType, Type[] argTypes)
            : base(name)
        {
            ArgTypes = argTypes;
            ReturnType = returnType;
        }

        public override int Arity
        {
            get { return ArgTypes.Length; }
        }
    }

    class HlslProgram
    {
        List<Value> Globals = new List<Value>();
        Dictionary<Function, bool> Functions = new Dictionary<Function, bool>();
        Pair<ShaderProfile, Function>[] Shaders = new Pair<ShaderProfile, Function>[(int)ShaderType.NUM_SHADER_TYPES];

        public HlslProgram()
        {
            // Populate built-in-functions here
        }

        public List<Function> GetFunctionsByName(string name)
        {
            List<Function> fns = new List<Function>();

            foreach (KeyValuePair<Function, bool> kvp in Functions)
                if (kvp.Key.Name == name)
                    fns.Add(kvp.Key);

            return fns;
        }

        public void AddFunction(UserDefinedFunction function)
        {
            if (!Functions.ContainsKey(function))
                Functions.Add(function, true);
        }

        public void SetShader(ShaderType type, UserDefinedFunction function, ShaderProfile profile)
        {
            AddFunction(function);
            Shaders[(int)type] = new Pair<ShaderProfile, Function>(profile, function);
        }

        public override string ToString()
        {
            StringBuilder SB = new StringBuilder();

            ReadOnlyCollection<Type.StructType> structTypes = Type.GetAllStructTypes();

            foreach (Type.StructType ST in structTypes)
                SB.AppendLine(ST.ToString());

            foreach (Value V in Globals)
                SB.AppendLine(V.ToString());

            foreach (KeyValuePair<Function, bool> kvp in Functions)
                SB.AppendLine(kvp.Key.ToString());

            SB.AppendLine("technique defaultTechnique {");
            SB.AppendLine("    pass P0 {");

            for (int i = 0; i < (int)ShaderType.NUM_SHADER_TYPES; ++i)
                SB.AppendFormat("        {0} = compile {1} {2}();{3}",
                    Enum.GetName(typeof(ShaderType), (object)i),
                    Shaders[i].first,
                    Shaders[i].second.Name,
                    System.Environment.NewLine);

            SB.AppendLine("    }");
            SB.AppendLine("}");

            return SB.ToString();
        }
    }
}
