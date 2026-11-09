using System;
using System.Collections.Generic;

// A tiny "uzulang" core, the same design TidalCycles and Strudel use, following
// Matthew Kaney's talk "Composing Patterns: The Pure Functional Core of Uzulangs":
//
//   - A pattern is a FUNCTION from a span of time (an Arc, measured in cycles) to the
//     events (Haps) happening inside that span. Patterns are infinite in both
//     directions: query any time, forward or backward, and you get an answer.
//   - Everything else (fast, early, cat, rev, palindrome, combining two patterns with
//     different structure strategies) is a function that wraps one pattern's query
//     in another.
//
// Nothing in this file touches UnityEngine, so it can be unit-tested anywhere.
//
// Example (the talk's demos, written as code):
//
//   var steps   = Mini.Parse("0 2 4 7");                       // scale steps
//   var layered = Pattern.Stack(steps, steps.Fast(2).Fmap(s => s + 4));
//   var pal     = Pattern.Palindrome(steps);                    // up, then down
//   var octaves = Mini.Parse("0 1 2");
//   var both    = Pattern.Combine(steps, octaves, (s, o) => s + 7 * o, Structure.Left);

namespace Synthic.Patterns
{
    // A span of time, [Begin, End), in cycles.
    public readonly struct Arc
    {
        public readonly Rational Begin;
        public readonly Rational End;

        public Arc(Rational begin, Rational end) { Begin = begin; End = end; }

        public Rational Duration => End - Begin;

        public Arc WithTime(Func<Rational, Rational> f) => new Arc(f(Begin), f(End));

        // Overlap of two arcs, or null if they don't overlap. A zero-width arc counts
        // as overlapping if it sits inside the other one.
        public Arc? Intersect(Arc other)
        {
            Rational b = Rational.Max(Begin, other.Begin);
            Rational e = Rational.Min(End, other.End);
            if (b > e) return null;
            if (b == e)
            {
                // Zero-width results only count when they come from a zero-width input
                // (e.g. a point query), not from two arcs that merely touch.
                if (Begin == End || other.Begin == other.End) return new Arc(b, e);
                return null;
            }
            return new Arc(b, e);
        }

        // Splits this arc at every cycle boundary, so each piece lies within one cycle.
        public List<Arc> CycleArcs()
        {
            var result = new List<Arc>();
            if (Begin >= End)
            {
                if (Begin == End) result.Add(this);
                return result;
            }
            Rational b = Begin;
            while (b < End)
            {
                Rational next = Rational.Min(b.Sam() + Rational.One, End);
                result.Add(new Arc(b, next));
                b = next;
            }
            return result;
        }

        public override string ToString() => $"({Begin} → {End})";
    }

    // One event. Whole is the event's full extent; Part is the slice of it that fell
    // inside the query. A note should only be TRIGGERED when its onset is in the query
    // (HasOnset); otherwise you'd play the same note again for every slice it spans.
    public readonly struct Hap<T>
    {
        public readonly Arc Whole;
        public readonly Arc Part;
        public readonly T Value;

        public Hap(Arc whole, Arc part, T value) { Whole = whole; Part = part; Value = value; }

        public bool HasOnset => Whole.Begin == Part.Begin;

        public Hap<U> WithValue<U>(U v) => new Hap<U>(Whole, Part, v);
        public Hap<T> WithTime(Func<Rational, Rational> f) => new Hap<T>(Whole.WithTime(f), Part.WithTime(f), Value);

        public override string ToString() => $"{Value} {Whole}{(HasOnset ? "" : $" part {Part}")}";
    }

    // How two patterns' rhythms combine when you join their values (talk ~11:00).
    //   Left       — keep the rhythm of the first pattern; each event takes whatever the
    //                second pattern holds at that moment.
    //   Right      — keep the rhythm of the second pattern.
    //   Both       — every overlap of the two becomes its own event.
    //   SqueezeIn  — squeeze one whole cycle of the second pattern into each event of the first.
    //   SqueezeOut — squeeze one whole cycle of the first pattern into each event of the second.
    public enum Structure
    {
        Left,
        Right,
        Both,
        SqueezeIn,
        SqueezeOut,
    }

