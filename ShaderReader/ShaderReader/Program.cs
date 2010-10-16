using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ShaderReader
{
    class ShaderBuilder
    {
        StringBuilder mShaderBuilder = new StringBuilder();
        StringBuilder mVSBuilder = new StringBuilder();
        StringBuilder mPSBuilder = new StringBuilder();
        int mNumSamplers = 0;
        public string mLastVSValue = "input.position";
        public string mLastPSValue;

        public ShaderBuilder()
        {
            EmitShaderPrologue();
        }

        public string AddSampler()
        {
            return AddSampler("Anisotropic");
        }

        public string AddSampler(string filter)
        {
            string samplerName = string.Format("Sampler{0}", mNumSamplers);
            const string samplerFormat =
@"sampler {0} : register(s{0}) = sampler_state {
    MinFilter = {1};
    MagFilter = {1};
    MipFilter = {1};
};

";
            mShaderBuilder.AppendFormat(samplerFormat, mNumSamplers, filter);
            ++mNumSamplers;

            return samplerName;
        }

        public void AddVSLine(string code, string value)
        {
            mVSBuilder.Append("    ");
            mVSBuilder.AppendLine(code);
            mLastVSValue = value;
        }

        public void AddPSLine(string code, string value)
        {
            mPSBuilder.Append("    ");
            mPSBuilder.AppendLine(code);
            mLastPSValue = value;
        }

        void EmitShaderPrologue()
        {
            const string prologue =
@"struct VS_INPUT {
    float4 position : POSITION;
    float2 surfaceUV  : TEXCOORD0;
    float2 lightmapUV : TEXCOORD1;
    float3 normal   : NORMAL;
    float4 color    : COLOR;
};


struct VS_OUTPUT {
    float4 position : POSITION;
    float2 surfaceUV  : TEXCOORD0;
    float2 lightmapUV : TEXCOORD1;
    float3 normal   : NORMAL;
    float4 color    : COLOR;
};


struct PS_INPUT {
    float2 surfaceUV  : TEXCOORD0;
    float2 lightmapUV : TEXCOORD1;
    float3 normal   : NORMAL;
    float4 color    : COLOR;
};

float4x4 View;
float4x4 Projection;
float Time;

#define PI 3.1415926535

float SinWave(float t, float base, float amp, float phase, float freq) {
    const float t1 = t * 2 * PI;
    return sin(freq * t1 + phase) * amp + base;
}

float SquareWave(float t, float base, float amp, float phase, float freq) {
    const float t1 = t * 2 * PI;
    const float sign = sign(sin(freq * t1 + phase));
    return sign * amp + base;
}

float TriangleWave(float t, float base, float amp, float phase, float freq) {
    // this is kind of broken for now.
    const float t1 = t * PI;
    return abs((2.0 / PI) * asin(sin(t1)));
}

float SawtoothWave(float t, float base, float amp, float phase, float freq) {
    const float t1 = t * freq + phase;
    return (t1 - floor(t1)) * amp + base;
}

float InverseSawtooth(float t, float base, float amp, float phase, float freq) {
    return amp - sawtooth(t, base, amp, phase, freq);
}

";
            mShaderBuilder.Append(prologue);
        }

        void FinalizeVertexShader()
        {
            const string vertexShader =
@"VS_OUTPUT vs_main(in VS_INPUT input) {{
{0}

    VS_OUTPUT output;
    output.position = mul(mul({1}, View), Projection);
    output.surfaceUV = input.surfaceUV;
    output.lightmapUV = input.lightmapUV;
    output.normal = input.normal;
    output.color = input.color;

    return output;
}}

