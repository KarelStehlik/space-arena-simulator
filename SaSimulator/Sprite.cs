using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SaSimulator
{
    internal class Sprite
    {
        private Texture2D texture;

        public Vector2 Position { get; set; }

        public Vector2 Size
        {
            get { return size; }
            set
            {
                size = value;
                origin = size / 2;
                scale.X = size.X / texture.Width;
                scale.Y = size.Y / texture.Height;
            }
        }

        private Vector2 size;
        private Vector2 origin;
        private Vector2 scale;

        public float Rotation { get; set; }
        public float Layer { get; set; }

        public Sprite(Texture2D texture)
        {
            this.texture = texture;
        }

        public void Draw(SpriteBatch batch)
        {
            batch.Draw(texture, Position, null, Color.White, Rotation, origin, scale, SpriteEffects.None, Layer);
        }
    }
}
