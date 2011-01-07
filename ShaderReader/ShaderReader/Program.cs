using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Hlsl;
using Hlsl.Expressions;

namespace ShaderReader
{
    public enum CullMode
    {
        Front,
        Back,
        None
    }

    public enum Flag
    {
        NOPICMIP,
        NOMIPMAP,
    }

    public enum BlendMode
    {
        INVALID,
        GL_ONE,
        GL_ZERO,
        GL_DST_COLOR,
        GL_ONE_MINUS_DST_COLOR,
        GL_SRC_COLOR,
        GL_ONE_MINUS_SRC_COLOR,
        GL_DST_ALPHA,
        GL_ONE_MINUS_DST_ALPHA,
        GL_SRC_ALPHA,
        GL_ONE_MINUS_SRC_ALPHA,
    }

    class Shader : IDisposable
    {
        string mName;
        public CullMode mCullMode = CullMode.Front;

        public List<string> mTextures = new List<string>();
        public HlslProgram mProgram = new HlslProgram();
        public UserDefinedFunction mVertexShader = new UserDefinedFunction("vs_main");
        public UserDefinedFunction mPixelShader = new UserDefinedFunction("ps_main");

        public readonly DeclExpr mWorld;
        public readonly DeclExpr mViewProjection;
        public readonly DeclExpr mTime;

        public Expr mVsOutput;
        public Expr mVsPositionExpr;

        public Expr mPsAccumulatedColor;

        public readonly Value mVSInput;
        public readonly Value mPSInput;

        public uint mFlags;

        public void SetFlag(Flag f)
        {
            mFlags = mFlags | (uint)f;
        }

