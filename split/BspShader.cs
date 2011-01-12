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
        NODRAW = 1 << 5,
    }

    class BspShader
    {
        public int mEffectIndex;
        public int mLightmapIndex;
        public int mNumTextures;
        public int mLastAddedIdx;
        public int mFlags;
        public int[] mTextureIndices;
        public string mName;

        public BspShader(int effectIndex, int lightmapIndex, int numTextures, params int[] textureIndices)
        {
            mEffectIndex = effectIndex;
            mLightmapIndex = lightmapIndex;
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

            if (idx == -1)
                mLightmapIndex = mLastAddedIdx;
        }
    }
}
