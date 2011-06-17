using System;
using System.IO;
using System.IO.Compression;
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

namespace Split.Pipeline
{
    public enum Flag
    {
        NOPICMIP    = 1,
        NOMIPMAP    = 1 << 1,
        TRANSPARENT = 1 << 2,
        DEPTH_WRITE = 1 << 3,
        DEPTH_EQUAL = 1 << 4,
        CULL_BACK = 1 << 5,
        CULL_FRONT = 1 << 6,
        ADDITIVE_BLEND = 1 << 8
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

    class Shader
    {
        public string mName;
        Dictionary<string, Value> mTextures = new Dictionary<string, Value>();
        public List<string> mTextureList = new List<string>();
        public HlslProgram mProgram = new HlslProgram();
        public UserDefinedFunction mVertexShader = new UserDefinedFunction("vs_main");
        public UserDefinedFunction mPixelShader = new UserDefinedFunction("ps_main");

        public readonly DeclExpr mWorld;
        public readonly DeclExpr mViewProjection;
        public readonly DeclExpr mTime;

        public Expr mVsOutput;
        public Expr mVsPositionExpr;
        public Expr mPsAccumulatedColor;

        public readonly Value mVsInput;
        public readonly Value mPsInput;

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

                Expr sinParam = new BinaryExpr(new BinaryExpr(t, freq, OpCode.MUL).Value, phase, OpCode.ADD);
                Expr sinExpr = new CallExpr(sin, new Expr[] { sinParam });

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

                Expr sinParam = new BinaryExpr(new BinaryExpr(t, freq, OpCode.MUL).Value, phase, OpCode.ADD);
                Expr sinExpr = new CallExpr(sin, new Expr[] { sinParam });
                Expr signExpr = new CallExpr(sign, new Expr[] { sinExpr });

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

                Expr twoOverPi = new BinaryExpr(new LiteralExpr(f, 2.0f).Value, PI.Value, OpCode.DIV);

                Expr sinExpr = new CallExpr(sin, new Value[] { t });
                Expr asinExpr = new CallExpr(asin, new Expr[] { sinExpr });
                Expr absExpr = new CallExpr(abs, new Expr[] { 
                    new BinaryExpr(twoOverPi.Value, asinExpr.Value, OpCode.MUL) });

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
                mVsInput = mVertexShader.AddArgument(vsData, "input");

                mVsOutput = new DeclExpr(vsData, "output");
                mVertexShader.AddExpr(mVsOutput);

                mVsPositionExpr = new StructMemberExpr(mVsInput, 0);

                // Pass through all non-position members of the vertex shader
                for (int i = 1; i <= 4; ++i)
                    mVertexShader.AddExpr(new AssignmentExpr(
                        new StructMemberExpr(mVsOutput.Value, i).Value,
                        new StructMemberExpr(mVsInput, i).Value));
            }
            #endregion

            #region Pixel shader setup
            {
                mPsInput = mPixelShader.AddArgument(vsData, "input");
            }
            #endregion
        }

        public Value AddTexture(string texture)
        {
            if (mTextures.ContainsKey(texture))
                return mTextures[texture];

            int idx = mTextures.Count;
            DeclExpr samplerDeclExpr = new DeclExpr(TypeRegistry.GetSamplerType(), string.Format("gSampler{0}", idx));
            samplerDeclExpr.SetRegister(string.Format("s{0}", idx));

            mProgram.AddGlobal(samplerDeclExpr);
            mTextures.Add(texture, samplerDeclExpr.Value);
            mTextureList.Add(texture);

            return samplerDeclExpr.Value;
        }