        public Shader(string name)
        {
            mName = name;

            Hlsl.Type f = TypeRegistry.GetFloatType();
            Hlsl.Type f2 = TypeRegistry.GetVectorType(f, 2);
            Hlsl.Type f3 = TypeRegistry.GetVectorType(f, 3);
            Hlsl.Type f4 = TypeRegistry.GetVectorType(f, 4);

            Hlsl.Type vsData = mProgram.Types.GetStructType("vs_data", new StructField[] {
                    new StructField(f4, "position", new Semantic(Semantic.SemanticType.POSITION)),
                    new StructField(f2, "surfaceST", new Semantic(Semantic.SemanticType.TEXCOORD, 0)),
                    new StructField(f2, "lightmapST", new Semantic(Semantic.SemanticType.TEXCOORD, 1)),
                    new StructField(f3, "normal", new Semantic(Semantic.SemanticType.NORMAL)),
                    new StructField(f4, "color", new Semantic(Semantic.SemanticType.COLOR))
                });

            Function sin = mProgram.GetFunctionByName("sin");
            Function asin = mProgram.GetFunctionByName("asin");
            Function sign = mProgram.GetFunctionByName("sign");
            Function floor = mProgram.GetFunctionByName("floor");
            Function abs = mProgram.GetFunctionByName("abs");
            Expr PI = new LiteralExpr(f, 3.1415926535f);
            Expr TwoPI = new LiteralExpr(f, 2 * 3.1415926535f);

            mWorld = new DeclExpr(TypeRegistry.GetMatrixType(f4, 4), "gWorld");
            mProgram.AddGlobal(mWorld);

            mViewProjection = new DeclExpr(TypeRegistry.GetMatrixType(f4, 4), "gViewProjection");
            mProgram.AddGlobal(mViewProjection);

            mTime = new DeclExpr(f, "gTime");
            mProgram.AddGlobal(mTime);

            #region SinWave
            {
                UserDefinedFunction sinWave = new UserDefinedFunction("sinwave");
                Value t = sinWave.AddArgument(f, "t");
                Value t0 = sinWave.AddArgument(f, "t0");
                Value amp = sinWave.AddArgument(f, "amp");
                Value phase = sinWave.AddArgument(f, "phase");
                Value freq = sinWave.AddArgument(f, "freq");

                Expr t1 = new BinaryExpr(t, TwoPI.Value, OpCode.MUL);
                Expr sinParam = new BinaryExpr(new BinaryExpr(t1.Value, freq, OpCode.MUL).Value, phase, OpCode.ADD);
                Expr sinExpr = new CallExpr(sin, new Expr[] { sinParam });
                sinWave.AddExpr(sinExpr);

                Expr returnExpr = new ReturnExpr(
                    new BinaryExpr(
                        new BinaryExpr(sinExpr.Value, amp, OpCode.MUL).Value,
                        t0, OpCode.ADD));

                sinWave.AddExpr(returnExpr);
                mProgram.AddFunction(sinWave);
            }
            #endregion

            #region SquareWave
            {
                UserDefinedFunction squareWave = new UserDefinedFunction("squarewave");
                Value t = squareWave.AddArgument(f, "t");
                Value t0 = squareWave.AddArgument(f, "t0");
                Value amp = squareWave.AddArgument(f, "amp");
                Value phase = squareWave.AddArgument(f, "phase");
                Value freq = squareWave.AddArgument(f, "freq");

                Expr t1 = new BinaryExpr(t, TwoPI.Value, OpCode.MUL);
                Expr sinParam = new BinaryExpr(new BinaryExpr(t1.Value, freq, OpCode.MUL).Value, phase, OpCode.ADD);
                Expr sinExpr = new CallExpr(sin, new Expr[] { sinParam });
                squareWave.AddExpr(sinExpr);

                Expr signExpr = new CallExpr(sign, new Expr[] { sinExpr });
                squareWave.AddExpr(signExpr);

                Expr returnExpr = new ReturnExpr(
                    new BinaryExpr(new BinaryExpr(signExpr.Value, amp, OpCode.MUL).Value,
                        t0, OpCode.ADD));
                squareWave.AddExpr(returnExpr);
                mProgram.AddFunction(squareWave);
            }
            #endregion

            #region TriangleWave
            {
                UserDefinedFunction triangleWave = new UserDefinedFunction("trianglewave");
                Value t = triangleWave.AddArgument(f, "t");
                Value t0 = triangleWave.AddArgument(f, "t0");
                Value amp = triangleWave.AddArgument(f, "amp");
                Value phase = triangleWave.AddArgument(f, "phase");
                Value freq = triangleWave.AddArgument(f, "freq");

                Expr t1 = new BinaryExpr(t, PI.Value, OpCode.MUL);
                Expr twoOverPi = new BinaryExpr(new LiteralExpr(f, 2.0f).Value, PI.Value, OpCode.DIV);

                Expr sinExpr = new CallExpr(sin, new Expr[] { t1 });
                triangleWave.AddExpr(sinExpr);

                Expr asinExpr = new CallExpr(asin, new Expr[] { sinExpr });
                triangleWave.AddExpr(asinExpr);

                Expr absExpr = new CallExpr(abs, new Expr[] { 
                    new BinaryExpr(twoOverPi.Value, asinExpr.Value, OpCode.MUL) });
                triangleWave.AddExpr(absExpr);

                triangleWave.AddExpr(new ReturnExpr(absExpr));
                mProgram.AddFunction(triangleWave);
            }
            #endregion

            #region SawtoothWave
            {
                UserDefinedFunction sawtoothWave = new UserDefinedFunction("sawtoothwave");
                Value t = sawtoothWave.AddArgument(f, "t");
                Value t0 = sawtoothWave.AddArgument(f, "t0");
                Value amp = sawtoothWave.AddArgument(f, "amp");
                Value phase = sawtoothWave.AddArgument(f, "phase");
                Value freq = sawtoothWave.AddArgument(f, "freq");

                DeclExpr t1 = new DeclExpr(new BinaryExpr(new BinaryExpr(t, freq, OpCode.MUL).Value, phase, OpCode.ADD), "t1");
                t1.SetConst(true);
                sawtoothWave.AddExpr(t1);

                Expr t1Floor = new CallExpr(floor, new Expr[] { t1 });
                sawtoothWave.AddExpr(t1Floor);
                Expr returnExpr = new ReturnExpr(
                    new BinaryExpr(
                        new BinaryExpr(
                            new BinaryExpr(t1.Value, t1Floor.Value, OpCode.SUB).Value, amp, OpCode.MUL).Value
                        , t0, OpCode.ADD));
                sawtoothWave.AddExpr(returnExpr);
                mProgram.AddFunction(sawtoothWave);
            }
            #endregion

            #region InverseSawtoothWave
            {
                UserDefinedFunction inverseSawtooth = new UserDefinedFunction("inversesawtoothwave");
                Value t = inverseSawtooth.AddArgument(f, "t");
                Value t0 = inverseSawtooth.AddArgument(f, "t0");
                Value amp = inverseSawtooth.AddArgument(f, "amp");
                Value phase = inverseSawtooth.AddArgument(f, "phase");
                Value freq = inverseSawtooth.AddArgument(f, "freq");

                Function sawtooth = mProgram.GetFunctionByName("sawtoothwave");
                Expr callExpr = new CallExpr(sawtooth, new Value[] { t, t0, amp, phase, freq });
                inverseSawtooth.AddExpr(callExpr);

                Expr returnExpr = new ReturnExpr(
                    new BinaryExpr(amp, callExpr.Value, OpCode.SUB));
                inverseSawtooth.AddExpr(returnExpr);
                mProgram.AddFunction(inverseSawtooth);
            }
            #endregion

            #region General setup and global declaration.
            #endregion

            #region Vertex shader setup
            {
                mVSInput = mVertexShader.AddArgument(vsData, "input");

                mVsOutput = new DeclExpr(vsData, "output");
                mVertexShader.AddExpr(mVsOutput);

                mVsPositionExpr = new StructMemberExpr(mVSInput, 0);

                // Pass through all non-position members of the vertex shader
                StructMemberExpr[] otherMembers = new StructMemberExpr[] {
                    new StructMemberExpr(mVsOutput.Value, 1),
                    new StructMemberExpr(mVsOutput.Value, 2),
                    new StructMemberExpr(mVsOutput.Value, 3),
                    new StructMemberExpr(mVsOutput.Value, 4),
                };

                foreach (StructMemberExpr SME in otherMembers)
                    mVertexShader.AddExpr(new AssignmentExpr(SME.Value, new LiteralExpr(SME.Value.ValueType).Value));
            }
            #endregion

            #region Pixel shader setup
            {
                mPSInput = mPixelShader.AddArgument(vsData, "input");
                mPsAccumulatedColor = new LiteralExpr(f4, 0.0f, 0.0f, 0.0f, 0.0f);
            }
            #endregion
        }

