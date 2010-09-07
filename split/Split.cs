using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
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

        public Split()
        {
            Graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            Graphics.PreferMultiSampling = true;
            Graphics.PreferredBackBufferFormat = SurfaceFormat.Bgra1010102;

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
            int width = GraphicsDevice.DisplayMode.Width;
            int height = GraphicsDevice.DisplayMode.Height;
            Projection = Matrix.CreatePerspectiveFieldOfView(
                (float)(Math.PI / 4), (float)width / (float)height, 1.0f, 5000.0f);
            Camera = new FreeCam(width, height);

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

        protected override void Update(GameTime gameTime)
        {
            Camera.Update();

            KeyboardState KS = Keyboard.GetState();
            // Allows the game to exit
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed
                || Keyboard.GetState().IsKeyDown(Keys.Escape))
                this.Exit();

            base.Update(gameTime);
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