        public void FinalizeShader()
        {
            #region Finalize vertex shader
            {
                // Transform the position after the shader code below has first applied any particular 
                // model-space deformations.
                Function mul = mProgram.GetFunctionByName("mul");
                Expr worldViewProjExpr = new CallExpr(mul, new Expr[] { mWorld, mViewProjection });
                Expr transformedPos = new CallExpr(mul, new Expr[] { mVsPositionExpr, worldViewProjExpr});
                mVertexShader.AddExpr(new AssignmentExpr(new StructMemberExpr(mVsOutput.Value, 0).Value, transformedPos.Value));
                mVertexShader.AddExpr(new ReturnExpr(mVsOutput));

                mProgram.SetShader(ShaderType.VertexShader, mVertexShader, Hlsl.ShaderProfile.vs_3_0);
            }
            #endregion

            #region Finalize pixel shader
            {
                Function mul = mProgram.GetFunctionByName("mul");
                Hlsl.Type f = TypeRegistry.GetFloatType();
                Hlsl.Type f4 = TypeRegistry.GetVectorType(f, 4);

                mPixelShader.AddExpr(new ReturnExpr(
                    new CallExpr(
                        mul,
                        new Expr[] { 
                            mPsAccumulatedColor,
                            new LiteralExpr(f, 2.0f)
                        })));
                mPixelShader.SetReturnTypeSemantic(new Semantic(Semantic.SemanticType.COLOR));
                mProgram.SetShader(ShaderType.PixelShader, mPixelShader, Hlsl.ShaderProfile.ps_3_0);
            }
            #endregion
        }