        public Value AddTexture(string texture)
        {
            mTextures.Add(texture);
            DeclExpr samplerDeclExpr = new DeclExpr(TypeRegistry.GetSamplerType());
            mProgram.AddGlobal(samplerDeclExpr);

            return samplerDeclExpr.Value;
        }

        public void FinalizeShader()
        {
            #region Finalize vertex shader
            {
                // Transform the position after the shader code below has first applied any particular 
                // model-space deformations.
                Expr worldViewProjExpr = new BinaryExpr(mWorld.Value, mViewProjection.Value, OpCode.MUL);
                Function mul = mProgram.GetFunctionByName("mul");

                Expr transformedPos = new CallExpr(mul, new Expr[] { mVsPositionExpr, worldViewProjExpr});
                mVertexShader.AddExpr(new AssignmentExpr(new StructMemberExpr(mVsOutput.Value, 0).Value, transformedPos.Value));
                mVertexShader.AddExpr(new ReturnExpr(mVsOutput));

                mProgram.SetShader(ShaderType.VertexShader, mVertexShader, Hlsl.ShaderProfile.vs_3_0);
            }
            #endregion

            #region Finalize pixel shader
            {
                mPixelShader.AddExpr(new ReturnExpr(mPsAccumulatedColor));
                mProgram.SetShader(ShaderType.PixelShader, mPixelShader, Hlsl.ShaderProfile.ps_3_0);
            }
            #endregion
        }

