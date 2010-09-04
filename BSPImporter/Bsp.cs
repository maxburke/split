using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Slim;

namespace Split.Pipeline
{
    public class Surface
    {
        #region Content flags
        public const uint CONTENTS_SOLID = 0x1;
        public const uint CONTENTS_LAVA = 0x8;
        public const uint CONTENTS_SLIME = 0x10;
        public const uint CONTENTS_WATER = 0x20;
        public const uint CONTENTS_FOG = 0x40;

        public const uint CONTENTS_NOTTEAM1 = 0x80;
        public const uint CONTENTS_NOTTEAM2 = 0x100;
        public const uint CONTENTS_NOBOTCLIP = 0x200;

        public const uint CONTENTS_AREAPORTAL = 0x8000;

        public const uint CONTENTS_PLAYERCLIP = 0x10000;
        public const uint CONTENTS_MONSTERCLIP = 0x20000;

        public const uint CONTENTS_TELEPORTER = 0x40000;
        public const uint CONTENTS_JUMPPAD = 0x80000;
        public const uint CONTENTS_CLUSTERPORTAL = 0x100000;
        public const uint CONTENTS_DONOTENTER = 0x200000;
        public const uint CONTENTS_BOTCLIP = 0x400000;
        public const uint CONTENTS_MOVER = 0x800000;

        public const uint CONTENTS_ORIGIN = 0x1000000;

        public const uint CONTENTS_BODY = 0x2000000;
        public const uint CONTENTS_CORPSE = 0x4000000;
        public const uint CONTENTS_DETAIL = 0x8000000;
        public const uint CONTENTS_STRUCTURAL = 0x10000000;
        public const uint CONTENTS_TRANSLUCENT = 0x20000000;
        public const uint CONTENTS_TRIGGER = 0x40000000;
        public const uint CONTENTS_NODROP = 0x80000000;
        #endregion

        #region Surface flags
        public const uint SURF_NODAMAGE = 0x1;
        public const uint SURF_SLICK = 0x2;
        public const uint SURF_SKY = 0x4;
        public const uint SURF_LADDER = 0x8;
        public const uint SURF_NOIMPACT = 0x10;
        public const uint SURF_NOMARKS = 0x20;
        public const uint SURF_FLESH = 0x40;
        public const uint SURF_NODRAW = 0x80;
        public const uint SURF_HINT = 0x100;
        public const uint SURF_SKIP = 0x200;
        public const uint SURF_NOLIGHTMAP = 0x400;
        public const uint SURF_POINTLIGHT = 0x800;
        public const uint SURF_METALSTEPS = 0x1000;
        public const uint SURF_NOSTEPS = 0x2000;
        public const uint SURF_NONSOLID = 0x4000;
        public const uint SURF_LIGHTFILTER = 0x8000;
        public const uint SURF_ALPHASHADOW = 0x10000;
        public const uint SURF_NODLIGHT = 0x20000;
        public const uint SURF_DUST = 0x40000;
        #endregion

        public string Name;
        public readonly uint Flags;
        public readonly uint Contents;
        public Texture Texture;

        public Surface(EndianReader ER)
        {
            Name = ER.ReadCString(64);
            Flags = (uint)ER.ReadI4();
            Contents = (uint)ER.ReadI4();
        }

        public override string ToString()
        {
            return Name;
        }
    }


    public struct Node
    {
        public readonly int Plane;
        public readonly Pair<int, int> Children;
        public readonly BoundingBox NodeBounds;

        public Node(EndianReader ER)
        {
            Plane = ER.ReadI4();
            Children = ER.ReadIntPair();
            IntVector3 mins = ER.ReadIntVector3();
            IntVector3 maxs = ER.ReadIntVector3();
            Vector3 minsV3 = new Vector3((float)mins.X, (float)mins.Y, (float)mins.Z);
            Vector3 maxsV3 = new Vector3((float)maxs.X, (float)maxs.Y, (float)maxs.Z);
            NodeBounds = new BoundingBox(minsV3, maxsV3);
        }
    }

