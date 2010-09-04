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
    public class BspString
    {
        public string Filename;
    }

    [ContentProcessor(DisplayName="MapMaker")]
    public class MapMaker : ContentProcessor<BspString, BspString>
    {
        public override BspString Process(BspString input, ContentProcessorContext context)
        {
            return input;
        }
    }

    [ContentImporter(".bsp", DisplayName = "BspImporter", DefaultProcessor="MapMaker")]
    public class BspImporter : ContentImporter<BspString>
    {
        public override BspString Import(string filename, ContentImporterContext context)
        {
            BspString bspString = new BspString();
            bspString.Filename = filename;
            return bspString;
        }
    }
}