        public override string ToString()
        {
            return mName;
        }

        public void Dispose()
        {
            mProgram.Dispose();
            mProgram = null;
        }
    }

    class ShaderParser
    {
        TokenParser[] mGeneralTokenParsers;
        TokenParser[] mShaderStageTokenParsers;
        string mStashedToken;
        Shader mShader;
        string mContent;
        string mCurrentEffect;
        int I;
        int E;

        public string GetHlsl()
        {
            return mShader.mProgram.EmitEffect();
        }

        class TokenParser
        {
            public readonly string Token;
            public delegate void ParseFunction();
            public readonly ParseFunction Function;

            public TokenParser(string token, ParseFunction function)
            {
                Token = token;
                Function = function;
            }
        }

        void PassAndContinue()
        {
        }

        void DiscardOneTokenAndContinue()
        {
            // This method is a placeholder for shader functionality 
            // that is handled by the q3map pipeline
            NextToken();
        }

        void DiscardTwoTokensAndContinue()
        {
            // This method is a placeholder for shader functionality 
            // that is handled by the q3map pipeline
            NextToken();
            NextToken();
        }

        #region General parsers

        void Cull() 
        {
            string cullMode = NextTokenLowerCase();

            switch (cullMode)
            {
                case "front":
                    mShader.mCullMode = CullMode.Front;
                    break;
                case "back":
                    mShader.mCullMode = CullMode.Back;
                    break;
                case "disable":
                case "none":
                case "twosided":
                    mShader.mCullMode = CullMode.None;
                    break;
            }
        }

        void SkyParms()
        {
            string farBox = NextToken();
            string cloudHeight = NextToken();
            string nearbox = NextToken();

            // TODO: wtf?
        }

        void DeformVertexes()
        {
            string method = NextTokenLowerCase();

            switch (method)
            {
                case "wave":
                    {
                        float div = float.Parse(NextToken());
                        string func = NextTokenLowerCase() + "wave";
                        float baseVal = float.Parse(NextToken());
                        float ampVal = float.Parse(NextToken());
                        float phaseVal = float.Parse(NextToken());
                        float freqVal = float.Parse(NextToken());

                        Hlsl.Type floatType = TypeRegistry.GetFloatType();
                        Function fn = mShader.mProgram.GetFunctionByName(func);
                        Expr deformFnCall = new CallExpr(fn, new Expr[] {
                            mShader.mTime,
                            new LiteralExpr(floatType, baseVal),
                            new LiteralExpr(floatType, ampVal),
                            new LiteralExpr(floatType, phaseVal),
                            new LiteralExpr(floatType, freqVal)
                        });

                        Function mul = mShader.mProgram.GetFunctionByName("mul");
                        Expr mulExpr = new CallExpr(mul, new Expr[] {
                            new BinaryExpr(deformFnCall.Value, new LiteralExpr(floatType, div).Value, OpCode.MUL),
                            new LiteralExpr(TypeRegistry.GetVectorType(floatType, 4), new StructMemberExpr(mShader.mVSInput, "normal").Value, 1),
                        });

                        Expr modifiedPosition = new BinaryExpr(
                            mulExpr.Value,
                            mShader.mVsPositionExpr.Value, 
                            OpCode.ADD);
                        DeclExpr e = new DeclExpr(modifiedPosition);
                        e.SetConst(true);
                        mShader.mVertexShader.AddExpr(e);
                        mShader.mVsPositionExpr = e;
                    }
                    break;
                case "normal":
                    {
                        string ampVal = NextToken();
                        string freqVal = NextToken();
                    }
                    break;
                case "bulge":
                    {
                        string width = NextToken();
                        string height = NextToken();
                        string speed = NextToken();
                    }
                    break;
                case "move":
                    {
                        string x = NextToken();
                        string y = NextToken();
                        string z = NextToken();
                        string func = NextTokenLowerCase();
                        string baseVal = NextToken();
                        string ampVal = NextToken();
                        string phaseVal = NextToken();
                        string freqVal = NextToken();

                        //mShader.mShaderBuilder.AddVSLine(
                        //    string.Format("float4 deformVertexesMovePosition = float4({0}, {1}, {2}, 1) * {3}({4}, {5}, {6}, {7}) + {8};",
                        //    x, y, z, func, baseVal, ampVal, phaseVal, freqVal, mShader.mShaderBuilder.LastVSValue),
                        //    "deformVertexesMovePosition");
                    }
                    break;
                case "autosprite":
                case "autosprite2":
                case "projectionshadow":
                    break;
                default:
                    if (method.StartsWith("text"))
                    {
                    }
                    break;
            }
        }