        public override string ToString()
        {
            return mName;
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

        bool mHasShader;

        #region Shader stage data

        int mCurrentTextureStage;
        BlendMode mSrcBlend = BlendMode.GL_ONE;
        BlendMode mDestBlend = BlendMode.GL_ONE;
        Value mSampler;
        Expr mTexCoords;
        bool mIsClamped;
        Expr mStageColor;
        bool mIsAnimMap;
        List<Value> mAnimMapSamplers;
        string mRgbGenFunction;
        Expr mRgbGenMultiplicationExpr;
        float mAnimFrequency;

        #endregion

        void Reset()
        {
            mSampler = null;
            mTexCoords = null;
            mIsAnimMap = false;
            mIsClamped = false;
            mStageColor = null;
            mAnimFrequency = 0;
            mAnimMapSamplers = null;
            mRgbGenFunction = null;
            mRgbGenMultiplicationExpr = null;
        }

        public string GetHlsl()
        {
            HlslProgram program = mShader.mProgram;
            return program.EmitEffect();
        }

        public string[] GetTextureList()
        {
            string[] textures = mShader.mTextureList.ToArray();
            for (int i = 0; i < textures.Length; ++i)
                if (textures[i].Contains('.'))
                    textures[i] = textures[i].Substring(0, textures[i].LastIndexOf('.'));

            return textures;
        }

        public uint GetFlags()
        {
            return mShader.mFlags;
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

        void SurfaceParm()
        {
            string token = NextTokenLowerCase();

            switch (token)
            {
                case "trans":
                    mShader.mFlags |= (uint)(Flag.TRANSPARENT);
                    break;
            }
        }

        void Cull() 
        {
            string cullMode = NextTokenLowerCase();

            switch (cullMode)
            {
                case "front":
                    mShader.SetFlag(Flag.CULL_FRONT);
                    break;
                case "back":
                    mShader.SetFlag(Flag.CULL_BACK);
                    break;
                case "disable":
                case "none":
                case "twosided":
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
                        Value pos = mShader.mVsPositionExpr.Value;

                        Expr offsetExpr = new BinaryExpr(
                            new BinaryExpr(
                                new BinaryExpr(
                                    new SwizzleExpr(pos, "x").Value, 
                                    new SwizzleExpr(pos, "y").Value,
                                    OpCode.ADD).Value,
                                new SwizzleExpr(pos, "z").Value,
                                OpCode.ADD).Value,
                            new LiteralExpr(floatType, div).Value,
                            OpCode.MUL);
                        DeclExpr offsetDecl = new DeclExpr(offsetExpr);
                        offsetDecl.SetConst(true);
                        mShader.mVertexShader.AddExpr(offsetDecl);

                        Expr deformFnCall = new CallExpr(fn, new Expr[] {
                            mShader.mTime,
                            new LiteralExpr(floatType, baseVal),
                            new LiteralExpr(floatType, ampVal),
                            new BinaryExpr(new LiteralExpr(floatType, phaseVal).Value, offsetDecl.Value, OpCode.ADD),
                            new LiteralExpr(floatType, freqVal)
                        });

                        Function mul = mShader.mProgram.GetFunctionByName("mul");
                        DeclExpr fnValue = new DeclExpr(deformFnCall);
                        fnValue.SetConst(true);
                        mShader.mVertexShader.AddExpr(fnValue);

                        Hlsl.Type f4 = TypeRegistry.GetVectorType(floatType, 4);

                        Expr mulExpr = new BinaryExpr(
                            new LiteralExpr(f4, fnValue.Value, fnValue.Value, fnValue.Value, 0.0f).Value,
                            new LiteralExpr(TypeRegistry.GetVectorType(floatType, 4), new StructMemberExpr(mShader.mVsInput, "normal").Value, 1).Value,
                            OpCode.MUL);
/*                        Expr mulExpr = new CallExpr(mul, new Expr[] {
                            new BinaryExpr(deformFnCall.Value, new LiteralExpr(floatType, div).Value, OpCode.MUL),
                            new LiteralExpr(TypeRegistry.GetVectorType(floatType, 4), new StructMemberExpr(mShader.mVsInput, "normal").Value, 1),
                        });*/

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
                        float x = float.Parse(NextToken());
                        float y = float.Parse(NextToken());
                        float z = float.Parse(NextToken());
                        string func = NextTokenLowerCase() + "wave";
                        float baseVal = float.Parse(NextToken());
                        float ampVal = float.Parse(NextToken());
                        float phaseVal = float.Parse(NextToken());
                        float freqVal = float.Parse(NextToken());

                        Hlsl.Type floatType = TypeRegistry.GetFloatType();

                        Expr positionExpr = new LiteralExpr(TypeRegistry.GetVectorType(floatType, 4), x, y, z, 1.0f);

                        Function fn = mShader.mProgram.GetFunctionByName(func);
                        Expr moveCall = new CallExpr(fn, new Expr[] {
                            mShader.mTime,
                            new LiteralExpr(floatType, baseVal),
                            new LiteralExpr(floatType, ampVal),
                            new LiteralExpr(floatType, phaseVal),
                            new LiteralExpr(floatType, freqVal)
                        });

                        Function mul = mShader.mProgram.GetFunctionByName("mul");
                        Expr modifiedPosition = new BinaryExpr(
                            new CallExpr(mul, new Expr[] { positionExpr, moveCall }).Value,
                            mShader.mVsPositionExpr.Value,
                            OpCode.ADD);
                        DeclExpr modifiedPositionDeclaration = new DeclExpr(modifiedPosition);
                        modifiedPositionDeclaration.SetConst(true);
                        mShader.mVertexShader.AddExpr(modifiedPositionDeclaration);
                        mShader.mVsPositionExpr = modifiedPositionDeclaration;
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

            bool isLightmap = textureMap == "$lightmap";
            mSampler = mShader.AddTexture(textureMap);
            mTexCoords = new StructMemberExpr(mShader.mPsInput, isLightmap ? "lightmapST" : "surfaceST");
        }

        void ClampMap() 
        {
            string textureMap = NextToken();

            bool isLightmap = textureMap == "$lightmap";
            mSampler = mShader.AddTexture(textureMap);
            mIsClamped = true;
            mTexCoords = new StructMemberExpr(mShader.mPsInput, isLightmap ? "lightmapST" : "surfaceST");
        }

        void AnimMap() 
        {
            mAnimFrequency = float.Parse(NextToken());

            mAnimMapSamplers = new List<Value>();

            for (; ; )
            {
                string token = NextToken();

                if (IsKeyword(mShaderStageTokenParsers, token))
                {
                    PushToken(token);
                    break;
                }
                mAnimMapSamplers.Add(mShader.AddTexture(token));
            }

            mIsAnimMap = true;
            mTexCoords = new StructMemberExpr(mShader.mPsInput, "surfaceST");
        }

        void BlendFunc() 
        {
            string blendFunction = NextTokenLowerCase();
            bool parsed = false;

            switch (blendFunction)
            {
                case "add":
                    mSrcBlend = BlendMode.GL_ONE;
                    mDestBlend = BlendMode.GL_ONE;
                    parsed = true;
                    break;
                case "blend":
                    mSrcBlend = BlendMode.GL_SRC_ALPHA;
                    mDestBlend = BlendMode.GL_ONE_MINUS_SRC_ALPHA;
                    parsed = true;
                    break;
                case "filter":
                    mSrcBlend = BlendMode.GL_DST_COLOR;
                    mDestBlend = BlendMode.GL_ZERO;
                    parsed = true;
                    break;
                default:
                    break;
            }

            if (parsed)
                return;

            string sourceBlendToken = blendFunction.ToUpper();
            string destBlendToken = NextToken().ToUpper();
            mSrcBlend = (BlendMode)Enum.Parse(typeof(BlendMode), sourceBlendToken);
            mDestBlend = (BlendMode)Enum.Parse(typeof(BlendMode), destBlendToken);
        }

        void RgbGen()
        {
            mRgbGenFunction = NextTokenLowerCase();
            switch (mRgbGenFunction)
            {
                // In the case of identity/identitylighting, the rgbgen function multiplies the input color by 
                // float4(1, 1, 1, 1) but as that's a nop we just do nothing.
                case "identitylighting":
                case "identity":
                    break;

                // entity/oneminusentity are not used currently.
                case "entity":
                case "oneminusentity":
                    break;

                case "exactvertex":
                case "vertex":
                    mRgbGenMultiplicationExpr = new StructMemberExpr(mShader.mPsInput, "color");
                    break;
                case "oneminusvertex":
                    {
                        Expr one = new LiteralExpr(TypeRegistry.GetVectorType(TypeRegistry.GetFloatType(), 4),
                            1.0f, 1.0f, 1.0f, 0.0f);
                        mRgbGenMultiplicationExpr = new LiteralExpr(TypeRegistry.GetVectorType(TypeRegistry.GetFloatType(), 4), 
                            new SwizzleExpr(new BinaryExpr(one.Value,
                                new StructMemberExpr(mShader.mPsInput, "color").Value,
                                OpCode.SUB).Value, "xyz").Value, 1.0f);
                    }
                    break;

                // TODO: need source light vector.
                case "lightingdiffuse":
                    break;
                case "wave":
                    {
                        string func = NextTokenLowerCase() + "wave";

                        float baseVal = float.Parse(NextToken());
                        float ampVal = float.Parse(NextToken());
                        float phaseVal = float.Parse(NextToken());
                        float freqVal = float.Parse(NextToken());

                        Hlsl.Type floatType = TypeRegistry.GetFloatType();

                        if (func == "noisewave")
                        {
                            // Need to add a noise texture at runtime.
                            Value noiseTexture = mShader.AddTexture("noise");
                            Function tex1D = mShader.mProgram.GetFunctionByName("tex1D");
                            mRgbGenMultiplicationExpr = new SwizzleExpr(new CallExpr(tex1D, new Value[] { noiseTexture, mShader.mTime.Value }).Value, "xxxx");
                            return;
                        }

                        Function fn = mShader.mProgram.GetFunctionByName(func);
                        Expr waveCall = new DeclExpr(new CallExpr(fn, new Expr[] {
                            mShader.mTime,
                            new LiteralExpr(floatType, baseVal),
                            new LiteralExpr(floatType, ampVal),
                            new LiteralExpr(floatType, phaseVal),
                            new LiteralExpr(floatType, freqVal)
                        }));
                        mShader.mPixelShader.AddExpr(waveCall);
                        Value v = waveCall.Value;
                        mRgbGenMultiplicationExpr = new LiteralExpr(TypeRegistry.GetVectorType(floatType, 4), v, v, v, 1.0f);
                    }
                    break;
            }
        }

        Expr CalculateAnimMapSample()
        {
            int numSamplers = mAnimMapSamplers.Count;
            float period = 1.0f / mAnimFrequency;
            float interval = period / (float)numSamplers;

            Function fmod = mShader.mProgram.GetFunctionByName("fmod");
            DeclExpr timeModPeriod = new DeclExpr(
                new CallExpr(
                    fmod,
                    new Expr[] { 
                            mShader.mTime,
                            new LiteralExpr(TypeRegistry.GetFloatType(), period)
                        }));
            timeModPeriod.SetConst(true);
            mShader.mPixelShader.AddExpr(timeModPeriod);

            float[] intervals = new float[numSamplers - 1];
            for (int i = 0; i < numSamplers - 1; ++i)
                intervals[numSamplers - 2 - i] = interval * (float)(i + 1);

            Function tex2D = mShader.mProgram.GetFunctionByName("tex2D");
            Expr elseExpr = new CallExpr(tex2D, new Value[] { mAnimMapSamplers[numSamplers - 1], mTexCoords.Value });
            Hlsl.Type f = TypeRegistry.GetFloatType();

            for (int i = 0; i < intervals.Length; ++i)
            {
                TernaryExpr TE = new TernaryExpr(
                    new ComparisonExpr(timeModPeriod.Value, new LiteralExpr(f, intervals[i]).Value, Comparison.LESS),
                    new CallExpr(tex2D, new Value[] { mAnimMapSamplers[numSamplers - 2 - i], mTexCoords.Value }),
                    elseExpr);
                elseExpr = TE;
            }

            DeclExpr oneUnholyMess = new DeclExpr(elseExpr);
            mShader.mPixelShader.AddExpr(oneUnholyMess);
            mIsAnimMap = false;
            mAnimMapSamplers = null;

            return oneUnholyMess;
        }

        void PerformRgbGen()
        {
            Function saturate = mShader.mProgram.GetFunctionByName("saturate");
            Function tex2D = mShader.mProgram.GetFunctionByName("tex2D");

            Expr texCoords = mIsClamped
                ? new CallExpr(saturate, new Expr[] { mTexCoords })
                : mTexCoords;

            Expr sample = mIsAnimMap 
                ? CalculateAnimMapSample() 
                : new CallExpr(tex2D, new Value[] { mSampler, texCoords.Value });

            // Some effects, like the lagometer, just specify a texture map
            // and don't have any rgbgen keywords.
            if (mRgbGenFunction == null)
                mRgbGenFunction = "identity";

            switch (mRgbGenFunction)
            {
                case "identitylighting":
                case "identity":
                case "entity":
                case "oneminusentity":
                case "lightingdiffuse":
                    mStageColor = sample;
                    break;

                case "exactvertex":
                case "vertex":
                case "oneminusvertex":
                case "wave":
                    mStageColor = new BinaryExpr(mRgbGenMultiplicationExpr.Value, sample.Value, OpCode.MUL);
                    break;
            }

            if (mStageColor == null)
                throw new Exception("Unable to determine color in this shader stage.");

            mStageColor = new DeclExpr(mStageColor);
            mShader.mPixelShader.AddExpr(mStageColor);
        }

        void FinalizeTextureStageAndBlend() 
        {
            PerformRgbGen();
            PerformAlphaGen();

            if (mStageColor == null && mTexCoords != null && mSampler != null)
            {
                Function tex2D = mShader.mProgram.GetFunctionByName("tex2D");
                mStageColor = new CallExpr(tex2D, new Value[] { mSampler, mTexCoords.Value });
            }                 

            if (mShader.mPsAccumulatedColor == null)
            {
                mShader.mPsAccumulatedColor = mStageColor;
                return;
            }

            Expr source = mStageColor;
            Expr dest = mShader.mPsAccumulatedColor;
            Hlsl.Type f4 = TypeRegistry.GetVectorType(TypeRegistry.GetFloatType(), 4);
            bool skipCombine = false;

            switch (mSrcBlend)
            {
                case BlendMode.GL_ONE:
                    break;
                case BlendMode.GL_ZERO:
                    skipCombine = true;
                    mShader.mPsAccumulatedColor = dest;
                    break;
                case BlendMode.GL_DST_COLOR:
                    source = new BinaryExpr(mShader.mPsAccumulatedColor.Value, source.Value, OpCode.MUL);
                    break;
                case BlendMode.GL_ONE_MINUS_DST_COLOR:
                    source = new DeclExpr(
                        new BinaryExpr(
                            new BinaryExpr(
                                new LiteralExpr(f4, 1.0f, 1.0f, 1.0f, 1.0f).Value,
                                mShader.mPsAccumulatedColor.Value,
                                OpCode.SUB).Value,
                            source.Value,
                            OpCode.MUL));
                    mShader.mPixelShader.AddExpr(source);
                    break;
                case BlendMode.GL_SRC_ALPHA:
                    source = new DeclExpr(
                        new BinaryExpr(new SwizzleExpr(source.Value, "wwww").Value, source.Value, OpCode.MUL));
                    mShader.mPixelShader.AddExpr(source);
                    break;
                case BlendMode.GL_ONE_MINUS_SRC_ALPHA:                    
                    source = new DeclExpr(
                        new BinaryExpr(
                            new BinaryExpr(
                                new LiteralExpr(f4, 1.0f, 1.0f, 1.0f, 1.0f).Value, 
                                new SwizzleExpr(source.Value, "wwww").Value, 
                                OpCode.SUB).Value,
                            source.Value, OpCode.MUL));
                    mShader.mPixelShader.AddExpr(source);
                    break;
                default:
                    throw new Exception("Whachu talkin about, Willis?");
            }

            switch (mDestBlend)
            {
                case BlendMode.GL_ONE:
                    break;
                case BlendMode.GL_ZERO:
                    skipCombine = true;
                    mShader.mPsAccumulatedColor = source;
                    break;
                case BlendMode.GL_SRC_COLOR:
                    dest = new BinaryExpr(mStageColor.Value, dest.Value, OpCode.MUL);
                    break;
                case BlendMode.GL_ONE_MINUS_SRC_COLOR:
                    dest = new DeclExpr(
                        new BinaryExpr(
                            new BinaryExpr(
                                new LiteralExpr(f4, 1.0f, 1.0f, 1.0f, 1.0f).Value,
                                mStageColor.Value,
                                OpCode.SUB).Value,
                            dest.Value,
                            OpCode.MUL));
                    mShader.mPixelShader.AddExpr(dest);
                    break;
                case BlendMode.GL_DST_ALPHA:
                    dest = new DeclExpr(
                        new BinaryExpr(new SwizzleExpr(dest.Value, "wwww").Value, dest.Value, OpCode.MUL));
                    mShader.mPixelShader.AddExpr(dest);
                    break;
                case BlendMode.GL_SRC_ALPHA:
                    dest = new DeclExpr(
                        new BinaryExpr(new SwizzleExpr(mStageColor.Value, "wwww").Value, dest.Value, OpCode.MUL));
                    mShader.mPixelShader.AddExpr(dest);
                    break;
                case BlendMode.GL_ONE_MINUS_DST_ALPHA:
                    dest = new DeclExpr(
                        new BinaryExpr(
                            new BinaryExpr(
                                new LiteralExpr(f4, 1.0f, 1.0f, 1.0f, 1.0f).Value,
                                new SwizzleExpr(dest.Value, "wwww").Value,
                                OpCode.SUB).Value,
                            dest.Value, OpCode.MUL));
                    mShader.mPixelShader.AddExpr(dest);
                    break;
                case BlendMode.GL_ONE_MINUS_SRC_ALPHA:
                    dest = new DeclExpr(
                        new BinaryExpr(
                            new BinaryExpr(
                                new LiteralExpr(f4, 1.0f, 1.0f, 1.0f, 1.0f).Value,
                                new SwizzleExpr(mStageColor.Value, "wwww").Value,
                                OpCode.SUB).Value,
                            dest.Value, OpCode.MUL));
                    mShader.mPixelShader.AddExpr(dest);
                    break;
                default:
                    throw new Exception("Whachu talkin about, Willis?");
            }

            // If the first texture stage has GL_ONE/GL_ONE set for source/dest blends then
            // ensure that additive blending is enabled in the back end renderer.
            if (++mCurrentTextureStage == 1 && mSrcBlend == BlendMode.GL_ONE && mDestBlend == BlendMode.GL_ONE)
                mShader.SetFlag(Flag.ADDITIVE_BLEND);

            if (!skipCombine)
                mShader.mPsAccumulatedColor = new BinaryExpr(source.Value, dest.Value, OpCode.ADD);

            mSrcBlend = BlendMode.GL_ONE;
            mDestBlend = BlendMode.GL_ONE;
            mSampler = null;
            mTexCoords = null;
            mIsClamped = false;
            mStageColor = null;
        }

        void PerformAlphaGen()
        {
            Debug.Assert(mStageColor != null);
        }

        void AlphaGen() 
        {
            // TODO: alphagen stuff.
            string function = NextTokenLowerCase();
            switch (function)
            {
                case "identitylighting":
                case "identity":
                case "entity":
                case "oneminusentity":
                case "exactvertex":
                case "vertex":
                case "oneminusvertex":
                    break;
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
                        float baseVal = float.Parse(NextToken());
                        float ampVal = float.Parse(NextToken());
                        float phaseVal = float.Parse(NextToken());
                        float freq = float.Parse(NextToken());
                    }
                    break;
            }

            /// TODO: Add alphagen handling code here.
        }

        void TcGen() 
        {
            // TODO: Figure out environment mapping.

            string function = NextTokenLowerCase();
            switch (function)
            {
                case "base":
                    mTexCoords = new StructMemberExpr(mShader.mPsInput, "surfaceST");
                    break;
                case "lightmap":
                    mTexCoords = new StructMemberExpr(mShader.mPsInput, "lightmapST");
                    break;
                case "environment":
                    break;
                default:
                    break;
            }

            // TODO: add tcgen handling code here
        }

        void TcMod() 
        {
            string function = NextTokenLowerCase();
            Hlsl.Type f = TypeRegistry.GetFloatType();
            Hlsl.Type f2 = TypeRegistry.GetVectorType(f, 2);
            Function mul = mShader.mProgram.GetFunctionByName("mul");

            switch (function)
            {
                case "rotate":
                    {
                        float degressPerSecond = float.Parse(NextToken());
                        Function sin = mShader.mProgram.GetFunctionByName("sin");
                        Function cos = mShader.mProgram.GetFunctionByName("cos");

                        Expr toRadians = new BinaryExpr(
                            new LiteralExpr(f, degressPerSecond).Value,
                            new LiteralExpr(f, (float)(Math.PI / 180.0)).Value,
                            OpCode.MUL);
                        DeclExpr theta = new DeclExpr(
                            new BinaryExpr(toRadians.Value, mShader.mTime.Value, OpCode.MUL));
                        theta.SetConst(true);
                        mShader.mPixelShader.AddExpr(theta);

                        DeclExpr sinTheta = new DeclExpr(new CallExpr(sin, new Expr[] { theta }));
                        DeclExpr cosTheta = new DeclExpr(new CallExpr(cos, new Expr[] { theta }));

                        sinTheta.SetConst(true);
                        cosTheta.SetConst(true);
                        mShader.mPixelShader.AddExpr(sinTheta);
                        mShader.mPixelShader.AddExpr(cosTheta);

                        DeclExpr matrix = new DeclExpr(new LiteralExpr(TypeRegistry.GetMatrixType(f2, 2),
                            cosTheta.Value, new BinaryExpr(new LiteralExpr(f, -1.0f).Value, sinTheta.Value, OpCode.MUL).Value,
                            sinTheta.Value, cosTheta.Value));
                        matrix.SetConst(true);
                        mShader.mPixelShader.AddExpr(matrix);
                        DeclExpr coords = new DeclExpr(new CallExpr(mul, new Expr[] { mTexCoords, matrix }));
                        coords.SetConst(true);
                        mShader.mPixelShader.AddExpr(coords);
                        mTexCoords = coords;

                    }
                    break;
                case "scale":
                    {
                        float sScale = float.Parse(NextToken());
                        float tScale = float.Parse(NextToken());
                        DeclExpr matrix = new DeclExpr(
                            new BinaryExpr(
                                new LiteralExpr(f2, sScale, tScale).Value,
                                mTexCoords.Value,
                                OpCode.MUL));
                        matrix.SetConst(true);
                        mShader.mPixelShader.AddExpr(matrix);
                        mTexCoords = matrix;
                    }
                    break;
                case "scroll":
                    {
                        float sSpeed = float.Parse(NextToken());
                        float tSpeed = float.Parse(NextToken());
                        DeclExpr scroll = new DeclExpr(
                            new BinaryExpr(
                                new LiteralExpr(f2, mShader.mTime.Value, mShader.mTime.Value).Value,
                                new LiteralExpr(f2, sSpeed, tSpeed).Value,
                                OpCode.MUL));
                        scroll.SetConst(true);
                        mShader.mPixelShader.AddExpr(scroll);

                        mTexCoords = new BinaryExpr(scroll.Value, mTexCoords.Value, OpCode.MUL);
                    }
                    break;
                case "stretch":
                    {
                        string func = NextTokenLowerCase() + "wave";
                        float baseVal = float.Parse(NextToken());
                        float ampVal = float.Parse(NextToken());
                        float phaseVal = float.Parse(NextToken());
                        float freqVal = float.Parse(NextToken());

                        Function fn = mShader.mProgram.GetFunctionByName(func);
                        CallExpr stretchFn = new CallExpr(fn, new Expr[] {
                            mShader.mTime,
                            new LiteralExpr(f, baseVal),
                            new LiteralExpr(f, ampVal),
                            new LiteralExpr(f, phaseVal),
                            new LiteralExpr(f, freqVal)
                        });
                        DeclExpr stretchFnResult = new DeclExpr(stretchFn);
                        stretchFnResult.SetConst(true);
                        mShader.mPixelShader.AddExpr(stretchFnResult);

                        mTexCoords = new CallExpr(
                            mul, 
                            new Expr[] { stretchFnResult, mTexCoords }
                        );
                    }
                    break;
                case "transform":
                    {
                        float m00 = float.Parse(NextToken());
                        float m01 = float.Parse(NextToken());
                        float m10 = float.Parse(NextToken());
                        float m11 = float.Parse(NextToken());
                        float t0 = float.Parse(NextToken());
                        float t1 = float.Parse(NextToken());

                        DeclExpr mat = new DeclExpr(new LiteralExpr(TypeRegistry.GetMatrixType(f2, 2), m00, m01, m10, m11));
                        mat.SetConst(true);

                        DeclExpr constantVector = new DeclExpr(new LiteralExpr(f2, t0, t1));
                        constantVector.SetConst(true);

                        mShader.mPixelShader.AddExprs(mat, constantVector);
                        mTexCoords = new BinaryExpr(
                            new CallExpr(mul, new Expr[] { mat, mTexCoords }).Value,
                            constantVector.Value,
                            OpCode.ADD);
                    }
                    break;
                case "turb":
                    {
                        string baseValToken = NextToken();

                        // Occasionally a "turb sin" declaration sneaks in.
                        // Discard, for now. 
                        // TODO: fix this.
                        float baseVal = (baseValToken == "sin") ? float.Parse(NextToken()) : float.Parse(baseValToken);
                        float ampVal = float.Parse(NextToken());
                        float phaseVal = float.Parse(NextToken());
                        float freq = float.Parse(NextToken());
                    }
                    break;
            }

            /// TODO: add tcmod handling code here.
        }

        void DepthFunc() 
        {
            string function = NextTokenLowerCase();
            switch (function)
            {
                case "equal":
                    mShader.SetFlag(Flag.DEPTH_EQUAL);
                    break;
                default:
                    throw new Exception("Expected depthfunc values of equal or lessequal only!");
            }
        }

        void DepthWrite() 
        {
            mShader.mFlags |= (uint)(Flag.DEPTH_WRITE);
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
                new TokenParser("surfaceparm", SurfaceParm),
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

            ParseShader();
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
                    {
                        FinalizeTextureStageAndBlend();
                        mHasShader = true;
                        break;
                    }

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

            mShader = new Shader(mCurrentEffect);
            mShader.mPixelShader.AddExpr(new CommentExpr(mCurrentEffect));

            Expect("{");

            ParseGeneralShaderCode();
            ParseShaderStages();

            Expect("}");

            if (mHasShader)
                mShader.FinalizeShader();

            return true;
        }
        #endregion
    }
}
