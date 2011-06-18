using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Split.Pipeline;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Split
{
    class BspRenderer : IRenderable
    {
        #region Patch tesselator
        class PatchTesselator
        {
            public struct PatchData
            {
                public int FaceIndex;
                public int StartVertex;
                public int StartIndex;
                public int NumIndices;
            }

            Bsp mBsp;
            List<int> mIndices = new List<int>();
            List<Vertex> mVertices = new List<Vertex>();
            List<PatchData> mDrawData = new List<PatchData>();
            int[] mControlPointIndices = new int[9];

            IndexBuffer mIndexBuffer;
            VertexBuffer mVertexBuffer;

            const int TESSELATION_DEGREE = 16;

            public PatchTesselator(Bsp b, GraphicsDevice device, VertexDeclaration vertexDeclaration)
            {
                mBsp = b;

                for (int i = 0; i < mBsp.Faces.Length; ++i)
                    if (mBsp.Faces[i].Type == Face.FaceType.Patch)
                        TesselateFace(i);

                if (mVertices.Count == 0)
                    return;

                mVertexBuffer = new VertexBuffer(device, vertexDeclaration, mVertices.Count, BufferUsage.WriteOnly);
                mVertexBuffer.SetData<Vertex>(mVertices.ToArray());
                mVertices = null;

                mIndexBuffer = new IndexBuffer(device, typeof(int), mIndices.Count, BufferUsage.WriteOnly);
                mIndexBuffer.SetData<int>(mIndices.ToArray());
                mIndices = null;
            }

            void LoadControlPoints(Face F, int x, int y)
            {
                int v0 = F.FirstVertex;
                int stride = F.Size.first;
                mControlPointIndices[0] = v0 + y * stride + x;
                mControlPointIndices[1] = v0 + y * stride + x + 1;
                mControlPointIndices[2] = v0 + y * stride + x + 2;
                mControlPointIndices[3] = v0 + (y + 1) * stride + x;
                mControlPointIndices[4] = v0 + (y + 1) * stride + x + 1;
                mControlPointIndices[5] = v0 + (y + 1) * stride + x + 2;
                mControlPointIndices[6] = v0 + (y + 2) * stride + x;
                mControlPointIndices[7] = v0 + (y + 2) * stride + x + 1;
                mControlPointIndices[8] = v0 + (y + 2) * stride + x + 2;
            }

            void TesselatePatches()
            {
                for (int y = 0; y <= TESSELATION_DEGREE; ++y)
                    for (int x = 0; x <= TESSELATION_DEGREE; ++x)
                        mVertices.Add(InterpolateVertex(x, y));

                for (int y = 0; y < TESSELATION_DEGREE; ++y)
                {
                    for (int x = 0; x < TESSELATION_DEGREE; ++x)
                    {
                        mIndices.AddRange(new int[] { I(x + 1, y), I(x, y), I(x, y + 1) });
                        mIndices.AddRange(new int[] { I(x + 1, y + 1), I(x + 1, y), I(x, y + 1) });

                        // TODO: remove these:
                        mIndices.AddRange(new int[] { I(x, y), I(x + 1, y), I(x, y + 1) });
                        mIndices.AddRange(new int[] { I(x + 1, y), I(x + 1, y + 1), I(x, y + 1) });
                    }
                }
            }

            static int I(int x, int y)
            {
                return y * (TESSELATION_DEGREE + 1) + x;
            }

            static float InterpolationFactor(int u, int v, int i, int j)
            {
                double[] binomialCoefficients = new double[] { 1, 2, 1 };
                double frac = 1.0 / (double)TESSELATION_DEGREE;

                double fracU = (double)u * frac;
                double fracV = (double)v * frac;

                double jFactor = binomialCoefficients[j] * Math.Pow(fracV, (double)j) * Math.Pow(1.0 - fracV, (double)(2 - j));
                double iFactor = binomialCoefficients[i] * Math.Pow(fracU, (double)i) * Math.Pow(1.0 - fracU, (double)(2 - i));
                float totalFactor = (float)(iFactor * jFactor);

                return totalFactor;
            }

            Vertex InterpolateVertex(int u, int v)
            {
                Vector4 position = new Vector4();
                Vector2 surfaceTexCoord = new Vector2();
                Vector2 lightmapTexcoord = new Vector2();
                Vector3 normal = new Vector3();
                Vector3 color = new Vector3();

                for (int j = 0; j < 3; ++j)
                {
                    for (int i = 0; i < 3; ++i)
                    {
                        float factor = InterpolationFactor(u, v, i, j);
                        int idx = mControlPointIndices[3 * j + i];

                        position += mBsp.Vertices[idx].Position * factor;
                        surfaceTexCoord += mBsp.Vertices[idx].SurfaceTexCoord * factor;
                        lightmapTexcoord += mBsp.Vertices[idx].LightMapTexCoord * factor;
                        normal += mBsp.Vertices[idx].Normal * factor;
                        color += mBsp.Vertices[idx].VertexColor.ToVector3() * factor;
                    }
                }

                position.W = 1.0f;
                return new Vertex(position, surfaceTexCoord, lightmapTexcoord, normal, new Color(color));
            }

            void TesselateFace(int i)
            {
                PatchData PD = new PatchData();
                PD.FaceIndex = i;
                PD.StartVertex = mVertices.Count;
                PD.StartIndex = mIndices.Count;

                Face F = mBsp.Faces[i];
                for (int y = 0; y < F.Size.second - 1; y += 2)
                {
                    for (int x = 0; x < F.Size.first - 1; x += 2)
                    {
                        LoadControlPoints(mBsp.Faces[i], x, y);
                        TesselatePatches();
                        PD.NumIndices = mIndices.Count - PD.StartIndex;
                    }
                }

                mDrawData.Add(PD);
            }

            public VertexBuffer VertexBuffer { get { return mVertexBuffer; } }
            public IndexBuffer IndexBuffer { get { return mIndexBuffer; } }
            public List<PatchData> PatchDrawData { get { return mDrawData; } }
        }
        #endregion

        #region Structures related to draw call data
        struct DrawData
        {
            public VertexBuffer VB;
            public IndexBuffer IB;

            public DrawData(VertexBuffer vb, IndexBuffer ib)
            {
                VB = vb;
                IB = ib;
            }
        }


        struct DrawCall
        {
            bool Valid;

            readonly public int FirstVertex;
            readonly public int StartIndex;
            readonly public int NumIndices;
            readonly public int PrimitiveCount;
            readonly public int ShaderIndex;
            readonly public int LightMap;
            public int DrawDataIndex;
            public bool Visible;
            public ulong SortKey;

            public const ulong INVALID_DRAW_CALL = ulong.MaxValue;
            public const int UNUSED_DRAW_CALL = int.MaxValue;

            const int INVISIBLE_SHIFT = 63;
            const int TRANSPARENCY_SHIFT = 61;

            const ulong OPAQUE = 0;
            const ulong HAS_ALPHA = 1;
            const ulong TRANSPARENT = 2;

            const int DRAWDATA_SHIFT = 54;
            const int LIGHTMAP_SHIFT = 27;
            const int SURFACE_SHIFT = 0;

            const uint DRAWDATA_MASK = 0xFF;
            const uint TEX_MASK = 0x07FFFFFF;

            public DrawCall(int firstVertex, 
                int startIndex,
                int numIndices,
                int primitiveCount,
                int shaderIndex,
                int lightMap,
                int drawDataIndex)
            {
                Valid = true;

                FirstVertex = firstVertex;
                StartIndex = startIndex;
                NumIndices = numIndices;
                PrimitiveCount = primitiveCount;
                ShaderIndex = shaderIndex;
                LightMap = lightMap;
                DrawDataIndex = drawDataIndex;
                SortKey = INVALID_DRAW_CALL;
                Visible = false;
            }

            public void CreateSortKey()
            {
                if (!Valid)
                {
                    SortKey = INVALID_DRAW_CALL;
                    return;
                }

                // [invisible][transparent][draw data][lightmap][surface]
                SortKey = 0;

                // Opaque geometry should be drawn before geometry with alpha which
                // should be drawn before transparent geometry.
                ulong transparency = 0;
                if ((Instance.mShaders[ShaderIndex].mFlags & (int)ShaderFlags.TRANSPARENT) != 0)
                    transparency = (TRANSPARENT << TRANSPARENCY_SHIFT);
                else if (!Instance.mShaders[ShaderIndex].IsOpaque())
                    transparency = (HAS_ALPHA << TRANSPARENCY_SHIFT);
                SortKey |= transparency;

                SortKey |= (ulong)((uint)DrawDataIndex & DRAWDATA_MASK) << DRAWDATA_SHIFT;
                SortKey |= (ulong)((uint)LightMap & TEX_MASK) << LIGHTMAP_SHIFT;
                SortKey |= (ulong)((uint)ShaderIndex & TEX_MASK) << SURFACE_SHIFT;
            }

            public void UpdateSortKey()
            {
                if (!Valid)
                    return;

                const ulong INVISIBLE_BIT = 1ul << INVISIBLE_SHIFT;
                SortKey = Visible ? SortKey & (~INVISIBLE_BIT) : SortKey | INVISIBLE_BIT;
            }
        }
        #endregion

        #region Member data
        BspTree mBspTree;
        GraphicsDevice mDevice;
        DrawData[] mDrawData = new DrawData[(int)SurfaceType.NUM_SURFACE_TYPES];
        Texture2D[] mLightMapTextures;
        DrawCall[] mDrawCalls;
        int[] mDrawCallIndices;
        VertexDeclaration mVertexDeclaration;
        List<Texture2D> mTextures = new List<Texture2D>();
        Effect[] mEffects;
        BspShader[] mShaders;
        Effect mCurrentEffect;
        Matrix mViewProjection;
        float mTime;
        int mLastDrawIdx = -1;
        VisData mVisData;
        #endregion

        public static BspRenderer Instance;

        enum SurfaceType
        {
            MESH,
            PATCH,
            NUM_SURFACE_TYPES
        }

        public BspRenderer(Bsp bsp, BspTree bspTree, GraphicsDevice device, ContentManager contentManager)
        {
            Instance = this;

            mBspTree = bspTree;
            mDevice = device;

            mDrawCalls = new DrawCall[bsp.Faces.Length];
            for (int i = 0; i < mDrawCalls.Length; ++i)
                mDrawCalls[i].DrawDataIndex = DrawCall.UNUSED_DRAW_CALL;

            mDrawCallIndices = new int[bsp.Faces.Length];
            mVertexDeclaration = new VertexDeclaration(BspVertexFormat);

            for (int i = 0; i < mDrawCallIndices.Length; ++i)
                mDrawCallIndices[i] = i;

            CreateSurfaceTextures(bsp, contentManager);
            CreateLightMapTextures(bsp);

            CreatePatchDrawData(bsp);

            CreateMeshDrawData(bsp);
            CreateMeshDrawCalls(bsp);

            mVisData = bsp.VisData;
            bsp.VisData = null;

            for (int i = 0; i < mDrawCalls.Length; ++i)
                mDrawCalls[i].CreateSortKey();
        }

        #region Vertex data

        static int BspVertexSize { get { unsafe { return sizeof(Vertex); } } }

        static VertexElement[] BspVertexFormat { get { return mBspVertexFormat; } }

        static VertexElement[] mBspVertexFormat =
        {
            new VertexElement(0, VertexElementFormat.Vector4, VertexElementUsage.Position, 0),
            new VertexElement(16, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 0),
            new VertexElement(24, VertexElementFormat.Vector2, VertexElementUsage.TextureCoordinate, 1),
            new VertexElement(32, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
            new VertexElement(44, VertexElementFormat.Color, VertexElementUsage.Color, 0)
        };

        #endregion

        public int RenderPriority { get { return 1; } }

        #region Texture prep code
        int mDefaultTextureIndex = -1;

        int GetLoadedTextureIndex(string textureName, ContentManager contentManager)
        {
            if (textureName == "$lightmap")
                return BspShader.LIGHTMAP;

            try
            {
                int idx = mTextures.Count;
                Texture2D texture = contentManager.Load<Texture2D>(textureName);
                mTextures.Add(texture);
                return idx;
            }
            catch (ContentLoadException)
            {
                if (mDefaultTextureIndex == -1)
                {
                    Texture2D defaultTexture = new Texture2D(mDevice, 1, 1, false, SurfaceFormat.Color);
                    Color[] defaultTextureColor = new Color[1];
                    defaultTextureColor[0] = new Color(1.0f, 0.0f, 0.0f, 1.0f);
                    defaultTexture.SetData<Color>(defaultTextureColor);

                    mDefaultTextureIndex = mTextures.Count;
                    mTextures.Add(defaultTexture);
                }
                Debug.WriteLine(string.Format("    Unable to load texture {0}", textureName));
                return mDefaultTextureIndex;
            }
        }

        void CreateSurfaceTextures(Bsp bsp, ContentManager contentManager)
        {
            List<Effect> effects = new List<Effect>();
            Effect defaultShader = contentManager.Load<Effect>("bsp");
            effects.Add(defaultShader);

            int numSurfaces = bsp.Textures.Length;

            mShaders = new BspShader[numSurfaces];
            for (int i = 0; i < numSurfaces; ++i)
            {
                Surface sfc = bsp.Textures[i];

                try
                {
                    string shaderName = "shaders/" + sfc.Name;
                    CompiledShader shader = contentManager.Load<CompiledShader>(shaderName);
                    Effect E = new Effect(mDevice, shader.mEffectCode);
                    int effectIndex = effects.Count;
                    effects.Add(E);
                    
                    int numTextures = shader.mTextures.Length;
                    int[] textureIndices = new int[numTextures];
                    for (int ii = 0; ii < shader.mTextures.Length; ++ii)
                        textureIndices[ii] = GetLoadedTextureIndex(shader.mTextures[ii], contentManager);

                    mShaders[i] = new BspShader(effectIndex, numTextures, textureIndices);
                    mShaders[i].mFlags = shader.mFlags;
                }
                catch (ContentLoadException)
                {
                    Debug.WriteLine(string.Format("Unable to load shader {0}", sfc.Name));
                    int idx = GetLoadedTextureIndex(sfc.Name, contentManager);
                    mShaders[i] = new BspShader(0, 2, idx, BspShader.LIGHTMAP);
                }
            }

            mEffects = effects.ToArray();
        }

        void CreateLightMapTextures(Bsp bsp)
        {
            int numLightMaps = bsp.LightMaps.Length;
            Texture2D[] lightMaps = new Texture2D[numLightMaps + 1];
            for (int i = 0; i < numLightMaps; ++i)
            {
                Texture2D LM = new Texture2D(mDevice, 128, 128, true, SurfaceFormat.Color);
                LM.SetData<Color>(bsp.LightMaps[i].LightMap);
                lightMaps[i] = LM;
            }
            bsp.LightMaps = null;
            mLightMapTextures = lightMaps;

            Texture2D defaultLightMap = new Texture2D(mDevice, 1, 1, false, SurfaceFormat.Color);
            Color[] defaultLightMapColor = new Color[1];
            defaultLightMapColor[0] = new Color(1.0f, 1.0f, 1.0f, 1.0f);
            defaultLightMap.SetData<Color>(defaultLightMapColor);
            lightMaps[lightMaps.Length - 1] = defaultLightMap;
        }
        #endregion

        #region Mesh drawing prep code
        void CreateMeshDrawCalls(Bsp bsp)
        {
            for (int i = 0; i < bsp.Faces.Length; ++i)
            {
                Face F = bsp.Faces[i];
                if (F.Type == Face.FaceType.Mesh || F.Type == Face.FaceType.Polygon)
                {
                    int lightmapIndex = (F.LightMapIndex != -1) ? F.LightMapIndex : mLightMapTextures.Length - 1;
                    mDrawCalls[i] = new DrawCall(
                        F.FirstVertex,
                        F.FirstMeshVert,
                        F.NumMeshVerts,
                        F.NumMeshVerts / 3,
                        F.Texture,
                        lightmapIndex,
                        (int)SurfaceType.MESH);
                }
            }
        }

        void CreateMeshDrawData(Bsp bsp)
        {
            VertexBuffer VB = new VertexBuffer(mDevice, mVertexDeclaration, bsp.Vertices.Length, BufferUsage.WriteOnly);
            VB.SetData<Vertex>(bsp.Vertices);
            bsp.Vertices = null;

            IndexBuffer IB = new IndexBuffer(mDevice, typeof(int), bsp.MeshVerts.Length, BufferUsage.WriteOnly);
            IB.SetData<int>(bsp.MeshVerts);
            bsp.MeshVerts = null;

            mDrawData[(int)SurfaceType.MESH] = new DrawData(VB, IB);
        }
        #endregion
        
        #region Patch drawing prep code
        void CreatePatchDrawData(Bsp bsp)
        {
            /// TODO: Calculate the number of verts we create during tesselation
            /// and create an array of that size.
            PatchTesselator PT = new PatchTesselator(bsp, mDevice, mVertexDeclaration);
            mDrawData[(int)SurfaceType.PATCH] = new DrawData(PT.VertexBuffer, PT.IndexBuffer);

            int numPatches = PT.PatchDrawData.Count;
            for (int i = 0; i < numPatches; ++i)
            {
                PatchTesselator.PatchData PD = PT.PatchDrawData[i];
                Face F = bsp.Faces[PD.FaceIndex];
                int lightmapIndex = (F.LightMapIndex != -1) ? F.LightMapIndex : mLightMapTextures.Length - 1;
                mDrawCalls[PD.FaceIndex] = new DrawCall(
                    PD.StartVertex,
                    PD.StartIndex,
                    PD.NumIndices,
                    PD.NumIndices / 3,
                    F.Texture,
                    lightmapIndex,
                    (int)SurfaceType.PATCH);
            }
        }
        #endregion

        #region Rendering code
        void MarkAllFacesInvisible()
        {
            for (int i = 0; i < mDrawCalls.Length; ++i)
                mDrawCalls[i].Visible = false;
        }

        uint mLastRenderStateFlags = 0;

        // This array needs to be kept in sync with the BlendMode enumeration.
        static Blend[] gBlendTranslationTable =
        {
            Blend.One,
            Blend.Zero,
            Blend.DestinationColor,
            Blend.InverseDestinationColor,
            Blend.SourceColor,
            Blend.InverseSourceColor,
            Blend.DestinationAlpha,
            Blend.InverseDestinationAlpha,
            Blend.SourceAlpha,
            Blend.InverseSourceAlpha,
        };

        BlendState GenerateBlendState(BlendMode sourceBlend, BlendMode destBlend)
        {
            if (sourceBlend == BlendMode.GL_DST_COLOR && destBlend == BlendMode.GL_ZERO)
                return BlendState.Opaque;
            if (sourceBlend == BlendMode.GL_ONE && destBlend == BlendMode.GL_ONE)
                return BlendState.Additive;
            if (sourceBlend == BlendMode.GL_SRC_ALPHA && destBlend == BlendMode.GL_ONE_MINUS_SRC_ALPHA)
                return BlendState.NonPremultiplied;

            BlendState BS = new BlendState();
            BS.ColorSourceBlend = gBlendTranslationTable[(int)sourceBlend];
            BS.AlphaSourceBlend = gBlendTranslationTable[(int)sourceBlend];
            BS.ColorDestinationBlend = gBlendTranslationTable[(int)destBlend];
            BS.AlphaDestinationBlend = gBlendTranslationTable[(int)destBlend];

            return BS;
        }

        void UpdateRenderStateFlags(uint flags)
        {
            if (flags == mLastRenderStateFlags)
                return;

            BlendMode sourceBlend = (BlendMode)((flags & (uint)ShaderFlags.SOURCE_BLEND_MASK) >> (int)FlagFields.SOURCE_BLEND);
            BlendMode destBlend = (BlendMode)((flags & (uint)ShaderFlags.DEST_BLEND_MASK) >> (int)FlagFields.DEST_BLEND);

            mDevice.BlendState = ((flags & (uint)ShaderFlags.TRANSPARENT) == 0)
                ? GenerateBlendState(sourceBlend, destBlend)
                : BlendState.Additive;

            DepthStencilState depthState = new DepthStencilState();

            if ((flags & (uint)ShaderFlags.DEPTH_EQUAL) != 0)
                depthState.DepthBufferFunction = CompareFunction.Equal;
            else
                depthState.DepthBufferFunction = CompareFunction.LessEqual;

            // Depth buffer writes are enabled if the surface is not transparent OR
            // the depth write flag is set.
            if ((flags & (uint)ShaderFlags.TRANSPARENT) == 0
                || (flags & (uint)ShaderFlags.DEPTH_WRITE) != 0)
                depthState.DepthBufferWriteEnable = true;
            else
                depthState.DepthBufferWriteEnable = false;

            uint cullFlags = flags & (uint)ShaderFlags.CULL_FLAGS;

            if (cullFlags == (uint)ShaderFlags.CULL_BACK)
                mDevice.RasterizerState = RasterizerState.CullClockwise;
            else if (flags == (uint)ShaderFlags.CULL_FRONT)
                mDevice.RasterizerState = RasterizerState.CullClockwise;
            else
                mDevice.RasterizerState = RasterizerState.CullNone;

            mDevice.DepthStencilState = depthState;

            mLastRenderStateFlags = flags;
        }

        int shaderIdx = -1;
        bool BeginDrawCall(int callIdx)
        {
            if (mDrawCalls[callIdx].SortKey == DrawCall.INVALID_DRAW_CALL
                || !mDrawCalls[callIdx].Visible)
                return false;

            int drawIdx = mDrawCalls[callIdx].DrawDataIndex;

            if (drawIdx != mLastDrawIdx)
            {
                mDevice.SetVertexBuffer(mDrawData[drawIdx].VB);
                mDevice.Indices = mDrawData[drawIdx].IB;
                mLastDrawIdx = drawIdx;
            }

            BspShader shader = mShaders[mDrawCalls[callIdx].ShaderIndex];
            if (mDrawCalls[callIdx].ShaderIndex == shaderIdx)
                return true;
            shaderIdx = mDrawCalls[callIdx].ShaderIndex;

            UpdateRenderStateFlags(shader.mFlags);

            mCurrentEffect = mEffects[shader.mEffectIndex];
            mCurrentEffect.CurrentTechnique = mCurrentEffect.Techniques[0];

            int lightmapIndex = mDrawCalls[callIdx].LightMap;
            for (int i = 0; i < shader.mNumTextures; ++i)
            {
                int textureIndex = shader.mTextureIndices[i];
                mDevice.Textures[i] = (textureIndex != BspShader.LIGHTMAP) ? mTextures[textureIndex] : mLightMapTextures[lightmapIndex];
                SamplerState SS = new SamplerState();
                SS.AddressU = TextureAddressMode.Wrap;
                SS.AddressV = TextureAddressMode.Wrap;
                SS.AddressW = TextureAddressMode.Wrap;
                SS.Filter = TextureFilter.Anisotropic;
                mDevice.SamplerStates[i] = SS;
            }

            mCurrentEffect.Parameters["gTime"].SetValue(mTime);
            mCurrentEffect.Parameters["gWorld"].SetValue(Matrix.Identity);
            mCurrentEffect.Parameters["gViewProjection"].SetValue(mViewProjection);

            mCurrentEffect.CurrentTechnique.Passes[0].Apply();
            
            return true;
        }

        StringBuilder DebugText = new StringBuilder();

        void DrawFaces()
        {
            System.Diagnostics.Stopwatch SW = new Stopwatch();
            SW.Start();

            int numDrawCalls = mDrawCalls.Length;
            int numDrawn = 0;

            for (int i = 0; i < numDrawCalls; ++i)
            {
                int callIdx = mDrawCallIndices[i];

                if (!BeginDrawCall(callIdx))
                    break;

                mDevice.DrawIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    mDrawCalls[callIdx].FirstVertex,
                    0,
                    mDrawCalls[callIdx].NumIndices,
                    mDrawCalls[callIdx].StartIndex,
                    mDrawCalls[callIdx].PrimitiveCount);
                ++numDrawn;
            }

            SW.Stop();

            if (Split.Special)
                Debugger.Break();

            DebugText.Clear();
            Split.DebugText.Draw(DebugText.AppendFormat("BSP draw calls: {0} Time: {1} ms", numDrawn, SW.Elapsed.TotalMilliseconds));

            mLastDrawIdx = -1;
        }

        public void Render(Matrix viewProjection, float time)
        {
            mTime = time;

            mViewProjection = viewProjection;
            MarkAllFacesInvisible();
            UpdateVisible(FreeCam.Instance.Position);

            Array.Sort(mDrawCallIndices, 
                (a, b) => 
                { 
                    ulong sortKeyA = mDrawCalls[a].SortKey;
                    ulong sortKeyB = mDrawCalls[b].SortKey;
                    if (sortKeyA < sortKeyB) return -1;
                    else if (sortKeyA == sortKeyB) return 0;
                    else return 1;
                });

            DrawFaces();
        }

        public void UpdateVisible(Vector3 cameraPosition)
        {
            Leaf[] leafs = mBspTree.mLeafs;
            int leaf = mBspTree.FindLeafForPoint(cameraPosition);
            int cluster = leafs[leaf].Cluster;

            Split.DebugText.Draw("Leaf: {0}", leaf);
            /*
            for (int i = 0; i < leafs.Length; ++i)
            {
                if (leafs[i].Cluster >= 0 
                    && mVisData.IsVisible(cluster, leafs[i].Cluster))
                {
                    int[] leafFaces = mBspTree.mLeafFaces;
                    int begin = leafs[i].LeafFace;
                    int end = begin + leafs[i].NumLeafFaces;
                    for (int ii = begin; ii < end; ++ii)
                    {
                        int drawCallIdx = leafFaces[ii];
                        mDrawCalls[drawCallIdx].Visible = true;
                    }
                }
            }*/

            for (int i = 0; i < mDrawCalls.Length; ++i)
            {
                mDrawCalls[i].Visible = true;
                mDrawCalls[i].UpdateSortKey();
            }
        }

        #endregion
    }
}