        void FogParms() 
        {
            string r = NextToken();
            if (r == "(")
                r = NextToken();
            string g = NextToken();
            string b = NextToken();

            string distanceToOpaque = NextToken();
            if (distanceToOpaque == ")")
                distanceToOpaque = NextToken();

            // Discard unused parameters.
//            string token = NextToken();
//            if (IsKeyword(mGeneralTokenParsers, token))
//                PushToken(token);

            // TODO: fill in fog stuff later.
        }

        void Nopicmip() 
        {
            mShader.SetFlag(Flag.NOPICMIP);
        }

        void Nomipmap() 
        {
            mShader.SetFlag(Flag.NOMIPMAP);
        }

        void PolygonOffset()
        {
            // TODO: add polygonOffset handling code here.
        }

        void Portal() 
        {
            // TODO: add portal handling code here.
        }

        void Sort()
        {
            string sortMethod = NextToken();
            // TODO: add sort handling code here. Link this to the 
            // object sort key that we generate, perhaps?
        }

        void Q3mapSun() 
        {
            string r = NextToken();
            string g = NextToken();
            string b = NextToken();
            string intensity = NextToken();
            string direction = NextToken();
            string elevation = NextToken();

            // TODO: Add sun code here.
        }

        void Q3mapSurfaceLight() 
        {
            string lightValue = NextToken();
        }

        void QerNoCarve()
        {
            // Editor only, discard.
        }

        void FogOnly()
        {
            // TODO: uh, dunno?
        }

        void EntityMergable()
        {
            // TODO: ditto, dunno.
        }

        void CloudParms()
        {
            // TODO: not sure here either.
        }

        void Lightning()
        {
            // TODO: Not sure.
        }

        void Sky()
        {
            // TODO: ???
        }

        #endregion

        #region Shader stage parsers
        void Map() 
        {
            string textureMap = NextToken();
            mShader.mTextures.Add(textureMap);

            bool isLightmap = textureMap.ToLower() == "$lightmap";
            //string sampler = mShader.mShaderBuilder.AddSampler(isLightmap);
        }

        void ClampMap() 
        {
            string textureMap = NextToken();
            mShader.mTextures.Add(textureMap);
            bool isLightmap = textureMap.ToLower() == "$lightmap";
            //string sampler = mShader.mShaderBuilder.AddSampler(isLightmap);

            // TODO: add more texture setup code here.
        }

        void AnimMap() 
        {
            string frequency = NextToken();

            List<string> frames = new List<string>();

            for (; ; )
            {
                string token = NextToken();

                if (IsKeyword(mShaderStageTokenParsers, token))
                {
                    PushToken(token);
                    break;
                }

                mShader.mTextures.Add(token);
            }

            // Todo: Implement this.
        }

