using System;

namespace Synthic.Patterns
{
    // An exact fraction (numerator / denominator). Pattern time is measured in cycles,
    // and cycles get subdivided into thirds, fifths, sevenths... — with floats those
    // drift (1/3 + 1/3 + 1/3 != 1), which makes notes land a hair outside the slice
    // that's supposed to contain them. Tidal and Strudel use rational time for the
    // same reason (the talk mentions this around 4:37).
    [Serializable]
    public readonly struct Rational : IComparable<Rational>, IEquatable<Rational>
    {
        public readonly long Num;
        public readonly long Den;

        public static readonly Rational Zero = new Rational(0, 1);
        public static readonly Rational One = new Rational(1, 1);

        public Rational(long num, long den = 1)
        {
            if (den == 0) throw new DivideByZeroException("Rational with zero denominator");
            if (den < 0) { num = -num; den = -den; }
            long g = Gcd(Math.Abs(num), den);
            if (g > 1) { num /= g; den /= g; }
            Num = num;
            Den = den;
        }

        private static long Gcd(long a, long b)
        {
            while (b != 0) { long t = a % b; a = b; b = t; }
            return a == 0 ? 1 : a;
        }

        // Turns an Inspector float (e.g. fast = 1.5) into an exact fraction, using the
        // closest fraction with a denominator up to maxDen (continued fractions).
        public static Rational FromDouble(double value, long maxDen = 1000)
        {
            if (double.IsNaN(value) || double.IsInfinity(value)) return Zero;
            long sign = value < 0 ? -1 : 1;
            value = Math.Abs(value);
            long h0 = 0, h1 = 1, k0 = 1, k1 = 0;
            double x = value;
            for (int i = 0; i < 64; i++)
            {
                long a = (long)Math.Floor(x);
                long h2 = a * h1 + h0, k2 = a * k1 + k0;
                if (k2 > maxDen) break;
                h0 = h1; h1 = h2; k0 = k1; k1 = k2;
                double frac = x - a;
                if (frac < 1e-12) break;
                x = 1.0 / frac;
            }
            return new Rational(sign * h1, k1 == 0 ? 1 : k1);
        }

        // The start of the cycle this time falls in ("sam" in Tidal). Correct for negatives.
        public Rational Sam()
        {
            long q = Num / Den;
            if (Num % Den != 0 && Num < 0) q -= 1;
            return new Rational(q, 1);
        }

        public Rational NextSam() => Sam() + One;
        public Rational CyclePos() => this - Sam();

        public double ToDouble() => (double)Num / Den;
        public float ToFloat() => (float)Num / Den;

        public static Rational operator +(Rational a, Rational b) => new Rational(a.Num * b.Den + b.Num * a.Den, a.Den * b.Den);
        public static Rational operator -(Rational a, Rational b) => new Rational(a.Num * b.Den - b.Num * a.Den, a.Den * b.Den);
        public static Rational operator -(Rational a) => new Rational(-a.Num, a.Den);
        public static Rational operator *(Rational a, Rational b) => new Rational(a.Num * b.Num, a.Den * b.Den);
        public static Rational operator /(Rational a, Rational b) => new Rational(a.Num * b.Den, a.Den * b.Num);

        public static bool operator <(Rational a, Rational b) => a.CompareTo(b) < 0;
        public static bool operator >(Rational a, Rational b) => a.CompareTo(b) > 0;
        public static bool operator <=(Rational a, Rational b) => a.CompareTo(b) <= 0;
        public static bool operator >=(Rational a, Rational b) => a.CompareTo(b) >= 0;
        public static bool operator ==(Rational a, Rational b) => a.Num == b.Num && a.Den == b.Den;
        public static bool operator !=(Rational a, Rational b) => !(a == b);

        public static implicit operator Rational(int v) => new Rational(v, 1);
        public static implicit operator Rational(long v) => new Rational(v, 1);

        public static Rational Min(Rational a, Rational b) => a <= b ? a : b;
        public static Rational Max(Rational a, Rational b) => a >= b ? a : b;

        public int CompareTo(Rational other) => (Num * other.Den).CompareTo(other.Num * Den);
        public bool Equals(Rational other) => this == other;
        public override bool Equals(object obj) => obj is Rational r && this == r;
        public override int GetHashCode() => unchecked(Num.GetHashCode() * 397) ^ Den.GetHashCode();
        public override string ToString() => Den == 1 ? Num.ToString() : $"{Num}/{Den}";
    }
}
