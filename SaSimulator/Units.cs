using System;
using System.Numerics;

/// A bunch of this is taken from my solution to 01-Physics. But technically it is 100% code that i wrote myself.
/// Sometimes it is annoying having to convert to and from these units, however i feel they do add some clarity
/// compared to just using floats for everything, and they have saved me at least 1 bug.
namespace SaSimulator
{
    public readonly struct Distance(float cells) : IMultiplyOperators<Distance, float, Distance>
    {
        public readonly float Cells = cells;

        public override readonly string ToString()
        {
            return Cells + " cells distance";
        }
        public static Distance operator +(Distance first, Distance other)
        {
            return new(first.Cells + other.Cells);
        }
        public static Distance operator *(Distance first, float other)
        {
            return new(first.Cells * other);
        }
        public static Distance operator -(Distance first, Distance other)
        {
            return new(first.Cells - other.Cells);
        }
        public static Distance operator /(Distance first, float other)
        {
            return new(first.Cells / other);
        }
        public static Speed operator /(Distance first, Time other)
        {
            return new(first.Cells / other.Seconds);
        }
        public static Time operator /(Distance first, Speed other)
        {
            return new(first.Cells / other.CellsPerSecond);
        }
        public static float operator /(Distance first, Distance other)
        {
            return first.Cells / other.Cells;
        }
    }

    public readonly struct Speed(float value) : IMultiplyOperators<Speed, float, Speed>
    {
        public readonly float CellsPerSecond = value;

        public override readonly string ToString()
        {
            return CellsPerSecond + "c/s speed";
        }

        public static Speed operator +(Speed first, Speed other)
        {
            return new(first.CellsPerSecond + other.CellsPerSecond);
        }
        public static Speed operator *(Speed first, float other)
        {
            return new(first.CellsPerSecond * other);
        }
        public static Speed operator -(Speed first, Speed other)
        {
            return new(first.CellsPerSecond - other.CellsPerSecond);
        }
        public static Speed operator /(Speed first, float other)
        {
            return new(first.CellsPerSecond / other);
        }
        public static Distance operator *(Speed first, Time other)
        {
            return new(first.CellsPerSecond * other.Seconds);
        }
        public static float operator /(Speed first, Speed other)
        {
            return first.CellsPerSecond / other.CellsPerSecond;
        }
    }

    public readonly struct Time(float seconds) : IMultiplyOperators<Time, float, Time>
    {
        public readonly float Seconds = seconds;

        public override string ToString()
        {
            return Seconds + "seconds";
        }

        public static Time operator +(Time first, Time other)
        {
            return new(first.Seconds + other.Seconds);
        }
        public static Time operator *(Time first, float other)
        {
            return new(first.Seconds * other);
        }
        public static Time operator -(Time first, Time other)
        {
            return new(first.Seconds - other.Seconds);
        }
        public static Time operator /(Time first, float other)
        {
            return new(first.Seconds / other);
        }
        public static float operator /(Time first, Time other)
        {
            return first.Seconds / other.Seconds;
        }
    }

    public static class Extensions
    {
        public static Distance Cells(this float value)
        {
            return new((float)value);
        }
        public static Distance Cells(this int value)
        {
            return new(value);
        }
        public static Speed CellsPerSecond(this float value)
        {
            return new((float)value);
        }
        public static Speed CellsPerSecond(this int value)
        {
            return new(value);
        }
        public static Time Seconds(this float value)
        {
            return new((float)value);
        }
        public static Time Seconds(this int value)
        {
            return new(value);
        }
        public static float ToDegrees(this float radians)
        {
            return (float)(radians * 180 / Math.PI);
        }
        public static float ToRadians(this float degrees)
        {
            return (float)(degrees / 180 * Math.PI);
        }
    }
}
