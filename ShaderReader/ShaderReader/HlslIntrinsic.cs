using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hlsl
{
    #region OutputTypeMatchesInput Functions

    class OutputTypeMatchesInputFunction : Function
    {
        public OutputTypeMatchesInputFunction(string name)
            : base(name)
        { }

        public override bool IsValidCall(Value[] args)
        {
            throw new NotImplementedException();
        }

        public static Function[] CreateOutputTypeMatchesInputFunctions()
        {
            string[] fnNames = new string[] {
                "abs",
                "acos",
                "all",
                "any",
                "asin",
                "atan",
                "ceil",
                "clamp",
                "cos",
                "cosh",
                "ddx",
                "ddy",
                "degrees",
                "exp",
                "exp2",
                "floor",
                "fmod",
                "fwidth",
                "log",
                "log10",
                "log2",
                "radians",
                "rsqrt",
                "saturate",
                "sign",
                "sin",
                "sinh",
                "sqrt",
                "tan",
                "tanh",
                "trunc"
            };
            
            Function[] fns = new Function[fnNames.Length];

            for (int i = 0; i < fnNames.Length; ++i)
                fns[i] = new OutputTypeMatchesInputFunction(fnNames[i]);

            return fns;
        }
    }

    #endregion

    #region Clip
    class Clip : Function
    {
        public Clip() : base("clip") 
        {
            throw new NotImplementedException();
        }

        public override bool IsValidCall(Value[] args)
        {
            throw new NotImplementedException();
        }
    }
    #endregion

    #region Miscellaneous

    class Atan2 : Function
    { }

    class Determinant : Function
    { }

    class Distance : Function
    { }

    class Dot : Function
    { }

    class Faceforward : Function
    { }

    class Frexp : Function
    { }

    class Ldexp : Function
    { }

    class Length : Function
    { }

    class Lerp : Function
    { }

    class Lit : Function
    { }

    class Max : Function
    { }

    class Min : Function
    { }

    class Noise : Function
    { }

    class Normalize : Function
    { }

    class Pow : Function
    { }

    class Reflect : Function
    { }

    class Refract : Function
    { }

    class Sincos : Function
    { }

    class Smoothstep : Function
    { }

    class Trunc : Function
    { }

    #endregion

    #region Predicates
    class PredicateFunction : Function
    {
        public PredicateFunction(string name)
            : base(name)
        { }

        public override bool IsValidCall(Value[] args)
        {
            throw new NotImplementedException();
        }
    }

    class Isfinite : PredicateFunction
    {
        public Isfinite() : base("isfinite") { }
    }

    class Isinf : PredicateFunction
    {
        public Isinf() : base("isinf") { }
    }

    class Isnan : PredicateFunction
    {
        public Isnan() : base("isnan") { }
    }

    #endregion
}
