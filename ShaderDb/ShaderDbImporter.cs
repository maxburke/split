using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;
using Microsoft.Xna.Framework.Content.Pipeline.Serialization.Compiler;
using Slim;

namespace Split.Pipeline
{
    public class ShaderDbString
    {
        public string Filename;
    }

    [ContentProcessor(DisplayName = "ShaderBuilder")]
    public class ShaderProcessor : ContentProcessor<ShaderDbString, ShaderDbString>
    {
        public override ShaderDbString Process(ShaderDbString input, ContentProcessorContext context)
        {
            return input;
        }
    }

    [ContentImporter(".shaderdb", DisplayName = "ShaderBuilder", DefaultProcessor = "ShaderProcessor")]
    public class ShaderDbImporter : ContentImporter<ShaderDbString>
    {
        public override ShaderDbString Import(string filename, ContentImporterContext context)
        {
            ShaderDbString shaderDbString = new ShaderDbString();
            shaderDbString.Filename = filename;
            return shaderDbString;
        }
    }

    [ContentTypeWriter]
    public class ShaderDbWriter : ContentTypeWriter<ShaderDbString>
    {
        protected override void Write(ContentWriter output, ShaderDbString shaderDbFile)
        {
            FileStream shaderDb = File.OpenRead(shaderDbFile.Filename);
            byte[] shaderDbBytes = new byte[shaderDb.Length];
            shaderDb.Read(shaderDbBytes, 0, (int)shaderDb.Length);
            output.Write(shaderDbBytes);
        }

        public override string GetRuntimeReader(TargetPlatform targetPlatform)
        {
            // TODO: change this to the name of your ContentTypeReader
            // class which will be used to load this data.
            return "Split.ShaderDbReader, Split";
        }
    }
}