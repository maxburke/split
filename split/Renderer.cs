using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Split
{
    interface IRenderable
    {
        int RenderPriority { get; }
        void Render(Matrix worldViewProjection);
    }

    class Renderer
    {
        Matrix WorldViewProjection;
        List<IRenderable> Renderables = new List<IRenderable>();
        GraphicsDevice Device;

        public Renderer(GraphicsDevice device)
        {
            Device = device;
        }

        public void Register(IRenderable R)
        {
            Renderables.Add(R);
            Renderables.Sort((x, y) => { return x.RenderPriority - y.RenderPriority; });
        }

        public void SetWorldViewProjection(Matrix M)
        {
            WorldViewProjection = M;
        }

        public void Render()
        {
#if DEBUG
            Device.Clear(Color.Black);
#endif

            int numRenderables = Renderables.Count;
            for (int i = 0; i < numRenderables; ++i)
            {
                Renderables[i].Render(WorldViewProjection);
            }
        }
    }
}