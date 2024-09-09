using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SaSimulator
{
    // splits a target area into rectangular chunks.
    // GameObjects are stored in each of the chunks that they occupy.
    // When we wish to apply some local effect, we only need to consider GameObjects in chunks touched by this efect.
    // This should generally lead to better performance than iterating through all gme objects, though the worst-case asymptotic complexity is terrible.
    internal class UniformGrid
    {
        private class Record(GameObject storedObject)
        {
            public GameObject storedObject = storedObject;
            public int lastAccess = 0;
        }

        private List<Record>[,] records;
        private Distance x, y, width, height;

        public UniformGrid(int sideCount)
        {
            records = new List<Record>[sideCount, sideCount];
        }

        public void Reset(RectangleF bounds)
        {
            foreach(List<Record> list in records)
            {
                list.Clear();
            }
            x=bounds.X.Cells();
            y = bounds.Y.Cells();
            width = bounds.Width.Cells();
            height = bounds.Height.Cells();
        }

        // determines the chunks covered by an object with given size and position
        private struct BoundsOf(float x, float y, float size)
        {

        }

        public void Add(GameObject obj)
        {
            float minX = Math.Min(x.Cells, obj.WorldPosition.x.Cells-obj.size.Cells);
            float minY = Math.Min(y.Cells, obj.WorldPosition.y.Cells - obj.size.Cells);
            float maxX = Math.Max(x.Cells + width.Cells, obj.WorldPosition.x.Cells + obj.size.Cells);
            float maxY = Math.Max(y.Cells + height.Cells, obj.WorldPosition.y.Cells + obj.size.Cells);
        }
    }
}
