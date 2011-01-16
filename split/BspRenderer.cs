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

            public PatchTesselator(Bsp b, GraphicsDevice device)
            {
                mBsp = b;

                for (int i = 0; i < mBsp.Faces.Length; ++i)
                    if (mBsp.Faces[i].Type == Face.FaceType.Patch)
                        TesselateFace(i);

                if (mVertices.Count == 0)
                    return;

                mVertexBuffer = new VertexBuffer(device, BspVertexSize * mVertices.Count, BufferUsage.WriteOnly);
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
            const int TRANSPARENT_SHIFT = 62;
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

                // Commenting out this bit of transparency sorting for now as this
                // should be handled correctly once we sort geometry.
                if ((Instance.mShaders[ShaderIndex].mFlags & (int)ShaderFlags.TRANSPARENT) != 0)
                    SortKey |= 1ul << TRANSPARENT_SHIFT;

                SortKey |= (ulong)((uint)DrawDataIndex & DRAWDATA_MASK) << DRAWDATA_SHIFT;
                SortKey |= (ulong)((uint)LightMap & TEX_MASK) << LIGHTMAP_SHIFT;
                SortKey |= (ulong)((uint)ShaderIndex & TEX_MASK) << SURFACE_SHIFT;
            }

            public void UpdateSortKey()
            {
                if (!Valid)
                    return;

                if ((Instance.mShaders[ShaderIndex].mFlags & (int)ShaderFlags.NODRAW) != 0)
                    Visible = false;

                const ulong INVISIBLE_BIT = 1ul << INVISIBLE_SHIFT;
                SortKey = Visible ? SortKey & (~INVISIBLE_BIT) : SortKey | INVISIBLE_BIT;
            }
        }
        #endregion

        #region Member data
        Bsp mBsp;
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
        #endregion

        static BspRenderer Instance;

        enum SurfaceType
        {
            MESH,
            PATCH,
            NUM_SURFACE_TYPES
        }

        public BspRenderer(Bsp b, GraphicsDevice device, ContentManager contentManager, ShaderDb shaderDb)
        {
            Instance = this;

            mBsp = b;
            mDevice = device;

            mDrawCalls = new DrawCall[b.Faces.Length];
            for (int i = 0; i < mDrawCalls.Length; ++i)
                mDrawCalls[i].DrawDataIndex = DrawCall.UNUSED_DRAW_CALL;

            mDrawCallIndices = new int[b.Faces.Length];
            mVertexDeclaration = new VertexDeclaration(device, BspVertexFormat);

            for (int i = 0; i < mDrawCallIndices.Length; ++i)
                mDrawCallIndices[i] = i;

            CreateSurfaceTextures(contentManager, shaderDb);
            CreateLightMapTextures();

            CreatePatchDrawData();

            CreateMeshDrawData();
            CreateMeshDrawCalls();

            for (int i = 0; i < mDrawCalls.Length; ++i)
                mDrawCalls[i].CreateSortKey();
        }

        #region Vertex data

        static int BspVertexSize { get { unsafe { return sizeof(Vertex); } } }

        static VertexElement[] BspVertexFormat { get { return mBspVertexFormat; } }

        static VertexElement[] mBspVertexFormat =
        {
            new VertexElement(0, 0, VertexElementFormat.Vector4, VertexElementMethod.Default, VertexElementUsage.Position, 0),
            new VertexElement(0, 16, VertexElementFormat.Vector2, VertexElementMethod.Default, VertexElementUsage.TextureCoordinate, 0),
            new VertexElement(0, 24, VertexElementFormat.Vector2, VertexElementMethod.Default, VertexElementUsage.TextureCoordinate, 1),
            new VertexElement(0, 32, VertexElementFormat.Vector3, VertexElementMethod.Default, VertexElementUsage.Normal, 0),
            new VertexElement(0, 44, VertexElementFormat.Color, VertexElementMethod.Default, VertexElementUsage.Color, 0)
        };

        #endregion

        public int RenderPriority { get { return 1; } }

        #region Texture prep code
#if WINDOWS
        TargetPlatform mTargetPlatform = TargetPlatform.Windows;
#endif

#if XBOX360
        TargetPlatform mTargetPlatform = TargetPlatform.Xbox360;
#endif

#if ZUNE
        TargetPlatform mTargetPlatform = TargetPlatform.Zune;
