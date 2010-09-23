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
        string mName;

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
        string mContent;
        int I;
        int E;

        public ShaderParser(string content)
        {
            I = 0;
            E = content.Length;
            mContent = content;
            Parse();
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

        void Parse()
        {
            for (; ; )
            {
                int nestLevel = 0;
                string effectName = NextToken();
                if (effectName == null)
                    return;

                Shader S = new Shader(effectName);

                for (; ; )
                {
                    string token = NextToken();

                    if (token == "{")
                    {
                        ++nestLevel;
                        continue;
                    }

                    if (token == "}")
                    {
                        --nestLevel;

                        if (nestLevel == 0)
                            break;
                    }
                }

                Console.WriteLine(S);
            }
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
