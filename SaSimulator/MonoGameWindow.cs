﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Drawing;

// About 4kb of this file is boilerplate stuff from a default Monogame project, therefore it should probably not count towards the code size requirement.

namespace SaSimulator
{
    internal class Camera
    {
        public float zoom = 2;
        public Vector2 position;
    }
    /// <summary>
    /// only relevant for graphics mode.
    /// This manages a Game, will tick that Game in real time, and will call graphics updates which render to this window.
    /// </summary>
    internal class MonoGameWindow : Microsoft.Xna.Framework.Game
    {
        public Camera camera = new();
        private static MonoGameWindow? _instance;
        private readonly GraphicsDeviceManager _graphics;
        private SpriteBatch? _spriteBatch;
        private readonly Game game;
        private float gamespeeed;
        private Time desiredGameTime = 0.Seconds();

        public static void Init(Game game, float gameSpeed)
        {
            _instance = new(game, gameSpeed);
        }

        public static MonoGameWindow Instance
        {
            get
            {
                if (_instance == null)
                {
                    throw new Exception("Window not initialized");
                }
                return _instance;
            }
        }

        private MonoGameWindow(Game game, float gamespeed)
        {
            this.gamespeeed = gamespeed;
            this.game = game;
            _graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
            Console.WriteLine("window create");
            Window.AllowUserResizing = true;
        }

        public Texture2D LoadTexture(string name)
        {
            return Content.Load<Texture2D>(name);
        }

        protected override void Initialize()
        {
            base.Initialize();
            game.Load();
        }

        protected override void LoadContent()
        {
            _spriteBatch = new SpriteBatch(GraphicsDevice);
        }

        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape) || game.Result != Game.GameResult.unfinished)
                Exit();

            base.Update(gameTime);

            desiredGameTime += ((float)gameTime.ElapsedGameTime.TotalSeconds).Seconds() * gamespeeed;
            while (game.Time.Seconds < desiredGameTime.Seconds)
            {
                game.Tick();
            }
        }


        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Microsoft.Xna.Framework.Color.Black);

#if DEBUG
            if(_spriteBatch == null)
            {
                throw new Exception("Sprite batch not initialized");
            }
#endif
            _spriteBatch.Begin();

            // zoom and center the camera
            float cameraClearance = 10;
            RectangleF bounds = game.GetBounds();
            bounds = new(bounds.Left - cameraClearance, bounds.Top - cameraClearance, bounds.Width + 2 * cameraClearance, bounds.Height + 2 * cameraClearance);

            float maxZoomX = Window.ClientBounds.Width / bounds.Width;
            float maxZoomY = Window.ClientBounds.Height / bounds.Height;

            if (maxZoomX > maxZoomY)
            {
                camera.zoom = maxZoomY;
                camera.position = new(bounds.Left - (Window.ClientBounds.Width / maxZoomY - bounds.Width) / 2, bounds.Top);
            }
            else
            {
                camera.zoom = maxZoomX;
                camera.position = new(bounds.Left, bounds.Top - (Window.ClientBounds.Height / maxZoomX - bounds.Height) / 2);
            }

            game.Draw(_spriteBatch);

            _spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
