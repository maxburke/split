using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Microsoft.Xna.Framework.Net;
using Microsoft.Xna.Framework.Storage;
using Split.Pipeline;

namespace Split
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class Split : Microsoft.Xna.Framework.Game
    {
        Renderer Renderer;

        GraphicsDeviceManager Graphics;

        FreeCam Camera;
        Matrix World = Matrix.CreateWorld(new Vector3(), new Vector3(0, -1, 0), new Vector3(-1, 0, 0));
        Matrix Projection;
        int mBackBufferWidth;
        int mBackBufferHeight;
        SurfaceFormat mBackBufferFormat;

        public Split()
        {
            Graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            Graphics.PreferMultiSampling = true;
//            Graphics.PreferredBackBufferFormat = SurfaceFormat.Bgra1010102;

//            Graphics.PreferredBackBufferWidth = 1920;
//            Graphics.PreferredBackBufferHeight = 1200;
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            mBackBufferWidth = GraphicsDevice.PresentationParameters.BackBufferWidth;
            mBackBufferHeight = GraphicsDevice.PresentationParameters.BackBufferHeight;
            mBackBufferFormat = GraphicsDevice.PresentationParameters.BackBufferFormat;

            Projection = Matrix.CreatePerspectiveFieldOfView(
                (float)(Math.PI / 4), (float)mBackBufferWidth / (float)mBackBufferHeight, 1.0f, 5000.0f);
            Camera = new FreeCam(mBackBufferWidth, mBackBufferHeight);

            base.Initialize();
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            Renderer = new Renderer(GraphicsDevice);
            Renderer.Register(new BspRenderer(Content.Load<Bsp>("q3dm11"), GraphicsDevice, Content));
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>

        static bool mSpecial;
        public static bool Special { get { return mSpecial; } }

        protected override void Update(GameTime gameTime)
        {
            Camera.Update();

            KeyboardState KS = Keyboard.GetState();
            // Allows the game to exit
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed
                || Keyboard.GetState().IsKeyDown(Keys.Escape))
                this.Exit();

            if (KS.IsKeyDown(Keys.F11))
                mSpecial = !mSpecial;

            if (KS.IsKeyDown(Keys.PrintScreen))
                TakeScreenshot();

            base.Update(gameTime);
        }


        void TakeScreenshot()
        {
#if !XBOX
            int i = 0;
            string fileName = null;

            for (; ; ++i)
            {
                fileName = string.Format("screenshot_{0}.png", i);
                if (!File.Exists(fileName))
                    break;
            }

            ResolveTexture2D backBuffer = new ResolveTexture2D(GraphicsDevice, mBackBufferWidth, mBackBufferHeight, 1, mBackBufferFormat);
            GraphicsDevice.ResolveBackBuffer(backBuffer);
            backBuffer.Save(fileName, ImageFileFormat.Png);
#endif
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            Matrix wvp = Matrix.Multiply(Matrix.Multiply(World, Camera.View), Projection);
            Renderer.SetWorldViewProjection(wvp);
            Renderer.Render();
            base.Draw(gameTime);
        }
    }
}
