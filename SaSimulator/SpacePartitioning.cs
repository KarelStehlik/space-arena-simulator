using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using static SaSimulator.Physics;

namespace SaSimulator
{
    /// <summary>
    /// a collision detection data structure.
    /// splits a target area into rectangular chunks.
    /// GameObjects are stored in each of the chunks that they occupy.
    /// When we wish to apply some local effect, we only need to consider GameObjects in chunks touched by this effect.
    /// This should generally lead to better performance than iterating through all game objects, though the worst-case asymptotic complexity is terrible.
    /// </summary>
    internal class UniformGrid
    {
        /// <summary>
        /// we could simply store the GameObjects in lists directly, and that's not necessarily wrong,
        /// but then when we're making a query we would need to create a collection of objects
        /// we've already returned to make sure we return nothing twice
        /// (as a single object may be stored in multiple chunks that we search through.)
        /// instead of that, I set lastAccess to the id of the search to know that I've already considered this Record.
        /// </summary>
        /// <param name="storedObject">the stored game object</param>
        private class Record(GameObject storedObject)
        {
            public GameObject storedObject = storedObject;
            public int lastAccess = 0;
        }

        private readonly List<Record>[,] records;
        private Distance x, y;
        private Distance chunkWidth, chunkHeight;
        private int searchId = int.MinValue;

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="sideCount">the number of chunks in each dimension. The total number of chunks will then be this squared.</param>
        public UniformGrid(int sideCount)
        {
            records = new List<Record>[sideCount, sideCount];
            for (int x = 0; x < sideCount; x++)
            {
                for (int y = 0; y < sideCount; y++)
                {
                    records[x, y] = [];
                }
            }
        }

