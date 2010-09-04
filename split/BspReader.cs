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
    class BspReader : ContentTypeReader<Bsp>
    {
        protected override Bsp Read(ContentReader input, Bsp existingInstance)
        {
            Bsp bsp = new Bsp();
            bsp.Parse(input);
            return bsp;
        }
    }
}
