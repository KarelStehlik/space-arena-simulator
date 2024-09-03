// This is taken from my solution to 01-Physics. But technically it is 100% code that i wrote myself.
namespace SaSimulator
{
    public struct Distance(float cells)
    {
        public float Cells { get; set; } = cells;

        public override string ToString()
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
    }

    public struct Speed(float value)
    {
        public float CellsPerSecond { get; set; } = value;

        public override string ToString()
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
    }

    public class Time(float seconds)
    {
        public float Seconds { get; set; } = seconds;

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
    }

    public static class Extensions
    {
        public static Distance Cells(this double value)
        {
            return new((float)value);
        }
        public static Distance Cells(this float value)
        {
            return new(value);
        }
        public static Distance Cells(this int value)
        {
            return new(value);
        }
        public static Speed CellsPerSecond(this double value)
        {
            return new((float)value);
        }
        public static Speed CellsPerSecond(this float value)
        {
            return new(value);
        }
        public static Speed CellsPerSecond(this int value)
        {
            return new(value);
        }
        public static Time Seconds(this double value)
        {
            return new((float)value);
        }
        public static Time Seconds(this float value)
        {
            return new(value);
        }
        public static Time Seconds(this int value)
        {
            return new(value);
        }
    }
}
