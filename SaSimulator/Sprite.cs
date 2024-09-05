using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using static SaSimulator.Physics;

namespace SaSimulator
{
    internal class Sprite
    {
        private Texture2D? texture;

        public Vector2 Position { get; set; }

        public Color Color { get; set; } = Color.White;

        public Vector2 Size
        {
            get { return size; }
            set
            {
                size = value;
                scale.X = size.X / texture.Width;
                scale.Y = size.Y / texture.Height;
            }
        }

        public void setTransform(ref Transform t)
        {
            Position = new((float)t.x.Cells, (float)t.y.Cells);
            Rotation = (float)t.rotation;
        }

        private Vector2 size;
        private Vector2 origin;
        private Vector2 scale;

        public Sprite(string textureName)
        {
            texture = MonoGameWindow.Instance.LoadTexture(textureName);
            origin = new(texture.Width / 2, texture.Height / 2);
        }

        public float Rotation { get; set; }
        public float Layer { get; set; }

        public void Draw(SpriteBatch batch)
        {
            Camera camera = MonoGameWindow.Instance.camera;
            batch.Draw(texture, (Position - camera.position) * camera.zoom,
                null, Color, Rotation, origin, scale * camera.zoom, SpriteEffects.None, Layer);
        }
    }
}