        void BlendFunc() 
        {
            string blendFunction = NextTokenLowerCase();
            BlendMode sourceBlend = BlendMode.INVALID;
            BlendMode destBlend = BlendMode.INVALID;
            bool parsed = false;

            switch (blendFunction)
            {
                case "add":
                    sourceBlend = BlendMode.GL_ONE;
                    destBlend = BlendMode.GL_ONE;
                    parsed = true;
                    break;
                case "blend":
                    sourceBlend = BlendMode.GL_SRC_ALPHA;
                    destBlend = BlendMode.GL_ONE_MINUS_SRC_ALPHA;
                    parsed = true;
                    break;
                case "filter":
                    sourceBlend = BlendMode.GL_DST_COLOR;
                    destBlend = BlendMode.GL_ZERO;
                    parsed = true;
                    break;
                default:
                    break;
            }

            if (parsed)
                return;

            string sourceBlendToken = blendFunction.ToUpper();
            string destBlendToken = NextToken().ToUpper();
            sourceBlend = (BlendMode)Enum.Parse(typeof(BlendMode), sourceBlendToken);
            destBlend = (BlendMode)Enum.Parse(typeof(BlendMode), destBlendToken);

            /// TODO: Add blend-handling code here.
        }

        void RgbGen() 
        {
            string function = NextTokenLowerCase();
            switch (function)
            {
                case "identitylighting":
                case "identity":
                case "entity":
                case "oneminusentity":
                case "vertex":
                case "oneminusvertex":
                case "lightingdiffuse":
                    break;
                case "wave":
                    {
                        string func = NextTokenLowerCase();
                        string baseVal = NextToken();
                        string ampVal = NextToken();
                        string phaseVal = NextToken();
                        string freq = NextToken();
                    }
                    break;
            }

            /// TODO: Add rgbgen handling code here.
        }

        void AlphaGen() 
        {
            string function = NextTokenLowerCase();
            switch (function)
            {
                case "identitylighting":
                case "identity":
                case "entity":
                case "oneminusentity":
                case "vertex":
                case "oneminusvertex":
                case "lightingspecular":
                    break;
                case "portal":
                    {
                        string portalRadius = NextToken();
                    }
                    break;
                case "wave":
                    {
                        string func = NextTokenLowerCase();
                        string baseVal = NextToken();
                        string ampVal = NextToken();
                        string phaseVal = NextToken();
                        string freq = NextToken();
                    }
                    break;
            }

            /// TODO: Add alphagen handling code here.
        }

        void TcGen() 
        {
            string function = NextTokenLowerCase();
            switch (function)
            {
                case "base":
                case "lightmap":
                case "environment":
                    break;
            }

            // TODO: add tcgen handling code here
        }

        void TcMod() 
        {
            string function = NextTokenLowerCase();

            switch (function)
            {
                case "rotate":
                    {
                        string degressPerSecond = NextToken();
                    }
                    break;
                case "scale":
                    {
                        string sScale = NextToken();
                        string tScale = NextToken();
                    }
                    break;
                case "scroll":
                    {
                        string sSpeed = NextToken();
                        string tSpeed = NextToken();
                    }
                    break;
                case "stretch":
                    {
                        string func = NextTokenLowerCase();
                        string baseVal = NextToken();
                        string ampVal = NextToken();
                        string phaseVal = NextToken();
                        string freq = NextToken();
                    }
                    break;
                case "transform":
                    {
                        string m00 = NextToken();
                        string m01 = NextToken();
                        string m10 = NextToken();
                        string M11 = NextToken();
                        string t0 = NextToken();
                        string t1 = NextToken();
                    }
                    break;
                case "turb":
                    {
                        string baseVal = NextToken();

                        // Occasionally a "turb sin" declaration sneaks in.
                        // Discard, for now. 
                        // TODO: fix this.
                        if (baseVal == "sin")
                            baseVal = NextToken();

                        string ampVal = NextToken();
                        string phaseVal = NextToken();
                        string freq = NextToken();
                    }
                    break;
            }

            /// TODO: add tcmod handling code here.
        }

        void DepthFunc() 
        {
            string function = NextTokenLowerCase();
            Debug.Assert(function == "equal" || function == "lequal");

            // TODO: add depth function handling code here.
        }

        void DepthWrite() 
        {
            // NOTE: transparent surfaces do not write to the depth buffer generally.
            // This causes a depth buffer write to occur. Record this information in
            // the shader structure above.

            // TODO: implement this.
        }

