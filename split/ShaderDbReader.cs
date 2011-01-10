using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Split.Pipeline;
using Slim;

namespace Split
{
    class ShaderDbReader : ContentTypeReader<ShaderDb>
    {
        protected override ShaderDb Read(ContentReader input, ShaderDb existingInstance)
        {
            ShaderDb shaderDb = new ShaderDb();
            shaderDb.Parse(input);
            return shaderDb;
        }
    }
}