    public struct Leaf
    {
        public readonly int Cluster;
        public readonly int Area;
        public readonly BoundingBox LeafBounds;
        public readonly int LeafFace;
        public readonly int NumLeafFaces;
        public readonly int LeafBrush;
        public readonly int NumLeafBrushes;

        public Leaf(EndianReader ER)
        {
            Cluster = ER.ReadI4();
            Area = ER.ReadI4();
            IntVector3 mins = ER.ReadIntVector3();
            IntVector3 maxs = ER.ReadIntVector3();
            Vector3 minsV3 = new Vector3((float)mins.X, (float)mins.Y, (float)mins.Z);
            Vector3 maxsV3 = new Vector3((float)maxs.X, (float)maxs.Y, (float)maxs.Z);
            LeafBounds = new BoundingBox(minsV3, maxsV3);
            LeafFace = ER.ReadI4();
            NumLeafFaces = ER.ReadI4();
            LeafBrush = ER.ReadI4();
            NumLeafBrushes = ER.ReadI4();
        }
    }

    public struct Model
    {
        public readonly BoundingBox Bounds;
        public readonly int FirstFace;
        public readonly int NumFaces;
        public readonly int FirstBrush;
        public readonly int NumBrushes;

        public Model(EndianReader ER)
        {
            Vector3 boxMin = ER.ReadV3();
            Vector3 boxMax = ER.ReadV3();
            Bounds = new BoundingBox(boxMin, boxMax);
            FirstFace = ER.ReadI4();
            NumFaces = ER.ReadI4();
            FirstBrush = ER.ReadI4();
            NumBrushes = ER.ReadI4();
        }
    }

    public struct Brush
    {
        public readonly int FirstBrushSide;
        public readonly int NumBrushSides;
        public readonly int Texture;

        public Brush(EndianReader ER)
        {
            FirstBrushSide = ER.ReadI4();
            NumBrushSides = ER.ReadI4();
            Texture = ER.ReadI4();
        }
    }

    public struct BrushSide
    {
        public readonly int Plane;
        public readonly int Texture;

        public BrushSide(EndianReader ER)
        {
            Plane = ER.ReadI4();
            Texture = ER.ReadI4();
        }
    }

    public struct Vertex
    {
        public readonly Vector4 Position;
        public readonly Vector2 SurfaceTexCoord;
        public readonly Vector2 LightMapTexCoord;
        public readonly Vector3 Normal;
        public readonly Color VertexColor;

        public Vertex(Vector4 position, Vector2 surfaceST, Vector2 lightmapST, Vector3 normal, Color color)
        {
            Position = position;
            SurfaceTexCoord = surfaceST;
            LightMapTexCoord = lightmapST;
            Normal = normal;
            VertexColor = color;
        }

        public Vertex(EndianReader ER)
        {
            Position = new Vector4(ER.ReadV3(), 1.0f);
            SurfaceTexCoord = ER.ReadV2();
            LightMapTexCoord = ER.ReadV2();
            Normal = ER.ReadV3();
            VertexColor = ER.ReadRGBA();
        }

        public override string ToString()
        {
            return string.Format("Position: {0}", Position);
        }
    }

    public struct MeshVert
    {
        public readonly int Offset;

        public MeshVert(EndianReader ER)
        {
            Offset = ER.ReadI4();
        }
    }

    public struct Fog
    {
        public readonly string Name;
        public readonly int Brush;
        public readonly int VisibleSide;

        public Fog(EndianReader ER)
        {
            Name = ER.ReadCString(64);
            Brush = ER.ReadI4();
            VisibleSide = ER.ReadI4();
        }
    }

    public class Face
    {
        public enum FaceType
        {
            Polygon = 1,
            Patch = 2,
            Mesh = 3,
            Billboard = 4
        }