        void AlphaFunc() 
        {
            string function = NextTokenLowerCase();

            Debug.Assert(function == "gt0" || function == "lt128" || function == "ge128");

            // TODO: add alpha function handling code here.
        }

        void Detail()
        {
            // We're including all detail textures here anyways, so we
            // ignore any special cases here.
        }
        #endregion

        #region Initialization code

        void InitializeTokenParsers()
        {
            mGeneralTokenParsers = new TokenParser[]
            {
                new TokenParser("skyparms", SkyParms),
                new TokenParser("cull", Cull),
                new TokenParser("deformvertexes", DeformVertexes),
                new TokenParser("fogparms", FogParms),
                new TokenParser("nopicmip", Nopicmip),
                new TokenParser("nomipmaps", Nomipmap),
                new TokenParser("polygonoffset", PolygonOffset),
                new TokenParser("portal", Portal),
                new TokenParser("sort", Sort),
                new TokenParser("tesssize", DiscardOneTokenAndContinue),
                new TokenParser("q3map_backshader", DiscardOneTokenAndContinue),
                new TokenParser("q3map_globaltexture", PassAndContinue),
                new TokenParser("q3map_sun", Q3mapSun),
                new TokenParser("q3map_surfacelight", Q3mapSurfaceLight),
                new TokenParser("q3map_lightimage", DiscardOneTokenAndContinue),
                new TokenParser("q3map_lightsubdivide", DiscardOneTokenAndContinue),
                new TokenParser("q3map_backsplash", DiscardTwoTokensAndContinue),
                new TokenParser("q3map_flare", PassAndContinue),
                new TokenParser("light", DiscardOneTokenAndContinue),
                new TokenParser("light1", PassAndContinue),
                new TokenParser("surfaceparm", DiscardOneTokenAndContinue),
                new TokenParser("qer_editorimage", DiscardOneTokenAndContinue),
                new TokenParser("qer_nocarve", QerNoCarve),
                new TokenParser("qer_trans", DiscardOneTokenAndContinue),
                new TokenParser("fogonly", FogOnly),
                new TokenParser("entitymergable", EntityMergable),
                new TokenParser("cloudparms", CloudParms),
                new TokenParser("lightning", Lightning),
                new TokenParser("sky", Sky)
            };

            mShaderStageTokenParsers = new TokenParser[] 
            {
                new TokenParser("map", Map),
                new TokenParser("clampmap", ClampMap),
                new TokenParser("animmap", AnimMap),
                new TokenParser("blendfunc", BlendFunc),
                new TokenParser("rgbgen", RgbGen),
                new TokenParser("alphagen", AlphaGen),
                new TokenParser("tcgen", TcGen),
                new TokenParser("tcmod", TcMod),
                new TokenParser("depthfunc", DepthFunc),
                new TokenParser("depthwrite", DepthWrite),
                new TokenParser("alphafunc", AlphaFunc),
                new TokenParser("detail", Detail)
            };

            Array.Sort(mGeneralTokenParsers, (a, b) => { return a.Token.CompareTo(b.Token); });
            Array.Sort(mShaderStageTokenParsers, (a, b) => { return a.Token.CompareTo(b.Token); });
        }

        #endregion

        #region Parser code

        bool IsKeyword(TokenParser[] parsers, string token)
        {
            string lowerCaseToken = token.ToLower();

            for (int i = 0; i < parsers.Length; ++i)
                if (parsers[i].Token == lowerCaseToken)
                    return true;

            return false;
        }

        public ShaderParser(string content)
        {   
            I = 0;
            E = content.Length;
            mContent = content;
            InitializeTokenParsers();

            ParseAllShaders();
        }

        void ConsumeWhitespace()
        {
            for (; I != E; ++I)
                if (!Char.IsWhiteSpace(mContent[I]))
                    break;

            if (I < E - 2)
            {
                if (mContent[I] == '/' && mContent[I] == '/')
                {
                    for (; I < E - 2; ++I)
                        if (mContent[I] == '\n' || mContent[I] == '\r')
                            break;
                    I += 2;

                    ConsumeWhitespace();
                }
            }
        }

