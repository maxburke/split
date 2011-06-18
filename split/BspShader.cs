using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Split
{
    enum FlagFields
    {
        SOURCE_BLEND = 8,
        DEST_BLEND = 12
    }

    public enum ShaderFlags
    {
        NOPICMIP = 1,
        NOMIPMAP = 1 << 1,
        TRANSPARENT = 1 << 2,
        DEPTH_WRITE = 1 << 3,
        DEPTH_EQUAL = 1 << 4,
        CULL_BACK = 1 << 5,
        CULL_FRONT = 1 << 6,
        CULL_FLAGS = (CULL_BACK | CULL_FRONT),
        SOURCE_BLEND_MASK = 15 << FlagFields.SOURCE_BLEND,
        DEST_BLEND_MASK = 15 << FlagFields.DEST_BLEND,
        BLEND_MASK = (SOURCE_BLEND_MASK | DEST_BLEND_MASK)
    }

    public enum BlendMode
    {
        INVALID = -1,
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

    class BspShader
    {
        public int mEffectIndex;
        public int mNumTextures;
        public int mLastAddedIdx;
        public uint mFlags;
        public int[] mTextureIndices;

        public const int LIGHTMAP = -1;
        const uint OPAQUE = ((uint)BlendMode.GL_ONE << (int)FlagFields.SOURCE_BLEND) | ((uint)BlendMode.GL_ZERO << (int)FlagFields.DEST_BLEND);

        public BspShader(int effectIndex, int numTextures, params int[] textureIndices)
        {
            mEffectIndex = effectIndex;
            mNumTextures = numTextures;
            mTextureIndices = textureIndices;
            mFlags = OPAQUE;
        }

        public BspShader(int effectIndex, int numTextures)
        {
            mEffectIndex = effectIndex;
            mNumTextures = numTextures;
            mTextureIndices = new int[numTextures];
            mFlags = OPAQUE;
        }

        public void AddIndex(int idx)
        {
            mTextureIndices[mLastAddedIdx++] = idx;
        }

        public bool IsOpaque()
        {
            return (mFlags & (uint)ShaderFlags.BLEND_MASK) == OPAQUE;
        }
    }
}