        public readonly int Texture;
        public readonly int Effect;
        public readonly FaceType Type;
        public readonly int FirstVertex;
        public readonly int NumVertices;
        public readonly int FirstMeshVert;
        public readonly int NumMeshVerts;
        public readonly int LightMapIndex;
        public readonly Pair<int, int> LightMapStart;
        public readonly Pair<int, int> LightMapSize;
        public readonly Vector3 LightMapOrigin;
        public readonly Pair<Vector3, Vector3> LightMapST;
        public readonly Vector3 Normal;
        public readonly Pair<int, int> Size;

        public Face(EndianReader ER)
        {
            Texture = ER.ReadI4();
            Effect = ER.ReadI4();
            Type = (FaceType)ER.ReadI4();
            FirstVertex = ER.ReadI4();
            NumVertices = ER.ReadI4();
            FirstMeshVert = ER.ReadI4();
            NumMeshVerts = ER.ReadI4();
            LightMapIndex = ER.ReadI4();
            LightMapStart = ER.ReadIntPair();
            LightMapSize = ER.ReadIntPair();
            LightMapOrigin = ER.ReadV3();

            Vector3 lmS = ER.ReadV3();
            Vector3 lmT = ER.ReadV3();
            LightMapST = new Pair<Vector3, Vector3>(lmS, lmT);

            Normal = ER.ReadV3();
            Size = ER.ReadIntPair();
        }
    }

    public struct LightMapData
    {
        public readonly Color[] LightMap;

        public LightMapData(EndianReader ER)
        {
            LightMap = new Color[128 * 128];

            for (int i = 0; i < 128 * 128; ++i)
                LightMap[i] = ER.ReadRGB();
        }
    }

    public struct LightVol
    {
        public readonly Color Ambient;
        public readonly Color Directional;
        public readonly Vector2 Direction;

        public LightVol(EndianReader ER)
        {
            Ambient = ER.ReadRGB();
            Directional = ER.ReadRGB();
            byte phi = ER.ReadI1();
            byte theta = ER.ReadI1();
            Direction = new Vector2((float)phi, (float)theta);
        }
    }

    struct VisData
    {
        public readonly int NumVectors;
        public readonly int VectorSize;
        public readonly byte[] Vectors;

        public VisData(EndianReader ER)
        {
            NumVectors = ER.ReadI4();
            VectorSize = ER.ReadI4();
            int vectorBytes = NumVectors * VectorSize;
            Vectors = new byte[vectorBytes];

            for (int i = 0; i < vectorBytes; ++i)
                Vectors[i] = ER.ReadI1();
        }
    }

    public class Bsp
    {
        public const int TargetVersion = 0x2e;
        public const int Cookie = 0x50534249;

        public enum LumpType
        {
            Entities,
            Textures,
            Planes,
            Nodes,
            Leaves,
            LeafFaces,
            LeafBrushes,
            Models,
            Brushes,
            BrushSides,
            Vertices,
            MeshVerts,
            Effects,
            Faces,
            LightMaps,
            LightVols,
            VisData,
            NumLumps
        }

        private static int[] LumpSizes = {
            -1,    // Entities
            72,    // Textures
            16,    // Planes
            36,    // Nodes
            48,    // Leaves
            4,     // LeafFaces
            4,     // LeafBrushes
            40,    // Models
            12,    // Brushes
            8,     // BrushSides
            44,    // Vertices
            4,     // MeshVerts
            72,    // Effects
            104,   // Faces
            3 * 128 * 128,    // LightMaps
            8,     // LightVols
            1,    // VisData
        };

        Pair<int, int>[] LumpDirectory = new Pair<int,int>[(int)LumpType.NumLumps];

        Surface[] BspSurfaceData;
        Plane[] BspPlaneData;
        Node[] BspNodeData;
        Leaf[] BspLeafData;
        int[] BspLeafFaceData;
        int[] BspLeafBrushData;
        Model[] BspModelData;
        Brush[] BspBrushData;
        BrushSide[] BspBrushSideData;
        Vertex[] BspVertexData;
        int[] BspMeshVertData;
        Fog[] BspEffectData;
        Face[] BspFaceData;
        LightMapData[] BspLightMapData;
        LightVol[] BspLightVolData;
        VisData BspVisData;