        /// <summary>
        /// removes all stored objects and sets this grid to cover the given area. Does not change the number of chunks, only their size.
        /// </summary>
        /// <param name="bounds"></param>
        public void Reset(RectangleF bounds)
        {
            foreach (List<Record> list in records)
            {
                list.Clear();
            }
            x = bounds.X.Cells();
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
        private Bounds GetBoundsOf(Distance x, Distance y, Distance size)
        {
            return new Bounds()
            {
                minX = (int)Math.Max(0, (x - size - this.x) / chunkWidth),
                minY = (int)Math.Max(0, (y - size - this.y) / chunkHeight),
                maxX = (int)Math.Min(records.GetLength(0) - 1, (x + size - this.x) / chunkWidth),
                maxY = (int)Math.Min(records.GetLength(1) - 1, (y + size - this.y) / chunkHeight),
            };
        }

        private Bounds GetBoundsOf(GameObject obj)
        {
            return GetBoundsOf(obj.WorldPosition.x, obj.WorldPosition.y, obj.size);
        }

        public void Add(GameObject obj)
        {
            Bounds bounds = GetBoundsOf(obj);
            Record record = new(obj);
            for (int x = bounds.minX; x <= bounds.maxX; x++)
            {
                for (int y = bounds.minY; y <= bounds.maxY; y++)
                {
                    records[x, y].Add(record);
                }
            }
        }

        // return all objects in a given circle.
        public IEnumerable<GameObject> Get(Distance x, Distance y, Distance radius)
        {
            searchId++;
            Bounds bounds = GetBoundsOf(x, y, radius);
            for (int xChunk = bounds.minX; xChunk <= bounds.maxX; xChunk++)
            {
                for (int yChunk = bounds.minY; yChunk <= bounds.maxY; yChunk++)
                {
                    foreach (Record record in records[xChunk, yChunk])
                    {
                        if (record.lastAccess != searchId &&
                            Vector2.DistanceSquared(record.storedObject.WorldPosition.Position, new(x.Cells, y.Cells)) < Physics.Square(radius.Cells + record.storedObject.size.Cells))
                        {
                            yield return record.storedObject;
                            record.lastAccess = searchId;
                        }
                    }
                }
            }
        }

        // return all objects overlapping a given point.
        public IEnumerable<GameObject> Get(Distance x, Distance y)
        {
            searchId++;
            int chunkX = (int)((x - this.x) / chunkWidth);
            int chunkY = (int)((y - this.y) / chunkHeight);
            if (chunkX < 0 || chunkX >= records.GetLength(0) || chunkY < 0 || chunkY >= records.GetLength(1))
            {
                yield break;
            }
            foreach (Record record in records[chunkX, chunkY])
            {
                if (record.lastAccess != searchId &&
                    Vector2.DistanceSquared(record.storedObject.WorldPosition.Position, new(x.Cells, y.Cells)) < Physics.Square(record.storedObject.size.Cells))
                {
                    record.lastAccess = searchId;
                    yield return record.storedObject;
                }
            }
        }

        private bool IsChunk(int x, int y)
        {
            return x >= 0 && x < records.GetLength(0) && y >= 0 && y < records.GetLength(1);
        }

        private bool IsRecordOnRay(Record record, float sin, float cos, Transform origin)
        {
            if (record.lastAccess == searchId)
            {
                return false;
            }
            record.lastAccess = searchId;
            // rotate object around the ray origin so that the ray is the x axis, then check if it intersects the x axis.
            float dx = (record.storedObject.WorldPosition.x - origin.x).Cells;
            float dy = (record.storedObject.WorldPosition.y - origin.y).Cells;
            return Math.Abs(dx * (-sin) + dy * cos) < record.storedObject.size.Cells;
        }

        // return all objects hit by a given ray, approximately in order (not guaranteed).
        public IEnumerable<GameObject> Get(Transform origin, Distance length)
        {
            searchId++;
            float sin = (float)Math.Sin(origin.rotation), cos = (float)Math.Cos(origin.rotation); //-.2, +.8

            bool steppingY = Math.Abs(sin) > Math.Abs(cos);  //false

            Vector2 step = steppingY ? new(cos / sin * (chunkHeight / chunkWidth), 1) : new(1, sin / cos * (chunkWidth / chunkHeight));  // (1, -0.1)

            if ((!steppingY && cos < 0) || (steppingY && sin<0))
            {
                step = - step;
            }

            Distance stepSize = (steppingY ? chunkHeight : chunkWidth) * step.Length();

            Vector2 chunkCoords = new((origin.x - x) / chunkWidth, (origin.y - y) / chunkHeight);   // 50,50
            
            // align chunk coordinates to the edge of a chunk
            if (steppingY)
            {
                float newChunkY = (int)chunkCoords.Y + (step.Y < 0 ? 0.999f : 0);
                chunkCoords.X += step.X * (newChunkY - chunkCoords.Y) / step.Y;
                chunkCoords.Y = newChunkY;
            }
            else
            {
                float newChunkX = (int)chunkCoords.X + (step.X < 0 ? 0.999f : 0);
                chunkCoords.Y += step.Y * (newChunkX - chunkCoords.X) / step.X;
                chunkCoords.X = newChunkX;
            }
            
            int lastX = (int)chunkCoords.X, lastY = (int)chunkCoords.Y;
            float stepsNeeded = length / stepSize +1;

            for (int i = 0; i < stepsNeeded; i++)
            {
                int x = (int)chunkCoords.X, y = (int)chunkCoords.Y;
                
                // check the [x, lastY] chunk
                if (steppingY && x != lastX && IsChunk(x, lastY))
                {
                    foreach (var record in records[x, lastY])
                    {
                        if (IsRecordOnRay(record, sin, cos, origin))
                        {
                            yield return record.storedObject;
                        }
                    }
                }

                // check the [lastX, y] chunk
                if (!steppingY && y != lastY && IsChunk(lastX, y))
                {
                    foreach (var record in records[lastX, y])
                    {
                        if (IsRecordOnRay(record, sin, cos, origin))
                        {
                            yield return record.storedObject;
                        }
                    }
                }
                
                // check [x,y]
                if (IsChunk(x, y))
                {
                    foreach (var record in records[x, y])
                    {
                        if (IsRecordOnRay(record, sin, cos, origin))
                        {
                            yield return record.storedObject;
                        }
                    }
                }
                // i had these 3 chunk checks extracted as a local function at first, replaced with
                // foreach(var gameObject in CheckChunk(x,y)){yield return gameObject; }
                // however the extra layer of enumerators added a performance hit that was too significant to ignore,
                // so i felt the little bit of repetition was the lesser evil.

                chunkCoords += step;
                lastX = x;
                lastY = y;
            }
        }
    }
}
