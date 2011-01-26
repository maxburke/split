using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;
using Microsoft.Xna.Framework.Content.Pipeline.Processors;
using Slim;

// TODO: replace these with the processor input and output types.
using TInput = System.String;
using TOutput = System.String;

namespace Split.Pipeline
{
    public class BspData
    {
        public byte[] MapData;
    }

    [ContentProcessor(DisplayName="MapMaker")]
    public class MapMaker : ContentProcessor<BspData, BspData>
    {
        public override BspData Process(BspData input, ContentProcessorContext context)
        {
            return input;
        }
    }

    [ContentImporter(".bsp", DisplayName = "BspImporter", DefaultProcessor="MapMaker")]
    public class BspImporter : ContentImporter<BspData>
    {
        public override BspData Import(string filename, ContentImporterContext context)
        {
            BspData bspData = new BspData();
            FileStream FS = File.OpenRead(filename);
            bspData.MapData = new byte[FS.Length];
            FS.Read(bspData.MapData, 0, (int)FS.Length);

            return bspData;
        }
    }
}