        public Surface[] Textures { get { return BspSurfaceData; } set { BspSurfaceData = value; } }
        public Plane[] Planes { get { return BspPlaneData; } }
        public Node[] Nodes { get { return BspNodeData; } }
        public Leaf[] Leafs { get { return BspLeafData; } }
        public int[] LeafFaces { get { return BspLeafFaceData; } }
        public int[] LeafBrushes { get { return BspLeafBrushData; } }
        public Model[] Models { get { return BspModelData; } }
        public Brush[] Brushes { get { return BspBrushData; } }
        public BrushSide[] BrushSides { get { return BspBrushSideData; } }
        public Vertex[] Vertices { get { return BspVertexData; } set { BspVertexData = value; } }
        public int[] MeshVerts { get { return BspMeshVertData; } set { BspMeshVertData = value; } }
        public Fog[] Effects { get { return BspEffectData; } }
        public Face[] Faces { get { return BspFaceData; } }
        public LightMapData[] LightMaps { get { return BspLightMapData; } set { BspLightMapData = value; } }
        public LightVol[] LightVols { get { return BspLightVolData; } }

        EndianReader Reader;
        long ByteOffset;

        void Seek(long position)
        {
            position = (position + 3) & ~3;
            Reader.BaseStream.BaseStream.Seek(position + ByteOffset, SeekOrigin.Begin);
        }

        void ReadLumpDirectory()
        {
            for (int i = 0; i < (int)LumpType.NumLumps; ++i)
            {
                int offset = Reader.ReadI4();
                int length = Reader.ReadI4();
                Pair<int, int> lump = new Pair<int, int>(offset, length);
                LumpDirectory[i] = lump;
            }
        }

        int FetchNumEntriesAndSeek(LumpType lump)
        {
            int lumpIndex = (int)lump;
            int lumpSize = LumpSizes[lumpIndex];
            Debug.Assert(lumpSize > 0);

            Pair<int, int> lumpData = LumpDirectory[lumpIndex];
            int numEntries = lumpData.second / lumpSize;

            Seek(lumpData.first);

            return numEntries;
        }

        void ReadEntities()
        {
        }

        void ReadTextures()
        {
            int numTextures = FetchNumEntriesAndSeek(LumpType.Textures);
            BspSurfaceData = new Surface[numTextures];

            for (int i = 0; i < numTextures; ++i)
                BspSurfaceData[i] = new Surface(Reader);
        }

        void ReadPlanes()
        {
            int numPlanes = FetchNumEntriesAndSeek(LumpType.Planes);
            BspPlaneData = new Plane[numPlanes];

            for (int i = 0; i < numPlanes; ++i)
            {
                Vector3 normal = Reader.ReadV3();
                float distance = Reader.ReadF4();
                BspPlaneData[i] = new Plane(normal, distance);
            }
        }

        void ReadNodes()
        {
            int numNodes = FetchNumEntriesAndSeek(LumpType.Nodes);
            BspNodeData = new Node[numNodes];

            for (int i = 0; i < numNodes; ++i)
                BspNodeData[i] = new Node(Reader);
        }

        void ReadLeaves()
        {
            int numLeaves = FetchNumEntriesAndSeek(LumpType.Leaves);
            BspLeafData = new Leaf[numLeaves];

            for (int i = 0; i < numLeaves; ++i)
                BspLeafData[i] = new Leaf(Reader);
        }

        void ReadLeafFaces()
        {
            int numLeafFaces = FetchNumEntriesAndSeek(LumpType.LeafFaces);
            BspLeafFaceData = new int[numLeafFaces];

            for (int i = 0; i < numLeafFaces; ++i)
                BspLeafFaceData[i] = Reader.ReadI4();
        }

