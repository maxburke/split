using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hlsl
{
    class Test
    {
        public static bool RunTests()
        {
            FloatType f = new FloatType();
            VectorType float4 = new VectorType(f, 4);
            VectorType float3 = new VectorType(f, 3);
            VectorType float2 = new VectorType(f, 2);
            StructType vsData = new StructType("VertexShaderData", new StructField[] {
                new StructField(float4, "position", Semantic.POSITION),
                new StructField(float2, "surfaceUV", Semantic.TEXCOORD),
                new StructField(float2, "lightmapUV", Semantic.TEXCOORD),
                new StructField(float3, "normal", Semantic.NORMAL),
                new StructField(float4, "color", Semantic.COLOR)
            });

            UserDefinedFunction udf = new UserDefinedFunction("vs_main");
            Value argValue = udf.AddArgument(vsData);

            DeclExpr output = new DeclExpr(vsData, argValue);
            udf.AddExpr(output);
            udf.AddExpr(new ReturnExpr(output.Value));

            return false;
        }
    }
}
