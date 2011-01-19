using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Split.Pipeline;

namespace Split
{
    class BspTree
    {
        public readonly Node[] mNodes;
        public readonly Leaf[] mLeafs;
        public readonly int[] mLeafFaces;
        public readonly int[] mLeafBrushes;
        public readonly Plane[] mPlanes;

        public BspTree(Bsp bsp)
        {
            mNodes = bsp.Nodes;
            mLeafs = bsp.Leafs;
            mLeafFaces = bsp.LeafFaces;
            mLeafBrushes = bsp.LeafBrushes;
            mPlanes = bsp.Planes;

            bsp.Nodes = null;
            bsp.Leafs = null;
            bsp.LeafFaces = null;
            bsp.LeafBrushes = null;
            bsp.Planes = null;
        }

        public int FindLeafForPoint(Vector3 point)
        {
            int i = 0;

            for (; ; )
            {
                int leftIdx = mNodes[i].Children.first;
                int rightIdx = mNodes[i].Children.second;
                Plane plane = mPlanes[mNodes[i].Plane];

                float dotCoordinate = plane.DotNormal(point) - plane.D;

                if (dotCoordinate >= 0)
                {
                    if (leftIdx < 0)
                        return ~leftIdx;

                    Debug.Assert(mNodes[i].NodeBounds.Contains(point) == ContainmentType.Contains);
                    i = leftIdx;
                }
                else
                {
                    if (rightIdx < 0)
                        return ~rightIdx;

                    Debug.Assert(mNodes[i].NodeBounds.Contains(point) == ContainmentType.Contains);
                    i = rightIdx;
                }
            }
        }
    }
}