    public sealed class Pattern<T>
    {
        public readonly Func<Arc, List<Hap<T>>> Query;

        public Pattern(Func<Arc, List<Hap<T>>> query) { Query = query; }

        public List<Hap<T>> QueryArc(Rational begin, Rational end) => Query(new Arc(begin, end));

        // Events whose onset falls in [begin, end), sorted by onset.
        public List<Hap<T>> Onsets(Rational begin, Rational end)
        {
            var list = QueryArc(begin, end).FindAll(h => h.HasOnset);
            list.Sort((a, b) => a.Whole.Begin.CompareTo(b.Whole.Begin));
            return list;
        }

        // ── Plumbing ──────────────────────────────────────────────────────

        // Runs each query one cycle at a time (needed by anything that works per cycle).
        public Pattern<T> SplitQueries() => new Pattern<T>(arc =>
        {
            var result = new List<Hap<T>>();
            foreach (var piece in arc.CycleArcs()) result.AddRange(Query(piece));
            return result;
        });

        public Pattern<T> WithQueryTime(Func<Rational, Rational> f) => new Pattern<T>(arc => Query(arc.WithTime(f)));

        public Pattern<T> WithHapTime(Func<Rational, Rational> f) => new Pattern<T>(arc =>
            Query(arc).ConvertAll(h => h.WithTime(f)));

        public Pattern<T> FilterValues(Func<T, bool> keep) => new Pattern<T>(arc =>
            Query(arc).FindAll(h => keep(h.Value)));

        public Pattern<T> FilterHaps(Func<Hap<T>, bool> keep) => new Pattern<T>(arc =>
            Query(arc).FindAll(h => keep(h)));

        // ── The operations from the talk ──────────────────────────────────

        // "Mapping" (functor, ~7:30): change every value, keep the rhythm.
        // e.g. scale steps → notes in D major.
        public Pattern<U> Fmap<U>(Func<T, U> f) => new Pattern<U>(arc =>
            Query(arc).ConvertAll(h => h.WithValue(f(h.Value))));

        // Speed up by a factor (~8:45: "multiply the query").
        public Pattern<T> Fast(Rational factor)
        {
            if (factor <= Rational.Zero) return Pattern.Silence<T>();
            return WithQueryTime(t => t * factor).WithHapTime(t => t / factor);
        }

        public Pattern<T> Slow(Rational factor)
        {
            if (factor <= Rational.Zero) return Pattern.Silence<T>();
            return Fast(Rational.One / factor);
        }

        // Shift earlier in time (~8:40: "add to the query").
        public Pattern<T> Early(Rational offset) => WithQueryTime(t => t + offset).WithHapTime(t => t - offset);
        public Pattern<T> Late(Rational offset) => Early(-offset);

        // Reverse each cycle (~9:55).
        public Pattern<T> Rev()
        {
            return new Pattern<T>(arc =>
            {
                Rational cycle = arc.Begin.Sam();
                Rational next = cycle + Rational.One;
                Func<Rational, Rational> reflect = t => cycle + (next - t);
                var reflected = new Arc(reflect(arc.End), reflect(arc.Begin));
                return Query(reflected).ConvertAll(h =>
                    new Hap<T>(
                        new Arc(reflect(h.Whole.End), reflect(h.Whole.Begin)),
                        new Arc(reflect(h.Part.End), reflect(h.Part.Begin)),
                        h.Value));
            }).SplitQueries();
        }

