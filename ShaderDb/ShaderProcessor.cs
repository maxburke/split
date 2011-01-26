using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;
using Microsoft.Xna.Framework.Content.Pipeline.Serialization.Compiler;
using Slim;

namespace Split.Pipeline
{
    public class ShaderText
    {
        public string ShaderContent;
    }

    public class CompiledShaderOutput
    {
        public uint Flags;
        public readonly string[] Textures;
        public byte[] ShaderByteCode;

        public CompiledShaderOutput(string[] textures, byte[] shaderByteCode, uint flags)
        {
            Textures = textures;
            ShaderByteCode = shaderByteCode;
            Flags = flags;
        }
    }

    [ContentProcessor(DisplayName = "ShaderProcessor")]
    public class ShaderProcessor : ContentProcessor<ShaderText, CompiledShaderOutput>
    {
        public override CompiledShaderOutput Process(ShaderText input, ContentProcessorContext context)
        {
            ShaderParser SP = new ShaderParser(input.ShaderContent);
            byte[] textureBytes = new byte[1];

            EffectProcessor compiler = new EffectProcessor();
            EffectContent content = new EffectContent();
            content.EffectCode = SP.GetHlsl();
            CompiledEffectContent compiledContent = compiler.Process(content, context);

            return new CompiledShaderOutput(SP.GetTextureList(), compiledContent.GetEffectCode(), SP.GetFlags());
        }
    }

    [ContentImporter(".q3shader", DisplayName = "ShaderImporter", DefaultProcessor = "ShaderProcessor")]
    public class Q3ShaderImporter : ContentImporter<ShaderText>
    {
        public override ShaderText Import(string filename, ContentImporterContext context)
        {
            ShaderText shaderText = new ShaderText();
            shaderText.ShaderContent = File.ReadAllText(filename);
            return shaderText;
        }
    }

    [ContentTypeWriter]
    public class CompiledShaderWriter : ContentTypeWriter<CompiledShaderOutput>
    {
        protected override void Write(ContentWriter output, CompiledShaderOutput shaderDbFile)
        {
            output.Write((byte)'S'); output.Write((byte)'H'); output.Write((byte)'D'); output.Write((byte)'R');
            output.Write(shaderDbFile.Flags);
            output.Write(shaderDbFile.ShaderByteCode.Length);
            output.Write(shaderDbFile.ShaderByteCode);
            output.Write(shaderDbFile.Textures.Length);

            for (int i = 0; i < shaderDbFile.Textures.Length; ++i)
            {
                byte[] stringBytes = ASCIIEncoding.ASCII.GetBytes(shaderDbFile.Textures[i]);
                output.Write(stringBytes);
                output.Write((byte)0);
            }
        }

        public override string GetRuntimeReader(TargetPlatform targetPlatform)
        {
            // TODO: change this to the name of your ContentTypeReader
            // class which will be used to load this data.
            return "Split.ShaderReader, Split";
        }

        protected override bool ShouldCompressContent(TargetPlatform targetPlatform, object value)
        {
            return true;
        }
    }
}