        string NextToken()
        {
            return NextTokenImpl();
        }

        string NextTokenImpl()
        {
            if (mStashedToken != null)
            {
                string t = mStashedToken;
                mStashedToken = null;
                return t;
            }

            if (I == E)
                return null;

            ConsumeWhitespace();

            if (I == E)
                return null;

            int start = I, end = I + 1;
            while (end < E)
            {
                if (Char.IsWhiteSpace(mContent[end]))
                    break;
                ++end;
            }

            I = end;
            return mContent.Substring(start, end - start);
        }

        static bool IsNewLine(char c)
        {
            return c == '\r' || c == '\n';
        }

        void SkipToEndOfLine()
        {
            if (mStashedToken != null)
                return;

            if (I == E)
                return;

            bool newlineFound = false;
            for (; I < E; ++I)
            {
                if (IsNewLine(mContent[I]))
                {
                    newlineFound = true;
                }
                else
                {
                    if (newlineFound)
                        return;
                }
            }
        }

        string NextTokenLowerCase()
        {
            string s = NextToken();
            if (s == null)
                return s;
            return s.ToLower();
        }

        void PushToken(string s)
        {
            if (mStashedToken != null)
                throw new Exception("Didn't expect there to be a token alredy here.");

            mStashedToken = s;
        }

        void ParseAllShaders()
        {
            while (ParseShader())
            {
                // do something with mShader
                mShader = null;
            }
        }

        string Expect(string str)
        {
            string token = NextToken();
            if (token != str)
            {
                throw new Exception(
                    string.Format("Shit's malformed, just sayin'. While parsing effect '{0}', I expected '{1}' and got '{2}'",
                        mCurrentEffect, str, token));
            }
            return token;
        }

        void InvokeParser(TokenParser[] parsers, string token)
        {
            TokenParser t = Array.Find<TokenParser>(parsers, x => x.Token == token);

            if (t == null)
                throw new Exception(string.Format("shit's broken. failing on token '{0}'", token));

            t.Function();
        }

        void ParseGeneralShaderCode()
        {
            for (; ; )
            {
                string token = NextTokenLowerCase();
                if (token == "{" || token == "}")
                {
                    PushToken(token);
                    return;
                }

                InvokeParser(mGeneralTokenParsers, token);
                SkipToEndOfLine();
            }
        }

        void ParseShaderStages()
        {
            for (; ; )
            {
                string t = NextToken();
                if (t != "{")
                {
                    PushToken(t);
                    return;
                }
              
                for (; ; )
                {
                    string token = NextTokenLowerCase();
                    if (token == "}")
                        break;

                    InvokeParser(mShaderStageTokenParsers, token);
                    SkipToEndOfLine();
                }
            }
        }

        bool ParseShader()
        {
            mCurrentEffect = NextToken();
            if (mCurrentEffect == null)
                return false;

            using (mShader = new Shader(mCurrentEffect))
            {

                Expect("{");

                ParseGeneralShaderCode();
                ParseShaderStages();

                Expect("}");

                //string shader = mShader.mProgram.EmitEffect();
                mShader.FinalizeShader();
                string shader = mShader.mProgram.EmitRawShaderCode();

                Console.WriteLine(shader);
            }
            return true;
        }

        #endregion
    }

    class ShaderReader
    {
        static void Process(string shaderContent)
        {
            ShaderParser SP = new ShaderParser(shaderContent);
        }

        static void Main(string[] args)
        {
            if (args.Length == 0)
                return;

            string dir = args[0];
            if (!Directory.Exists(dir))
                return;

            DirectoryInfo DI = new DirectoryInfo(dir);
            FileInfo[] files = DI.GetFiles("*.shader");
            foreach (FileInfo FI in files)
            {
                string content = File.ReadAllText(FI.FullName);
                Process(content);
            }

            Debugger.Break();
        }
    }
}
