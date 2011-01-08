﻿using System;
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

        Dictionary<string, Value> mTextures = new Dictionary<string, Value>();
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

            DeclExpr samplerDeclExpr = new DeclExpr(TypeRegistry.GetSamplerType(), string.Format("gSampler{0}", mTextures.Count));
            mProgram.AddGlobal(samplerDeclExpr);
            mTextures.Add(texture, samplerDeclExpr.Value);

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

        bool mHasShader;

        #region Shader stage data
        BlendMode mSrcBlend = BlendMode.GL_ONE;
        BlendMode mDestBlend = BlendMode.GL_ONE;
        Value mSampler;
        Expr mTexCoords;
        bool mIsClamped;
        Expr mStageColor;
        #endregion

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
                            new LiteralExpr(TypeRegistry.GetVectorType(floatType, 4), new StructMemberExpr(mShader.mVsInput, "normal").Value, 1),
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

//                mShader.mTexturesAdd(token);
            }

            // Todo: Implement this.
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

        string mRgbGenFunction;
        Expr mRgbGenMultiplicationExpr;

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
                    throw new Exception("Unsupported");

                case "vertex":
                    mRgbGenMultiplicationExpr = new StructMemberExpr(mShader.mPsInput, "color");
                    break;
                case "oneminusvertex":
                    {
                        Expr one = new LiteralExpr(TypeRegistry.GetVectorType(TypeRegistry.GetFloatType(), 4),
                            1.0f, 1.0f, 1.0f, 1.0f);
                        mRgbGenMultiplicationExpr = new BinaryExpr(one.Value,
                            new StructMemberExpr(mShader.mPsInput, "color").Value,
                            OpCode.SUB);
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
                        mRgbGenMultiplicationExpr = new LiteralExpr(TypeRegistry.GetVectorType(floatType, 4), v, v, v, v);
                    }
                    break;
            }
        }

        void PerformRgbGen()
        {
            Function saturate = mShader.mProgram.GetFunctionByName("saturate");
            Function tex2D = mShader.mProgram.GetFunctionByName("tex2D");

            Expr texCoords = mIsClamped
                ? new CallExpr(saturate, new Expr[] { mTexCoords })
                : mTexCoords;
            Expr sample = new CallExpr(tex2D, new Value[] { mSampler, texCoords.Value });

            switch (mRgbGenFunction)
            {
                case "identitylighting":
                case "identity":
                    mStageColor = sample;
                    break;

                case "entity":
                case "oneminusentity":
                    break;

                case "vertex":
                case "oneminusvertex":
                case "lightingdiffuse":
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

            switch (function)
            {
                case "rotate":
                    {
                        float degressPerSecond = float.Parse(NextToken());
                        Function sin = mShader.mProgram.GetFunctionByName("sin");
                        Function cos = mShader.mProgram.GetFunctionByName("cos");
                        Function mul = mShader.mProgram.GetFunctionByName("mul");
                        Hlsl.Type f = TypeRegistry.GetFloatType();
                        Hlsl.Type f2 = TypeRegistry.GetVectorType(f, 2);

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

                        REMOVETHIS_BreakAfterEmitting = true;
                    }
                    break;
                case "scale":
                    {
                        float sScale = float.Parse(NextToken());
                        float tScale = float.Parse(NextToken());
                    }
                    break;
                case "scroll":
                    {
                        float sSpeed = float.Parse(NextToken());
                        float tSpeed = float.Parse(NextToken());
                    }
                    break;
                case "stretch":
                    {
                        string func = NextTokenLowerCase();
                        float baseVal = float.Parse(NextToken());
                        float ampVal = float.Parse(NextToken());
                        float phaseVal = float.Parse(NextToken());
                        float freq = float.Parse(NextToken());
                    }
                    break;
                case "transform":
                    {
                        float m00 = float.Parse(NextToken());
                        float m01 = float.Parse(NextToken());
                        float m10 = float.Parse(NextToken());
                        float M11 = float.Parse(NextToken());
                        float t0 = float.Parse(NextToken());
                        float t1 = float.Parse(NextToken());
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

            using (mShader = new Shader(mCurrentEffect))
            {

                Expect("{");

                ParseGeneralShaderCode();
                ParseShaderStages();

                Expect("}");

                //string shader = mShader.mProgram.EmitEffect();

                if (mHasShader)
                {
                    mShader.FinalizeShader();
                    string shader = mShader.mProgram.EmitRawShaderCode();

                    Console.WriteLine(shader);

                    if (REMOVETHIS_BreakAfterEmitting)
                        Debugger.Break();
                }

                mHasShader = false;
            }
            return true;
        }

        bool REMOVETHIS_BreakAfterEmitting = false;

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
