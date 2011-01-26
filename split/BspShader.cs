using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Split
{
    public enum ShaderFlags
    {
        NOPICMIP = 1,
        NOMIPMAP = 1 << 1,
        TRANSPARENT = 1 << 2,
        DEPTH_WRITE = 1 << 3,
        DEPTH_EQUAL = 1 << 4,
        CULL_BACK = 1 << 5,
        CULL_NONE = 1 << 6,
        NODRAW = 1 << 7,
    }

    class BspShader
    {
        public int mEffectIndex;
        public int mNumTextures;
        public int mLastAddedIdx;
        public uint mFlags;
        public int[] mTextureIndices;

        public const int LIGHTMAP = -1;

        public BspShader(int effectIndex, int numTextures, params int[] textureIndices)
        {
            mEffectIndex = effectIndex;
            mNumTextures = numTextures;
            mTextureIndices = textureIndices;
        }

        public BspShader(int effectIndex, int numTextures)
        {
            mEffectIndex = effectIndex;
            mNumTextures = numTextures;
            mTextureIndices = new int[numTextures];
        }

        public void AddIndex(int idx)
        {
            mTextureIndices[mLastAddedIdx++] = idx;
        }
    }
}