        // Repeat a pattern every n cycles only on some cycles, etc. — handy extras.
        public Pattern<T> Every(int n, Func<Pattern<T>, Pattern<T>> transform)
        {
            if (n <= 0) return this;
            var transformed = transform(this);
            return new Pattern<T>(arc =>
            {
                long cycle = arc.Begin.Sam().Num;
                long m = ((cycle % n) + n) % n;
                return m == 0 ? transformed.Query(arc) : Query(arc);
            }).SplitQueries();
        }

        // Deterministically drop events (Q&A ~22:50: randomness as a noisy function of
        // time — the same moment always gives the same answer). chance = 0.5 drops about
        // half the notes; the same notes every time you pass that point in the pattern.
        public Pattern<T> Degrade(double chance, int seed = 0)
        {
            if (chance <= 0) return this;
            return FilterHaps(h => Pattern.TimeToRand(h.Whole.Begin, seed) >= chance);
        }
    }

    public static class Pattern
    {
        public static Pattern<T> Silence<T>() => new Pattern<T>(_ => new List<Hap<T>>());

        // One value filling every cycle (~6:10).
        public static Pattern<T> Pure<T>(T value) => new Pattern<T>(arc =>
        {
            var result = new List<Hap<T>>();
            foreach (var piece in arc.CycleArcs())
            {
                Rational start = piece.Begin.Sam();
                var whole = new Arc(start, start + Rational.One);
                result.Add(new Hap<T>(whole, piece, value));
            }
            return result;
        });

        // Layer patterns on top of each other (~8:00, the monoid).
        public static Pattern<T> Stack<T>(params Pattern<T>[] patterns) => new Pattern<T>(arc =>
        {
            var result = new List<Hap<T>>();
            foreach (var p in patterns) result.AddRange(p.Query(arc));
            return result;
        });

        public static Pattern<T> Stack<T>(IList<Pattern<T>> patterns) => Stack(ToArray(patterns));

        // One cycle of each pattern in turn (~9:15). Each pattern keeps its own sense of
        // which cycle it's on, so a pattern that changes per cycle still progresses.
        public static Pattern<T> SlowCat<T>(params Pattern<T>[] patterns)
        {
            int n = patterns.Length;
            if (n == 0) return Silence<T>();
            if (n == 1) return patterns[0];
            return new Pattern<T>(arc =>
            {
                Rational cycle = arc.Begin.Sam();
                long c = cycle.Num;
                int i = (int)(((c % n) + n) % n);
                // Pattern i sees cycles 0,1,2... as overall cycles i, i+n, i+2n...
                Rational ownCycle = new Rational(FloorDiv(c, n), 1);
                Rational offset = cycle - ownCycle;
                return patterns[i].Query(arc.WithTime(t => t - offset)).ConvertAll(h => h.WithTime(t => t + offset));
            }).SplitQueries();
        }

        public static Pattern<T> SlowCat<T>(IList<Pattern<T>> patterns) => SlowCat(ToArray(patterns));

        // All patterns squeezed into one cycle (~9:40: cat, then fast by the count).
        public static Pattern<T> FastCat<T>(params Pattern<T>[] patterns) =>
            patterns.Length == 0 ? Silence<T>() : SlowCat(patterns).Fast(patterns.Length);

        public static Pattern<T> FastCat<T>(IList<Pattern<T>> patterns) => FastCat(ToArray(patterns));

        // The talk's "fancy brackets": a list of values packed into one cycle.
        public static Pattern<T> Sequence<T>(params T[] values) =>
            FastCat(Array.ConvertAll(values, v => Pure(v)));

        // A pattern followed by its reverse: the up-and-down shape at ~10:05.
        public static Pattern<T> Palindrome<T>(Pattern<T> p) => SlowCat(p, p.Rev());

        // ── Combining two patterns (the applicative part, ~10:35) ───────────