        void ReadLeafBrushes()
        {
            int numLeafBrushes = FetchNumEntriesAndSeek(LumpType.LeafBrushes);
            BspLeafBrushData = new int[numLeafBrushes];

            for (int i = 0; i < numLeafBrushes; ++i)
                BspLeafBrushData[i] = Reader.ReadI4();
        }

        void ReadModels()
        {
            int numModels = FetchNumEntriesAndSeek(LumpType.Models);
            BspModelData = new Model[numModels];

            for (int i = 0; i < numModels; ++i)
                BspModelData[i] = new Model(Reader);
        }

        void ReadBrushes()
        {
            int numBrushes = FetchNumEntriesAndSeek(LumpType.Brushes);
            BspBrushData = new Brush[numBrushes];

            for (int i = 0; i < numBrushes; ++i)
                BspBrushData[i] = new Brush(Reader);
        }

        void ReadBrushSides()
        {
            int numBrushSides = FetchNumEntriesAndSeek(LumpType.BrushSides);
            BspBrushSideData = new BrushSide[numBrushSides];

            for (int i = 0; i < numBrushSides; ++i)
                BspBrushSideData[i] = new BrushSide(Reader);
        }

        void ReadVertices()
        {
            int numVertices = FetchNumEntriesAndSeek(LumpType.Vertices);
            BspVertexData = new Vertex[numVertices];

            for (int i = 0; i < numVertices; ++i)
                BspVertexData[i] = new Vertex(Reader);
        }

        void ReadMeshVerts()
        {
            int numMeshVerts = FetchNumEntriesAndSeek(LumpType.MeshVerts);
            BspMeshVertData = new int[numMeshVerts];

            for (int i = 0; i < numMeshVerts; ++i)
                BspMeshVertData[i] = Reader.ReadI4();
        }

        void ReadEffects()
        {
            int numEffects = FetchNumEntriesAndSeek(LumpType.Effects);
            BspEffectData = new Fog[numEffects];

            for (int i = 0; i < numEffects; ++i)
                BspEffectData[i] = new Fog(Reader);
        }

        void ReadFaces()
        {
            int numFaces = FetchNumEntriesAndSeek(LumpType.Faces);
            BspFaceData = new Face[numFaces];

            for (int i = 0; i < numFaces; ++i)
                BspFaceData[i] = new Face(Reader);
        }

        void ReadLightMaps()
        {
            int numLightMaps = FetchNumEntriesAndSeek(LumpType.LightMaps);
            BspLightMapData = new LightMapData[numLightMaps];

            for (int i = 0; i < numLightMaps; ++i)
                BspLightMapData[i] = new LightMapData(Reader);
        }

        void ReadLightVols()
        {
            int numLightVols = FetchNumEntriesAndSeek(LumpType.LightVols);
            BspLightVolData = new LightVol[numLightVols];

            for (int i = 0; i < numLightVols; ++i)
                BspLightVolData[i] = new LightVol(Reader);
        }

        void ReadVisData()
        {
            FetchNumEntriesAndSeek(LumpType.VisData);
            BspVisData = new VisData(Reader);
        }
        
        public void Parse(BinaryReader BR)
        {
            ByteOffset = BR.BaseStream.Position;
            Reader = BR.ReadInt32() == Cookie
                ? (EndianReader)new LittleEndianReader(BR)
                : new BigEndianReader(BR);
            int version = BR.ReadInt32();
            Debug.Assert(version == TargetVersion);

            ReadLumpDirectory();

            ReadEntities();
            ReadTextures();
            ReadPlanes();
            ReadNodes();
            ReadLeaves();
            ReadLeafFaces();
            ReadLeafBrushes();
            ReadModels();
            ReadBrushes();
            ReadBrushSides();
            ReadVertices();
            ReadMeshVerts();
            ReadEffects();
            ReadFaces();
            ReadLightMaps();
            ReadLightVols();
            ReadVisData();
        }
    }
}