";
            mShaderBuilder.AppendFormat(vertexShader, mVSBuilder.ToString(), mLastVSValue);
        }

        void FinalizePixelShader()
        {
            const string pixelShader = 
@"float4 ps_main(in PS_INPUT input) : COLOR {{
{0}
    return {1};
}}
";
            mShaderBuilder.AppendFormat(pixelShader, mPSBuilder.ToString(), mLastPSValue);
        }

        void EmitShaderEpilogue()
        {
            const string epilogue =
@"technique defaultTechnique {
    pass P0 {
        VertexShader = compile vs_3_0 vs_main();
        PixelShader = compile ps_3_0 ps_main();
    }
}
";
            mShaderBuilder.Append(epilogue);
        }

        string mFinalShader;

        public string ToString()
        {
            if (mFinalShader == null)
            {
                FinalizeVertexShader();
                FinalizePixelShader();
                EmitShaderEpilogue();
                mFinalShader = mShaderBuilder.ToString();
            }

            return mFinalShader;
        }
    }

    class Shader
    {
        public enum CullMode
        {
            Front,
            Back,
            None
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

        public enum Flag
        {
            NOPICMIP = 1 << 0,
            NOMIPMAP = 1 << 1
        }

        string mName;
        public CullMode mCullMode = CullMode.Front;
        public List<string> mTextures = new List<string>();
        public ShaderBuilder mShaderBuilder = new ShaderBuilder();
        public uint mFlags;

        public void SetFlag(Flag f)
        {
            mFlags = mFlags | (uint)f;
        }

        public Shader(string name)
        {
            mName = name;
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

        public void DefaultParseFunction()
        {
        }

        #region General parsers

        void Cull() 
        {
            string cullMode = NextTokenLowerCase();

            switch (cullMode)
            {
                case "front":
                    mShader.mCullMode = Shader.CullMode.Front;
                    break;
                case "back":
                    mShader.mCullMode = Shader.CullMode.Back;
                    break;
                case "disable":
                case "none":
                case "twosided":
                    mShader.mCullMode = Shader.CullMode.None;
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
                        string div = NextToken();
                        string func = NextTokenLowerCase();
                        string baseVal = NextToken();
                        string ampVal = NextToken();
                        string phaseVal = NextToken();
                        string freqVal = NextToken();

                        mShader.mShaderBuilder.AddVSLine(
                            string.Format("float4 deformVertexesWavePosition = {0}({1}, {2}, {3}, {4}) * {5} * float4(input.normal, 1) + {6};",
                                func, baseVal, ampVal, phaseVal, freqVal, div, mShader.mShaderBuilder.mLastVSValue),
                            "deformVertexesWavePosition");
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

                        mShader.mShaderBuilder.AddVSLine(
                            string.Format("float4 deformVertexesMovePosition = float4({0}, {1}, {2}, 1) * {3}({4}, {5}, {6}, {7}) + {8};",
                            x, y, z, func, baseVal, ampVal, phaseVal, freqVal, mShader.mShaderBuilder.mLastVSValue),
                            "deformVertexesMovePosition");
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
            mShader.SetFlag(Shader.Flag.NOPICMIP);
        }

        void Nomipmap() 
        {
            mShader.SetFlag(Shader.Flag.NOMIPMAP);
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

        void TessSize() 
        {
            // Handled by the pipeline, discard.
            string tessSize = NextToken();
        }

        void Q3mapBackshader() 
        {
            // Handled by the pipeline, discard.
            NextToken();
        }

        void Q3mapFlare()
        {
            // Handled by the pipeline, discard.
        }

        void Q3mapGlobalTexture() 
        { 
            // Handled by the pipeline, discard.
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

        void Q3mapBacksplash()
        {
            // Handled by the pipeline, discard.
            NextToken();
            NextToken();
        }

        void Q3mapSurfaceLight() 
        {
            string lightValue = NextToken();
        }

        void Q3mapLight()
        {
            // Handled by the pipeline, discard.
            NextToken();
        }

        void Q3mapLightImage() 
        {
            // Handled by the pipeline, discard.
            NextToken();
        }

        void Q3mapLightSubdivide() 
        {
            // Handled by the pipeline, discard.
            NextToken();
        }

        void SurfaceParm()
        {
            // Handled by the pipeline and will appear in surface flags, discard.
            NextToken();
        }

        void QerEditorImage() 
        { 
            // Editor only, discard.
            NextToken();
        }

        void QerNoCarve()
        {
            // Editor only, discard.
        }

        void QerTrans()
        {
            // Editor only, discard.
            NextToken();
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
            if (textureMap.ToLower() != "$lightmap")
            {
                // TODO: Validate that the texture map exists
                mShader.mTextures.Add(textureMap);
            }
        }

        void ClampMap() 
        {
            string textureMap = NextToken();

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
            Shader.BlendMode sourceBlend = Shader.BlendMode.INVALID;
            Shader.BlendMode destBlend = Shader.BlendMode.INVALID;
            bool parsed = false;

            switch (blendFunction)
            {
                case "add":
                    sourceBlend = Shader.BlendMode.GL_ONE;
                    destBlend = Shader.BlendMode.GL_ONE;
                    parsed = true;
                    break;
                case "blend":
                    sourceBlend = Shader.BlendMode.GL_SRC_ALPHA;
                    destBlend = Shader.BlendMode.GL_ONE_MINUS_SRC_ALPHA;
                    parsed = true;
                    break;
                case "filter":
                    sourceBlend = Shader.BlendMode.GL_DST_COLOR;
                    destBlend = Shader.BlendMode.GL_ZERO;
                    parsed = true;
                    break;
                default:
                    break;
            }

            if (parsed)
                return;

            string sourceBlendToken = blendFunction.ToUpper();
            string destBlendToken = NextToken().ToUpper();
            sourceBlend = (Shader.BlendMode)Enum.Parse(typeof(Shader.BlendMode), sourceBlendToken);
            destBlend = (Shader.BlendMode)Enum.Parse(typeof(Shader.BlendMode), destBlendToken);

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
                new TokenParser("tesssize", TessSize),
                new TokenParser("q3map_backshader", Q3mapBackshader),
                new TokenParser("q3map_globaltexture", Q3mapGlobalTexture),
                new TokenParser("q3map_sun", Q3mapSun),
                new TokenParser("q3map_surfacelight", Q3mapSurfaceLight),
                new TokenParser("q3map_lightimage", Q3mapLightImage),
                new TokenParser("q3map_lightsubdivide", Q3mapLightSubdivide),
                new TokenParser("q3map_backsplash", Q3mapBacksplash),
                new TokenParser("q3map_flare", Q3mapFlare),
                new TokenParser("light", Q3mapLight),
                new TokenParser("light1", DefaultParseFunction),
                new TokenParser("surfaceparm", SurfaceParm),
                new TokenParser("qer_editorimage", QerEditorImage),
                new TokenParser("qer_nocarve", QerNoCarve),
                new TokenParser("qer_trans", QerTrans),
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

            mShader = new Shader(mCurrentEffect);

            Expect("{");

            ParseGeneralShaderCode();
            ParseShaderStages();

            Expect("}");

            Console.WriteLine(mShader);

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
