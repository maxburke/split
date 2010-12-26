using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Hlsl
{
    class Test
    {
        public static Type CreateVSType(HlslProgram program)
        {
            Type f = program.Types.GetFloatType();
            Type float4 = program.Types.GetVectorType(f, 4);
            Type float3 = program.Types.GetVectorType(f, 3);
            Type float2 = program.Types.GetVectorType(f, 2);
            Type vsData = program.Types.GetStructType("vs_input", new StructField[] {
                    new StructField(float4, "position", new Semantic(Semantic.SemanticType.POSITION)),
                    new StructField(float2, "surfaceUV", new Semantic(Semantic.SemanticType.TEXCOORD, 0)),
                    new StructField(float2, "lightmapUV", new Semantic(Semantic.SemanticType.TEXCOORD, 1)),
                    new StructField(float3, "normal", new Semantic(Semantic.SemanticType.NORMAL)),
                    new StructField(float4, "color", new Semantic(Semantic.SemanticType.COLOR))
                });

            return vsData;
        }

        public static string EmptyProgramTest()
        {
            try
            {
                using (HlslProgram program = new HlslProgram())
                {
                    return program.ToString();
                }
            }
            catch (ShaderDomException)
            {
            }

            return null;
        }


        public static string ArgAssignedToOutputTest()
        {
            using (HlslProgram program = new HlslProgram())
            {
                Type vsData = CreateVSType(program);

                UserDefinedFunction udf = new UserDefinedFunction("vs_main");
                Value argValue = udf.AddArgument(vsData);

                DeclExpr output = new DeclExpr(vsData, argValue);
                udf.AddExpr(output);
                udf.AddExpr(new ReturnExpr(output));

                program.SetShader(ShaderType.VertexShader, udf, ShaderProfile.vs_3_0);
                program.SetShader(ShaderType.PixelShader, udf, ShaderProfile.ps_3_0);
                return program.ToString();
            }
        }

        public static string SimpleStructMemberTest()
        {
            using (HlslProgram program = new HlslProgram())
            {
                Type vsData = CreateVSType(program);

                UserDefinedFunction udf = new UserDefinedFunction("vs_main");
                Value argValue = udf.AddArgument(vsData);

                DeclExpr output = new DeclExpr(vsData);
                udf.AddExpr(output);
                udf.AddExpr(new AssignmentExpr(
                    new StructMemberExpr(output.Value, "position").Value, 
                    new StructMemberExpr(argValue, "position").Value));

                udf.AddExpr(new ReturnExpr(output));
                program.SetShader(ShaderType.VertexShader, udf, ShaderProfile.vs_3_0);
                program.SetShader(ShaderType.PixelShader, udf, ShaderProfile.ps_3_0);

                return program.ToString();
            }
        }

        public static string SimpleFunctionCallTest()
        {
            using (HlslProgram program = new HlslProgram())
            {
                Type vsData = CreateVSType(program);
                Type f1 = program.Types.GetFloatType();
                Type f4 = program.Types.GetVectorType(f1, 4);

                DeclExpr wvpMatrixDecl = new DeclExpr(program.Types.GetMatrixType(f4, 4));
                program.AddGlobal(wvpMatrixDecl);

                UserDefinedFunction udf = new UserDefinedFunction("vs_main");
                Value argValue = udf.AddArgument(vsData);

                Function fn = program.GetFunctionByName("mul");
                udf.AddExpr(new CallExpr(...));

                DeclExpr output = new DeclExpr(vsData);
                udf.AddExpr(output);
                udf.AddExpr(new AssignmentExpr(
                    new StructMemberExpr(output.Value, "position").Value,
                    new StructMemberExpr(argValue, "position").Value));

                udf.AddExpr(new ReturnExpr(output));
                program.SetShader(ShaderType.VertexShader, udf, ShaderProfile.vs_3_0);
                program.SetShader(ShaderType.PixelShader, udf, ShaderProfile.ps_3_0);

                return program.ToString();
            }
        }

        public static bool RunTests()
        {
            string test1 = EmptyProgramTest();
            string test2 = ArgAssignedToOutputTest();
            string test3 = SimpleStructMemberTest();
            string test4 = SimpleFunctionCallTest();

            return false;
        }
    }
}
