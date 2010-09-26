using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace ShaderReader
{
    class Shader
    {
        public enum CullMode
        {
            Front,
            Back,
            None
        }

        string mName;
        CullMode mCullMode = CullMode.Front;

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

        TokenParser[] mGeneralTokenParsers;
        TokenParser[] mShaderStageTokenParsers;

        void SkyParms()
        {
            string farBox = NextToken();
            string cloudHeight = NextToken();
            string nearbox = NextToken();
        }

        #region General parsers
        void Cull() { Debugger.Break(); }
        void DeformVertexes() { Debugger.Break(); }
        void FogParms() { Debugger.Break(); }
        void Nopicmip() { Debugger.Break(); }
        void Nomipmap() { Debugger.Break(); }
        void PolygonOffset() { Debugger.Break(); }
        void Portal() { Debugger.Break(); }
        void Sort() { Debugger.Break(); }
        void TessSize() { Debugger.Break(); }
        void Q3mapBackshader() { Debugger.Break(); }
        void Q3mapGlobalTexture() { Debugger.Break(); }
        void Q3mapSun() { Debugger.Break(); }
        void Q3mapSurfaceLight() 
        {
            string lightValue = NextToken();
        }

        void Q3mapLightImage() { Debugger.Break(); }
        void Q3mapLightSubdivide() { Debugger.Break(); }
        void SurfaceParm() { Debugger.Break(); }
        void QerEditorImage() { Debugger.Break(); }
        void QerNoCarve() { Debugger.Break(); }
        void QerTrans() { Debugger.Break(); }
        #endregion

        #region Shader stage parsers
        void Map() 
        {
            string textureMap = NextToken();
        }

        void ClampMap() { Debugger.Break(); }
        void AnimMap() { Debugger.Break(); }
        void BlendFunc() { Debugger.Break(); }
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
        }

        void AlphaGen() { Debugger.Break(); }
        void TcGen() { Debugger.Break(); }
        void TcMod() { Debugger.Break(); }
        void DepthFunc() { Debugger.Break(); }
        void DepthWrite() { Debugger.Break(); }
        void AlphaFunc() { Debugger.Break(); }
        #endregion

        void InitializeTokenParsers()
        {
            mGeneralTokenParsers = new TokenParser[]
            {
                new TokenParser("skyparms", SkyParms),
                new TokenParser("cull", Cull),
                new TokenParser("deformvertexes", DeformVertexes),
                new TokenParser("fogparms", FogParms),
                new TokenParser("nopicmip", Nopicmip),
                new TokenParser("nomipmap", Nomipmap),
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
                new TokenParser("surfaceparm", SurfaceParm),
                new TokenParser("qer_editorimage", QerEditorImage),
                new TokenParser("qer_nocarve", QerNoCarve),
                new TokenParser("qer_trans", QerTrans)
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
                new TokenParser("alphafunc", AlphaFunc)
            };

            Array.Sort(mGeneralTokenParsers, (a, b) => { return a.Token.CompareTo(b.Token); });
            Array.Sort(mShaderStageTokenParsers, (a, b) => { return a.Token.CompareTo(b.Token); });
        }


        Stack<string> mPushedTokens = new Stack<string>();
        Shader mShader;
        string mContent;
        string mCurrentEffect;
        int I;
        int E;

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
            if (mPushedTokens.Count != 0)
                return mPushedTokens.Pop();

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

        string NextTokenLowerCase()
        {
            string s = NextToken();
            if (s == null)
                return s;
            return s.ToLower();
        }

        void PushToken(string s)
        {
            mPushedTokens.Push(s);
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
                if (token == "{")
                {
                    PushToken(token);
                    return;
                }

                InvokeParser(mGeneralTokenParsers, token);
            }
        }

        void ParseShaderStages()
        {
            for (; ; )
            {
                string t = NextToken();
                if (t != "{")
                    return;
              
                for (; ; )
                {
                    string token = NextTokenLowerCase();
                    if (token == "}")
                        break;

                    InvokeParser(mShaderStageTokenParsers, token);
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
