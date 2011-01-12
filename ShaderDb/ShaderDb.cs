using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Microsoft.Xna.Framework.Content;
using Slim;

namespace Split.Pipeline
{
    public class GeneratedShader
    {
        public string mName;
        public int mFlags;
        public int mNumTextures;
        public int[] mTextureIndices;
        public int mShaderTextIndex;
    }

    public class ShaderDb : IDisposable
    {
        public List<string> mTextureNames = new List<string>();
        public List<string> mShaderText = new List<string>();
        public Dictionary<int, int> mTextureOffsetToIdx = new Dictionary<int, int>();
        public Dictionary<int, int> mShaderSourceOffsetToIdx = new Dictionary<int, int>();
        public List<GeneratedShader> mGeneratedShaders = new List<GeneratedShader>();

        EndianReader Reader;
        int BeginOffset;

        void Seek(int offset)
        {
            Reader.BaseStream.BaseStream.Seek(offset + BeginOffset, SeekOrigin.Begin);
        }

        string ReadString(int offset)
        {
            Seek(offset);
            return Reader.ReadCString();
        }

        public int NumTextShaders()
        {
            return mShaderText.Count;
        }

        public GeneratedShader Find(string name)
        {
            foreach (GeneratedShader GS in mGeneratedShaders)
                if (name == GS.mName)
                    return GS;

            return null;
        }

        public void Parse(BinaryReader input)
        {
            const int cookie = 'S' | ('H' << 8) | ('D' << 16) | ('R' << 24);
            BeginOffset = (int)input.BaseStream.Position;

            Reader = input.ReadInt32() == cookie
                ? (EndianReader)new LittleEndianReader(input) 
                : new BigEndianReader(input);

            int dataStart = Reader.ReadI4();
            int shaderNameBegin = Reader.ReadI4();
            int textureNameBegin = Reader.ReadI4();
            int textureOffsetsBegin = Reader.ReadI4();
            int shaderTextBegin = Reader.ReadI4();
            int numShaders = Reader.ReadI4();

            for (int i = 0; i < numShaders; ++i)
            {
                Seek(dataStart + i * 5 * sizeof(int));

                int nameOffset = Reader.ReadI4();
                int flags = Reader.ReadI4();
                int numTextures = Reader.ReadI4();
                int textureOffsetStarts = Reader.ReadI4();
                int shaderSourceOffset = Reader.ReadI4();

                GeneratedShader GS = new GeneratedShader();
                GS.mName = ReadString(shaderNameBegin + nameOffset);
                GS.mFlags = flags;
                GS.mNumTextures = numTextures;
                GS.mTextureIndices = new int[numTextures];

                if (mShaderSourceOffsetToIdx.ContainsKey(shaderSourceOffset))
                {
                    GS.mShaderTextIndex = mShaderSourceOffsetToIdx[shaderSourceOffset];
                }
                else
                {
                    string str = ReadString(shaderTextBegin + shaderSourceOffset);
                    int idx = mShaderText.Count;
                    GS.mShaderTextIndex = idx;
                    mShaderSourceOffsetToIdx[shaderSourceOffset] = mShaderText.Count;
                    mShaderText.Add(str);
                }

                for (int ii = 0; ii < numTextures; ++ii)
                {
                    Seek(textureOffsetsBegin + (textureOffsetStarts + ii) * sizeof(int));
                    int offset = Reader.ReadI4();
                    if (mTextureOffsetToIdx.ContainsKey(offset))
                    {
                        GS.mTextureIndices[ii] = mTextureOffsetToIdx[offset];
                    }
                    else
                    {
                        int idx = mTextureNames.Count;
                        mTextureOffsetToIdx[offset] = idx;
                        GS.mTextureIndices[ii] = idx;
                        string str = ReadString(textureNameBegin + offset);
                        mTextureNames.Add(str);
                    }
                }

                mGeneratedShaders.Add(GS);
            }
        }

        public void Dispose()
        {
            mTextureNames = null;
            mShaderText = null;
            mGeneratedShaders = null;
            mTextureOffsetToIdx = null;
            mShaderSourceOffsetToIdx = null;
        }
    }
}
