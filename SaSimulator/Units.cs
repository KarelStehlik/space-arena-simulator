// A bunch of this is taken from my solution to 01-Physics. But technically it is 100% code that i wrote myself.
using System;

namespace SaSimulator
{
    public readonly struct Distance(double cells)
    {
        public readonly double Cells = cells;

        public override readonly string ToString()
        {
            return Cells + " cells distance";
        }

        public static Distance operator +(Distance first, Distance other)
        {
            return new(first.Cells + other.Cells);
        }
        public static Distance operator *(Distance first, double other)
        {
            return new(first.Cells * other);
        }
        public static Distance operator -(Distance first, Distance other)
        {
            return new(first.Cells - other.Cells);
        }
        public static Distance operator /(Distance first, double other)
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
    }

    public readonly struct Speed(double value)
    {
        public readonly double CellsPerSecond = value;

        public override readonly string ToString()
        {
            return CellsPerSecond + "c/s speed";
        }

        public static Speed operator +(Speed first, Speed other)
        {
            return new(first.CellsPerSecond + other.CellsPerSecond);
        }
        public static Speed operator *(Speed first, double other)
        {
            return new(first.CellsPerSecond * other);
        }
        public static Speed operator -(Speed first, Speed other)
        {
            return new(first.CellsPerSecond - other.CellsPerSecond);
        }
        public static Speed operator /(Speed first, double other)
        {
            return new(first.CellsPerSecond / other);
        }
        public static Distance operator *(Speed first, Time other)
        {
            return new(first.CellsPerSecond * other.Seconds);
        }
    }

    public readonly struct Time(double seconds)
    {
        public readonly double Seconds = seconds;

        public override string ToString()
        {
            return Seconds + "seconds";
        }

        public static Time operator +(Time first, Time other)
        {
            return new(first.Seconds + other.Seconds);
        }
        public static Time operator *(Time first, double other)
        {
            return new(first.Seconds * other);
        }
        public static Time operator -(Time first, Time other)
        {
            return new(first.Seconds - other.Seconds);
        }
        public static Time operator /(Time first, double other)
        {
            return new(first.Seconds / other);
        }
    }

    public static class Extensions
    {
        public static Distance Cells(this double value)
        {
            return new((double)value);
        }
        public static Distance Cells(this int value)
        {
            return new(value);
        }
        public static Speed CellsPerSecond(this double value)
        {
            return new((double)value);
        }
        public static Speed CellsPerSecond(this int value)
        {
            return new(value);
        }
        public static Time Seconds(this double value)
        {
            return new((double)value);
        }
        public static Time Seconds(this int value)
        {
            return new(value);
        }
        public static float ToDegrees(this float radians)
        {
            return (float)(radians * 180 / Math.PI);
        }
        public static double ToDegrees(this double radians)
        {
            return (radians * 180 / Math.PI);
        }
        public static float ToRadians(this float degrees)
        {
            return (float)(degrees / 180 * Math.PI);
        }
        public static double ToRadians(this double degrees)
        {
            return (degrees / 180 * Math.PI);
        }
    }
}
