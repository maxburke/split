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
            Type f = Type.GetFloatType();
            Type float4 = Type.GetVectorType(f, 4);
            Type float3 = Type.GetVectorType(f, 3);
            Type float2 = Type.GetVectorType(f, 2);
            Type vsData = Type.GetStructType("vs_input", new StructField[] {
                new StructField(float4, "position", new Semantic(Semantic.SemanticType.POSITION)),
                new StructField(float2, "surfaceUV", new Semantic(Semantic.SemanticType.TEXCOORD, 0)),
                new StructField(float2, "lightmapUV", new Semantic(Semantic.SemanticType.TEXCOORD, 1)),
                new StructField(float3, "normal", new Semantic(Semantic.SemanticType.NORMAL)),
                new StructField(float4, "color", new Semantic(Semantic.SemanticType.COLOR))
            });

            UserDefinedFunction udf = new UserDefinedFunction("vs_main");
            Value argValue = udf.AddArgument(vsData);

            DeclExpr output = new DeclExpr(vsData, argValue);
            udf.AddExpr(output);
            udf.AddExpr(new ReturnExpr(output.Value));

            HlslProgram program = new HlslProgram();
            program.SetShader(ShaderType.VertexShader, udf, ShaderProfile.vs_3_0);
            program.SetShader(ShaderType.PixelShader, udf, ShaderProfile.ps_3_0);

            string shaderCode = program.ToString();

            return false;
        }
    }
}
