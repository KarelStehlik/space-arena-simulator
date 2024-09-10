using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SaSimulator
{
    // splits a target area into rectangular chunks.
    // GameObjects are stored in each of the chunks that they occupy.
    // When we wish to apply some local effect, we only need to consider GameObjects in chunks touched by this efect.
    // This should generally lead to better performance than iterating through all gme objects, though the worst-case asymptotic complexity is terrible.
    internal class UniformGrid(int sideCount)
    {
        private class Record(GameObject storedObject)
        {
            public GameObject storedObject = storedObject;
            public int lastAccess = 0;
        }

        private readonly List<Record>[,] records = new List<Record>[sideCount, sideCount];
        private Distance x, y;
        private Distance chunkWidth, chunkHeight;
        private int searchId = int.MinValue;

        public void Reset(RectangleF bounds)
        {
            foreach(List<Record> list in records)
            {
                list.Clear();
            }
            x=bounds.X.Cells();
            y = bounds.Y.Cells();
            chunkWidth = bounds.Width.Cells() / records.GetLength(0);
            chunkHeight = bounds.Height.Cells() / records.GetLength(1);
        }

        
        private struct Bounds
        {
            public int minX;
            public int minY;
            public int maxX;
            public int maxY;
        }

        // determines the chunks covered by an object with given size and position
        private Bounds GetBoundsOf(float x, float y, float size)
        {
            return new Bounds()
            {
                minX = (int)Math.Max(0, (x - size - this.x.Cells) / chunkWidth.Cells),
                minY = (int)Math.Max(0, (y - size - this.y.Cells) / chunkHeight.Cells),
                maxX = (int)Math.Min(records.GetLength(0) - 1, (x + size - this.x.Cells) / chunkWidth.Cells),
                maxY = (int)Math.Min(records.GetLength(1) - 1, (y + size - this.y.Cells) / chunkHeight.Cells),
            };
        }

        private Bounds GetBoundsOf(GameObject obj)
        {
            return GetBoundsOf(obj.WorldPosition.x.Cells, obj.WorldPosition.y.Cells, obj.size.Cells);
        }

        public void Add(GameObject obj)
        {
            Bounds bounds = GetBoundsOf(obj);
            Record record = new(obj);
            for (int x = bounds.minX; x <=  bounds.maxX; x++)
            {
                for(int y = bounds.minY;  y <= bounds.maxY; y++)
                {
                    records[x, y].Add(record);
                }
            }
        }

        public IEnumerable<GameObject> Get(float x, float y, float radius)
        {
            Bounds bounds = GetBoundsOf(x, y, radius);
            for (int xChunk = bounds.minX; xChunk <= bounds.maxX; xChunk++)
            {
                for (int yChunk = bounds.minY; yChunk <= bounds.maxY; yChunk++)
                {
                    foreach(Record record in records[xChunk,yChunk]) {
                        if(record.lastAccess != searchId &&
                            Vector2.DistanceSquared(record.storedObject.WorldPosition.Position, new(x,y)) < (radius+record.storedObject.size.Cells))
                        {
                            yield return record.storedObject;
                            record.lastAccess = searchId;
                        }
                    }
                }
            }
        }
    }
}
