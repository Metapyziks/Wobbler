using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Wobbler
{
    public readonly struct Time : IEquatable<Time>
    {
        public const int TickRate = 4_410_000;

        public static Time Zero => default;

        public static Time FromSeconds(double seconds)
        {
            return new Time((long)(seconds * TickRate));
        }

        public static Time FromSamples(double sampleRate, double sampleCount)
        {
            return FromSeconds(sampleCount / sampleRate);
        }

        public static implicit operator Time(double value)
        {
            return FromSeconds(value);
        }

        public static Time operator +(in Time a, in TimeSpan b)
        {
            return new Time(a.Ticks + b.Ticks);
        }

        public static Time operator -(in Time a, in TimeSpan b)
        {
            return new Time(a.Ticks - b.Ticks);
        }

        public static TimeSpan operator -(in Time a, in Time b)
        {
            return new TimeSpan(a.Ticks - b.Ticks);
        }

        public long Ticks { get; }

        public double Seconds => (double)Ticks / TickRate;

        public Time(long ticks)
        {
            Ticks = ticks;
        }

        public bool Equals(Time other)
        {
            return Ticks == other.Ticks;
        }

        public override bool Equals(object obj)
        {
            return obj is Time other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Ticks.GetHashCode();
        }
    }

    public readonly struct TimeSpan : IEquatable<TimeSpan>
    {
        public static TimeSpan Zero => default;

        public static TimeSpan FromSeconds(double seconds)
        {
            return new TimeSpan((long)(seconds * Time.TickRate + 0.5d));
        }

        public static TimeSpan FromSamples(double sampleRate, double sampleCount)
        {
            return FromSeconds(sampleCount / sampleRate);
        }

        public static implicit operator TimeSpan(double value)
        {
            return FromSeconds(value);
        }

        public static implicit operator System.TimeSpan(TimeSpan value)
        {
            return System.TimeSpan.FromSeconds(value.Seconds);
        }

        public static TimeSpan operator +(in TimeSpan a, in TimeSpan b)
        {
            return new TimeSpan(a.Ticks + b.Ticks);
        }

        public static TimeSpan operator -(in TimeSpan a, in TimeSpan b)
        {
            return new TimeSpan(a.Ticks - b.Ticks);
        }

        public long Ticks { get; }

        public double Seconds => (double)Ticks / Time.TickRate;

        public TimeSpan(long ticks)
        {
            Ticks = ticks;
        }

        public bool Equals(TimeSpan other)
        {
            return Ticks == other.Ticks;
        }

        public override bool Equals(object obj)
        {
            return obj is TimeSpan other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Ticks.GetHashCode();
        }
    }
}