#endif

        int GetLoadedTextureIndex(int texIndex,
            ContentManager contentManager,
            ShaderDb shaderDb)
        {
            string textureName = shaderDb.mTextureNames[texIndex];

            int idx;
            if (textureName == "$lightmap")
            {
                idx = BspShader.LIGHTMAP;
            }
            else
            {
                idx = mTextures.Count;

                Texture2D texture = contentManager.Load<Texture2D>(textureName);
                texture.GenerateMipMaps(TextureFilter.Anisotropic);
                mTextures.Add(texture);
            }
            Debug.WriteLine(string.Format("    {0} {1}", idx, textureName));

            return idx;
        }

        void CreateSurfaceTextures(ContentManager contentManager, ShaderDb shaderDb)
        {
            List<Effect> effects = new List<Effect>();

            Effect defaultShader = contentManager.Load<Effect>("bsp");
            effects.Add(defaultShader);

            int[] shaderIdxToEffectIdx = new int[shaderDb.NumTextShaders()];

            int numSurfaces = mBsp.Textures.Length;
            mShaders = new BspShader[numSurfaces];

            for (int i = 0; i < numSurfaces; ++i)
            {
                Surface sfc = mBsp.Textures[i];

                GeneratedShader GS = shaderDb.Find(sfc.Name);
                if (GS != null)
                {
                    int effectIdx = shaderIdxToEffectIdx[GS.mShaderTextIndex];
                    Debug.WriteLine(string.Format("{0} Using SWEET AWESOME CUSTOM shader for {1}", i, sfc.Name));

                    if (effectIdx == 0)
                    {
                        CompiledEffect CE = Effect.CompileEffectFromSource(
                            shaderDb.mShaderText[GS.mShaderTextIndex],
                            null,
                            null,
                            CompilerOptions.Debug,
                            mTargetPlatform);
                        Effect E = new Effect(mDevice, CE.GetEffectCode(), CompilerOptions.Debug, null);
                        shaderIdxToEffectIdx[GS.mShaderTextIndex] = effects.Count;
                        effectIdx = effects.Count;
                        effects.Add(E);
                    }

                    BspShader B = new BspShader(effectIdx, GS.mNumTextures);
                    B.mFlags = GS.mFlags;

                    if ((sfc.Flags & Surface.SURF_NODRAW) != 0)
                        B.mFlags |= (int)ShaderFlags.NODRAW;

                    B.mName = GS.mName;

                    foreach (int texIndex in GS.mTextureIndices)
                        B.AddIndex(
                            GetLoadedTextureIndex(texIndex, contentManager, shaderDb));

                    mShaders[i] = B;
                }
                else
                {
                    // Use a default shader.
                    try
                    {
                        Texture2D tex = contentManager.Load<Texture2D>(sfc.Name);
                        int idx = mTextures.Count;
                        mTextures.Add(tex);
                        mShaders[i] = new BspShader(0, 2, idx, BspShader.LIGHTMAP);
                        mShaders[i].mName = sfc.Name;

                        Debug.WriteLine(string.Format("{0} Using default shader for {1} {2}", i, sfc.Name, idx));
                    }
                    catch
                    {
                        mTextures.Add(null);
                        Debug.WriteLine(string.Format("{0} Unable to load texture {1}!", i, sfc.Name));
                    }
                }
            }

            mEffects = effects.ToArray();
        }

        void CreateLightMapTextures()
        {
            int numLightMaps = mBsp.LightMaps.Length;
            Texture2D[] lightMaps = new Texture2D[numLightMaps + 1];
            for (int i = 0; i < numLightMaps; ++i)
            {
                Texture2D LM = new Texture2D(mDevice, 128, 128, 1, TextureUsage.Linear, SurfaceFormat.Color);
                LM.SetData<Color>(mBsp.LightMaps[i].LightMap);
                lightMaps[i] = LM;
                LM.GenerateMipMaps(TextureFilter.Linear);
            }
            mBsp.LightMaps = null;
            mLightMapTextures = lightMaps;

            Texture2D defaultLightMap = new Texture2D(mDevice, 1, 1, 1, TextureUsage.Linear, SurfaceFormat.Color);
            Color[] defaultLightMapColor = new Color[1];
            defaultLightMapColor[0] = new Color(1.0f, 1.0f, 1.0f, 1.0f);
            defaultLightMap.SetData<Color>(defaultLightMapColor);
            lightMaps[lightMaps.Length - 1] = defaultLightMap;
        }
        #endregion

        #region Mesh drawing prep code
        void CreateMeshDrawCalls()
        {
            for (int i = 0; i < mBsp.Faces.Length; ++i)
            {
                Face F = mBsp.Faces[i];
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

        void CreateMeshDrawData()
        {
            VertexBuffer VB = new VertexBuffer(mDevice, BspVertexSize * mBsp.Vertices.Length, BufferUsage.WriteOnly);
            VB.SetData<Vertex>(mBsp.Vertices);
            mBsp.Vertices = null;

            IndexBuffer IB = new IndexBuffer(mDevice, typeof(int), mBsp.MeshVerts.Length, BufferUsage.WriteOnly);
            IB.SetData<int>(mBsp.MeshVerts);
            mBsp.MeshVerts = null;

            mDrawData[(int)SurfaceType.MESH] = new DrawData(VB, IB);
        }
        #endregion
        
        #region Patch drawing prep code
        void CreatePatchDrawData()
        {
            /// TODO: Calculate the number of verts we create during tesselation
            /// and create an array of that size.
            PatchTesselator PT = new PatchTesselator(mBsp, mDevice);
            mDrawData[(int)SurfaceType.PATCH] = new DrawData(PT.VertexBuffer, PT.IndexBuffer);

            int numPatches = PT.PatchDrawData.Count;
            for (int i = 0; i < numPatches; ++i)
            {
                PatchTesselator.PatchData PD = PT.PatchDrawData[i];
                Face F = mBsp.Faces[i];
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
        public void MarkAllFacesInvisible()
        {
            for (int i = 0; i < mDrawCalls.Length; ++i)
                mDrawCalls[i].Visible = false;
        }

        public void DetermineVisibility()
        {
            /// TODO: make this actually determine visibility.
            BoundingFrustum BF = new BoundingFrustum(mViewProjection);
            
            for (int i = 0; i < mDrawCalls.Length; ++i)
            {
                mDrawCalls[i].Visible = true;
                mDrawCalls[i].UpdateSortKey();
            }
        }

        public void InitializeDevice()
        {
            mDevice.RenderState.AlphaBlendEnable = true;
            mDevice.RenderState.SourceBlend = Blend.SourceAlpha;
            mDevice.RenderState.DestinationBlend = Blend.InverseSourceAlpha;
        }

        void UpdateRenderStateFlags(int flags)
        {
            if ((flags & (int)ShaderFlags.DEPTH_EQUAL) != 0)
                mDevice.RenderState.DepthBufferFunction = CompareFunction.Equal;
            else
                mDevice.RenderState.DepthBufferFunction = CompareFunction.LessEqual;

            // Depth buffer writes are enabled if the surface is not transparent OR
            // the depth write flag is set.
            mDevice.RenderState.DepthBufferWriteEnable = 
                (flags & (int)ShaderFlags.TRANSPARENT) == 0
                || (flags & (int)ShaderFlags.DEPTH_WRITE) != 0;

            if ((flags & (int)ShaderFlags.CULL_BACK) != 0)
                mDevice.RenderState.CullMode = CullMode.CullClockwiseFace;
            else if ((flags & (int)ShaderFlags.CULL_BACK) != 0)
                mDevice.RenderState.CullMode = CullMode.None;
            else
                mDevice.RenderState.CullMode = CullMode.CullCounterClockwiseFace;
        }

        int shaderIdx = -1;
        bool BeginDrawCall(int callIdx)
        {
            if (mDrawCalls[callIdx].SortKey == DrawCall.INVALID_DRAW_CALL)
                return false;

            int drawIdx = mDrawCalls[callIdx].DrawDataIndex;

            if (drawIdx != mLastDrawIdx)
            {
                mDevice.Vertices[0].SetSource(mDrawData[drawIdx].VB, 0, BspVertexSize);
                mDevice.Indices = mDrawData[drawIdx].IB;
                mLastDrawIdx = drawIdx;
            }

            BspShader shader = mShaders[mDrawCalls[callIdx].ShaderIndex];
            if (mDrawCalls[callIdx].ShaderIndex == shaderIdx)
                return true;
            shaderIdx = mDrawCalls[callIdx].ShaderIndex;

            if (mCurrentEffect != null)
            {
                mCurrentEffect.CurrentTechnique.Passes[0].End();
                mCurrentEffect.End();
            }

            UpdateRenderStateFlags(shader.mFlags);

            mCurrentEffect = mEffects[shader.mEffectIndex];
            mCurrentEffect.CurrentTechnique = mCurrentEffect.Techniques[0];

            int lightmapIndex = mDrawCalls[callIdx].LightMap;
            for (int i = 0; i < shader.mNumTextures; ++i)
            {
                int textureIndex = shader.mTextureIndices[i];
                mDevice.Textures[i] = (textureIndex != BspShader.LIGHTMAP) ? mTextures[textureIndex] : mLightMapTextures[lightmapIndex];

                mDevice.SamplerStates[i].MagFilter = TextureFilter.Anisotropic;
                mDevice.SamplerStates[i].MinFilter = TextureFilter.Anisotropic;
                mDevice.SamplerStates[i].MipFilter = TextureFilter.Linear;
            }

            mCurrentEffect.Parameters["gTime"].SetValue(mTime);
            mCurrentEffect.Parameters["gWorld"].SetValue(Matrix.Identity);
            mCurrentEffect.Parameters["gViewProjection"].SetValue(mViewProjection);

            mCurrentEffect.Begin();
            mCurrentEffect.CurrentTechnique.Passes[0].Begin();
            
            return true;
        }

        public void DrawFaces()
        {
            mDevice.VertexDeclaration = mVertexDeclaration;

            int numDrawCalls = mDrawCalls.Length;
            int i = 0;
            for (; i < numDrawCalls; ++i)
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
            }

            mCurrentEffect.CurrentTechnique.Passes[0].End();
            mCurrentEffect.End();
            mCurrentEffect = null;
            mLastDrawIdx = -1;
        }

        public void Render(Matrix viewProjection, float time)
        {
            InitializeDevice();
            mTime = time;

            mViewProjection = viewProjection;

            MarkAllFacesInvisible();
            DetermineVisibility();
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
        #endregion
    }
}
