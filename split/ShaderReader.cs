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
    class CompiledShader
    {
        public uint mFlags;
        public byte[] mEffectCode;
        public string[] mTextures;
    }

    class ShaderReader : ContentTypeReader<CompiledShader>
    {
        protected override CompiledShader Read(ContentReader input, CompiledShader existingInstance)
        {
            CompiledShader shader = new CompiledShader();
            const int cookie = 'S' | ('H' << 8) | ('D' << 16) | ('R' << 24);

            EndianReader reader = input.ReadInt32() == cookie
                ? (EndianReader)new LittleEndianReader(input)
                : new BigEndianReader(input);

            shader.mFlags = (uint)reader.ReadI4();

            int numBytesInEffectCode = reader.ReadI4();
            shader.mEffectCode = new byte[numBytesInEffectCode];
            for (int i = 0; i < numBytesInEffectCode; ++i)
                shader.mEffectCode[i] = reader.ReadI1();

            int numTextureStrings = reader.ReadI4();
            shader.mTextures = new string[numTextureStrings];

            for (int i = 0; i < numTextureStrings; ++i)
            {
                List<byte> stringBytes = new List<byte>();

                for (; ; )
                {
                    byte b = reader.ReadI1();
                    if (b == 0)
                        break;

                    stringBytes.Add(b);
                }

                shader.mTextures[i] = ASCIIEncoding.ASCII.GetString(stringBytes.ToArray());
            }
            
            return shader;
        }
    }
}