        public static Pattern<V> Combine<T, U, V>(Pattern<T> left, Pattern<U> right, Func<T, U, V> f, Structure structure)
        {
            switch (structure)
            {
                case Structure.Left: return AppLeft(left, right, f);
                case Structure.Right: return AppLeft(right, left, (u, t) => f(t, u));
                case Structure.Both: return AppBoth(left, right, f);
                case Structure.SqueezeIn: return SqueezeJoin(left.Fmap(t => right.Fmap(u => f(t, u))));
                case Structure.SqueezeOut: return SqueezeJoin(right.Fmap(u => left.Fmap(t => f(t, u))));
                default: return AppLeft(left, right, f);
            }
        }

        // Rhythm from the left: for each left event, look at what the right pattern
        // holds during that event.
        private static Pattern<V> AppLeft<T, U, V>(Pattern<T> left, Pattern<U> right, Func<T, U, V> f) => new Pattern<V>(arc =>
        {
            var result = new List<Hap<V>>();
            foreach (var l in left.Query(arc))
            {
                foreach (var r in right.Query(l.Whole))
                {
                    var part = l.Part.Intersect(r.Part);
                    if (part == null) continue;
                    result.Add(new Hap<V>(l.Whole, part.Value, f(l.Value, r.Value)));
                }
            }
            return result;
        });

        // Every overlap is a new event.
        private static Pattern<V> AppBoth<T, U, V>(Pattern<T> left, Pattern<U> right, Func<T, U, V> f) => new Pattern<V>(arc =>
        {
            var result = new List<Hap<V>>();
            var rs = right.Query(arc);
            foreach (var l in left.Query(arc))
            {
                foreach (var r in rs)
                {
                    var whole = l.Whole.Intersect(r.Whole);
                    var part = l.Part.Intersect(r.Part);
                    if (whole == null || part == null) continue;
                    result.Add(new Hap<V>(whole.Value, part.Value, f(l.Value, r.Value)));
                }
            }
            return result;
        });

        // A pattern of patterns: squeeze one cycle of each inner pattern into the outer
        // event that holds it (~12:35, and the "pattern of patterns" idea at ~14:05).
        public static Pattern<T> SqueezeJoin<T>(Pattern<Pattern<T>> outer) => new Pattern<T>(arc =>
        {
            var result = new List<Hap<T>>();
            foreach (var o in outer.Query(arc))
            {
                Rational wb = o.Whole.Begin;
                Rational len = o.Whole.Duration;
                if (len <= Rational.Zero) continue;
                Func<Rational, Rational> toInner = t => (t - wb) / len;
                Func<Rational, Rational> toOuter = t => wb + t * len;

                foreach (var i in o.Value.Query(o.Part.WithTime(toInner)))
                {
                    var whole = i.Whole.WithTime(toOuter).Intersect(o.Whole);
                    var part = i.Part.WithTime(toOuter).Intersect(o.Part);
                    if (whole == null || part == null) continue;
                    result.Add(new Hap<T>(whole.Value, part.Value, i.Value));
                }
            }
            return result;
        });

        // ── Helpers ───────────────────────────────────────────────────────

        // Deterministic pseudo-random number in [0, 1) for a moment in time.
        public static double TimeToRand(Rational t, int seed = 0)
        {
            unchecked
            {
                ulong x = (ulong)t.Num * 0x9E3779B97F4A7C15UL ^ (ulong)t.Den * 0xC2B2AE3D27D4EB4FUL ^ (ulong)(uint)seed * 0x165667B19E3779F9UL;
                x ^= x >> 33; x *= 0xFF51AFD7ED558CCDUL;
                x ^= x >> 33; x *= 0xC4CEB9FE1A85EC53UL;
                x ^= x >> 33;
                return (x >> 11) * (1.0 / (1UL << 53));
            }
        }

        public static long FloorDiv(long a, long b)
        {
            long q = a / b;
            if ((a % b != 0) && ((a < 0) != (b < 0))) q--;
            return q;
        }

        private static Pattern<T>[] ToArray<T>(IList<Pattern<T>> list)
        {
            var arr = new Pattern<T>[list.Count];
            list.CopyTo(arr, 0);
            return arr;
        }
    }
}
