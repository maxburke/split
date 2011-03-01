using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace Split
{
    public class DebugText : IRenderable
    {
        SpriteFont Font;
        GraphicsDevice Device;
        SpriteBatch Batch;
        List<StringBuilder> Text = new List<StringBuilder>();

        public DebugText(ContentManager content, GraphicsDevice device)
        {
            Font = content.Load<SpriteFont>("consolas");
            Device = device;
            Batch = new SpriteBatch(Device);
        }

        public int RenderPriority
        {
            get { return int.MaxValue; }
        }

        public void Draw(StringBuilder str)
        {
            Text.Add(str);
        }

        public void Render(Matrix worldViewProjection, float gameTime)
        {
            if (Text.Count == 0)
                return;

            Batch.Begin();

            float y = 5;
            for (int i = 0; i < Text.Count; ++i)
            {
                Vector2 dimensions = Font.MeasureString(Text[i]);
                Batch.DrawString(Font, Text[i], new Vector2(5, y), Color.White);
                y += dimensions.Y + 5;
            }

            Text.Clear();

            Batch.End();
        }
    }
}
