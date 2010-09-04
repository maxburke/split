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
//            Vertex[] debugVerts = new Vertex[9];
            void TesselatePatches()
            {
//                for (int i = 0; i < 9; ++i)
//                    debugVerts[i] = B.Vertices[ControlPointIndices[i]];

                for (int i = 0; i < 9; ++i)
                    mVertices.Add(mBsp.Vertices[mControlPointIndices[i]]);

                mIndices.AddRange(new int[] { 1, 0, 3});
                mIndices.AddRange(new int[] { 1, 3, 4});
                mIndices.AddRange(new int[] { 2, 1, 5});
                mIndices.AddRange(new int[] { 5, 1, 4});

                mIndices.AddRange(new int[] { 4, 3, 6});
                mIndices.AddRange(new int[] { 7, 4, 6});
                mIndices.AddRange(new int[] { 5, 4, 8});
                mIndices.AddRange(new int[] { 8, 4, 7});

/*                
                Vertices.Add(B.Vertices[ControlPointIndices[0]]);
                Vertices.Add(B.Vertices[ControlPointIndices[2]]);
                Vertices.Add(B.Vertices[ControlPointIndices[6]]);
                Vertices.Add(B.Vertices[ControlPointIndices[8]]);

                Indices.Add(1); Indices.Add(0); Indices.Add(2);
                Indices.Add(2); Indices.Add(3); Indices.Add(1);
 */ 
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
//                        if (i == 56)
//                            Debugger.Break();
                        LoadControlPoints(mBsp.Faces[i], x, y);
                        TesselatePatches();
                        PD.NumIndices += 6;
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
            readonly public int SurfaceIndex;
            readonly public int LightMap;
            public int DrawDataIndex;
            public bool Visible;
            public ulong SortKey;

            Surface SurfaceData;

            public const ulong INVALID_DRAW_CALL = ulong.MaxValue;
            public const int UNUSED_DRAW_CALL = int.MaxValue;

            const int INVISIBLE_SHIFT = 63;
            const int TRANSLUCENT_SHIFT = 62;
            const int DRAWDATA_SHIFT = 54;
            const int LIGHTMAP_SHIFT = 27;
            const int SURFACE_SHIFT = 0;

            const uint DRAWDATA_MASK = 0xFF;
            const uint TEX_MASK = 0x07FFFFFF;

            public DrawCall(int firstVertex, int startIndex, int numIndices, int primitiveCount, int surfaceIndex, int lightMap, int drawDataIndex, Surface surface)
            {
                Valid = true;

                FirstVertex = firstVertex;
                StartIndex = startIndex;
                NumIndices = numIndices;
                PrimitiveCount = primitiveCount;
                SurfaceIndex = surfaceIndex;
                LightMap = lightMap;
                DrawDataIndex = drawDataIndex;
                SortKey = INVALID_DRAW_CALL;
                Visible = false;

                SurfaceData = surface;
            }

            public void CreateSortKey()
            {
                if (!Valid)
                {
                    SortKey = INVALID_DRAW_CALL;
                    return;
                }
                    
                // [invisible][translucent][draw data][lightmap][surface]
                SortKey = 0;

                if ((SurfaceData.Contents & Surface.CONTENTS_TRANSLUCENT) != 0)
                    SortKey |= 1ul << TRANSLUCENT_SHIFT;

                SortKey |= (ulong)((uint)DrawDataIndex & DRAWDATA_MASK) << DRAWDATA_SHIFT;
                SortKey |= (ulong)((uint)LightMap & TEX_MASK) << LIGHTMAP_SHIFT;
                SortKey |= (ulong)((uint)SurfaceIndex & TEX_MASK) << SURFACE_SHIFT;
            }

            public void UpdateSortKey()
            {
                if (!Valid)
                    return;

                if ((SurfaceData.Flags & Surface.SURF_NODRAW) != 0)
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
        Surface[] mSurfaces;
        Texture[] mLightMapTextures;
        DrawCall[] mDrawCalls;
        int[] mDrawCallIndices;
        VertexDeclaration mVertexDeclaration;
        Effect mBspShader;
        DrawCall mLastDrawCall;
        int mLastPassIdx;
        #endregion

        enum SurfaceType
        {
            MESH,
            PATCH,
            NUM_SURFACE_TYPES
        }

        public BspRenderer(Bsp b, GraphicsDevice device, ContentManager contentManager)
        {
            mBsp = b;
            mDevice = device;
            mBspShader = contentManager.Load<Effect>("bsp");

            mDrawCalls = new DrawCall[b.Faces.Length];
            for (int i = 0; i < mDrawCalls.Length; ++i)
                mDrawCalls[i].DrawDataIndex = DrawCall.UNUSED_DRAW_CALL;

            mDrawCallIndices = new int[b.Faces.Length];
            mVertexDeclaration = new VertexDeclaration(device, BspVertexFormat);

            for (int i = 0; i < mDrawCallIndices.Length; ++i)
                mDrawCallIndices[i] = i;

            CreateSurfaceTextures(contentManager);
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
        void CreateSurfaceTextures(ContentManager contentManager)
        {
            mSurfaces = mBsp.Textures;
            mBsp.Textures = null;

            for (int i = 0; i < mSurfaces.Length; ++i)
            {
                try
                {
                    mSurfaces[i].Texture = contentManager.Load<Texture2D>(mSurfaces[i].Name);
// enable this?
//                    mSurfaces[i].Texture.GenerateMipMaps(TextureFilter.Anisotropic);

#if DEBUG
                    Debug.WriteLine(string.Format("{0,3}: {1,8:X} {2,8:X} {3}", 
                        i, 
                        mSurfaces[i].Contents, 
                        mSurfaces[i].Flags,
                        mSurfaces[i].Name));
#else
                    BspSurfaces[i].Name = null;
#endif
                }
                catch (ContentLoadException)
                {
                    Debug.WriteLine(string.Format("{0,3}: {1,8:X} {2,8:X} {3} ** NOT LOADED **",
                        i,
                        mSurfaces[i].Contents,
                        mSurfaces[i].Flags,
                        mSurfaces[i].Name)); mSurfaces[i].Texture = null;
                }
            }
        }

        void CreateLightMapTextures()
        {
            int numLightMaps = mBsp.LightMaps.Length;
            Texture[] lightMaps = new Texture[numLightMaps];
            for (int i = 0; i < numLightMaps; ++i)
            {
                Texture2D LM = new Texture2D(mDevice, 128, 128, 1, TextureUsage.Linear, SurfaceFormat.Color);
                LM.SetData<Color>(mBsp.LightMaps[i].LightMap);
                lightMaps[i] = LM;
            }
            mBsp.LightMaps = null;
            mLightMapTextures = lightMaps;
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
                    mDrawCalls[i] = new DrawCall(
                        F.FirstVertex,
                        F.FirstMeshVert,
                        F.NumMeshVerts,
                        F.NumMeshVerts / 3,
                        F.Texture,
                        F.LightMapIndex,
                        (int)SurfaceType.MESH,
                        mSurfaces[F.Texture]);
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
                mDrawCalls[PD.FaceIndex] = new DrawCall(
                    PD.StartVertex,
                    PD.StartIndex,
                    PD.NumIndices,
                    PD.NumIndices / 3,
                    F.Texture,
                    F.LightMapIndex,
                    (int)SurfaceType.PATCH,
                    mSurfaces[i]);
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
            BoundingFrustum BF = new BoundingFrustum(ViewProjection);
            
            for (int i = 0; i < mDrawCalls.Length; ++i)
            {
                mDrawCalls[i].Visible = true;
                mDrawCalls[i].UpdateSortKey();
            }
        }

        void SetupDeviceForDrawing(int drawDataIndex, int passIndex, bool firstCall)
        {
            if (!firstCall)
            {
                mBspShader.CurrentTechnique.Passes[passIndex].End();
                mBspShader.End();
            }

            mDevice.Vertices[0].SetSource(mDrawData[drawDataIndex].VB, 0, BspVertexSize);
            mDevice.Indices = mDrawData[drawDataIndex].IB;

            mBspShader.Parameters["WorldViewProjection"].SetValue(ViewProjection);

            mBspShader.Begin();
            mBspShader.CurrentTechnique.Passes[passIndex].Begin();
        }

        public int ChangeToPass(int currentPassIndex, int newPassIndex)
        {
            if (currentPassIndex == newPassIndex)
                return newPassIndex;

            mBspShader.CurrentTechnique.Passes[currentPassIndex].End();
            mBspShader.CurrentTechnique.Passes[newPassIndex].Begin();
            return newPassIndex;
        }

        public bool UpdateRenderStateForDrawCall(int callIdx)
        {
            if (mDrawCalls[callIdx].SortKey == DrawCall.INVALID_DRAW_CALL)
                return false;

            if ((mSurfaces[mDrawCalls[callIdx].SurfaceIndex].Contents & Surface.CONTENTS_TRANSLUCENT) != 0)
                return false;

            bool stateChanged = false;

            int drawIdx = mDrawCalls[callIdx].DrawDataIndex;
            if (mDrawCalls[callIdx].DrawDataIndex != mLastDrawCall.DrawDataIndex)
            {
                stateChanged = true;
                SetupDeviceForDrawing(drawIdx, mLastPassIdx, false);
            }

            int surfaceIdx = mDrawCalls[callIdx].SurfaceIndex;
            if (surfaceIdx != mLastDrawCall.SurfaceIndex)
            {
                stateChanged = true;
                mDevice.Textures[0] = mSurfaces[surfaceIdx].Texture;
            }

            if (mDrawCalls[callIdx].LightMap != mLastDrawCall.LightMap)
            {
                stateChanged = true;
                int lightMapIdx = mDrawCalls[callIdx].LightMap;

                if (mDrawCalls[callIdx].LightMap >= 0)
                {
                    if (mLastPassIdx == 1)
                        mLastPassIdx = ChangeToPass(1, 0);

                    mDevice.Textures[1] = mLightMapTextures[lightMapIdx];
                }
                else
                {
                    mDevice.Textures[1] = null;
                    mLastPassIdx = ChangeToPass(0, 1);
                }
            }

            if (stateChanged)
                mLastDrawCall = mDrawCalls[callIdx];

            return true;
        }

        public void DrawFaces()
        {
            mBspShader.CurrentTechnique = mBspShader.Techniques[0];
            mDevice.VertexDeclaration = mVertexDeclaration;
            SetupDeviceForDrawing(mDrawCalls[mDrawCallIndices[0]].DrawDataIndex, mLastPassIdx, true);

            int numDrawCalls = mDrawCalls.Length;
            int i = 0;
            for (; i < numDrawCalls; ++i)
            {
                int callIdx = mDrawCallIndices[i];

                if (!UpdateRenderStateForDrawCall(callIdx))
                    break;

                mDevice.DrawIndexedPrimitives(
                    PrimitiveType.TriangleList,
                    mDrawCalls[callIdx].FirstVertex,
                    0,
                    mDrawCalls[callIdx].NumIndices,
                    mDrawCalls[callIdx].StartIndex,
                    mDrawCalls[callIdx].PrimitiveCount);
            }

            mBspShader.CurrentTechnique.Passes[mLastPassIdx].End();
            mBspShader.End();

            mLastPassIdx = 0;
        }

        Matrix ViewProjection;

        public void Render(Matrix viewProjection)
        {
            ViewProjection = viewProjection;

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
