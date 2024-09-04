using Microsoft.Xna.Framework;
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
    internal class MonoGameWindow : Microsoft.Xna.Framework.Game
    {
        public Camera camera = new();
        private static MonoGameWindow? _instance;
        private GraphicsDeviceManager _graphics;
        private SpriteBatch? _spriteBatch;
        private Game game;

        public static void Init(Game game)
        {
            _instance = new(game);
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

        private MonoGameWindow(Game game)
        {
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
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape) || game.result != Game.GameResult.unfinished)
                Exit();

            game.Tick(1.Seconds() / 30);

            base.Update(gameTime);
        }


        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Microsoft.Xna.Framework.Color.CornflowerBlue);

            _spriteBatch.Begin();

            RectangleF bounds = game.GetBounds();
            camera.position = new(bounds.Left, bounds.Top);
            camera.zoom = Math.Min(Window.ClientBounds.Width / bounds.Width, Window.ClientBounds.Height / bounds.Height);
            game.Draw(_spriteBatch);

            _spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
