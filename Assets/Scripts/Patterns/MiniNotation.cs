using System;
using System.Collections.Generic;
using System.Globalization;

namespace Synthic.Patterns
{
    // A small subset of Strudel's "mini-notation", so a pattern can be typed into the
    // Inspector as a string instead of written in code. Values are whole numbers —
    // scale steps for notes, or octave offsets.
    //
    //   "0 2 4 7"       four steps, squeezed into one cycle
    //   "0 [2 4] 7"     brackets subdivide: 2 and 4 share one slot
    //   "0 <2 4> 7"     angle brackets alternate: 2 on the first cycle, 4 on the next
    //   "0 ~ 4 ~"       ~ is a rest (the hop still happens, silently)
    //   "0 2, 7 9"      comma layers sequences on top of each other (a stack)
    //   "0*2 4"         * plays an element faster (here 0 0 4)
    //   "[0 2 4]/2"     / plays an element slower (stretches over 2 cycles)
    //   "0!3 4"         ! repeats an element (0 0 0 4)
    //   "0 2? 4"        ? drops an element about half the time (or "2?0.25" for 25%)
    //   "-3 0 4"        negative steps go below the root
    public static class Mini
    {
        public static Pattern<int> Parse(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Pattern.Silence<int>();
            var parser = new Parser(text);
            var result = parser.ParseStack('\0', bracket: '[');
            parser.SkipSpace();
            if (!parser.AtEnd) throw parser.Error($"unexpected '{parser.Peek}'");
            return result;
        }

        public static bool TryParse(string text, out Pattern<int> pattern, out string error)
        {
            try
            {
                pattern = Parse(text);
                error = null;
                return true;
            }
            catch (FormatException e)
            {
                pattern = Pattern.Silence<int>();
                error = e.Message;
                return false;
            }
        }

        private sealed class Parser
        {
            private readonly string _s;
            private int _i;
            private int _degradeSeed;

            public Parser(string s) { _s = s; }

            public bool AtEnd => _i >= _s.Length;
            public char Peek => AtEnd ? '\0' : _s[_i];

            public FormatException Error(string msg) =>
                new FormatException($"Pattern \"{_s}\": {msg} at position {_i + 1}");

            public void SkipSpace()
            {
                while (!AtEnd && (char.IsWhiteSpace(Peek) || Peek == '|')) _i++;
            }

            // A list of sequences separated by commas, until `close`.
            // bracket '[' : each sequence is squeezed into one cycle (fastcat)
            // bracket '<' : each sequence plays one element per cycle (slowcat)
            public Pattern<int> ParseStack(char close, char bracket)
            {
                var layers = new List<Pattern<int>>();
                while (true)
                {
                    var terms = ParseTerms(close);
                    if (terms.Count == 0) throw Error("empty sequence");
                    layers.Add(terms.Count == 1 ? terms[0]
                        : bracket == '<' ? Pattern.SlowCat(terms) : Pattern.FastCat(terms));
                    SkipSpace();
                    if (Peek == ',') { _i++; continue; }
                    break;
                }
                return layers.Count == 1 ? layers[0] : Pattern.Stack(layers);
            }

            private List<Pattern<int>> ParseTerms(char close)
            {
                var terms = new List<Pattern<int>>();
                while (true)
                {
                    SkipSpace();
                    if (AtEnd || Peek == close || Peek == ',') break;
                    if (Peek == ']' || Peek == '>') throw Error($"unmatched '{Peek}'");
                    ParseTerm(terms);
                }
                return terms;
            }

            private void ParseTerm(List<Pattern<int>> into)
            {
                Pattern<int> p = ParseAtom();
                int repeat = 1;

                while (!AtEnd)
                {
                    char c = Peek;
                    if (c == '*') { _i++; p = p.Fast(Rational.FromDouble(ParseNumber())); }
                    else if (c == '/') { _i++; p = p.Slow(Rational.FromDouble(ParseNumber())); }
                    else if (c == '?')
                    {
                        _i++;
                        double chance = StartsNumber() ? ParseNumber() : 0.5;
                        p = p.Degrade(chance, ++_degradeSeed);
                    }
                    else if (c == '!')
                    {
                        _i++;
                        repeat += StartsNumber() ? Math.Max(0, (int)ParseNumber() - 1) : 1;
                    }
                    else break;
                }

                for (int k = 0; k < repeat; k++) into.Add(p);
            }

            private Pattern<int> ParseAtom()
            {
                char c = Peek;
                if (c == '[')
                {
                    _i++;
                    var p = ParseStack(']', '[');
                    Expect(']');
                    return p;
                }
                if (c == '<')
                {
                    _i++;
                    var p = ParseStack('>', '<');
                    Expect('>');
                    return p;
                }
                if (c == '~')
                {
                    _i++;
                    return Pattern.Silence<int>();
                }
                if (StartsNumber())
                {
                    int start = _i;
                    double v = ParseNumber();
                    if (Math.Abs(v - Math.Round(v)) > 1e-9)
                    {
                        _i = start;
                        throw Error("steps must be whole numbers");
                    }
                    return Pattern.Pure((int)Math.Round(v));
                }
                throw Error($"unexpected '{c}'");
            }

            private void Expect(char c)
            {
                SkipSpace();
                if (Peek != c) throw Error($"expected '{c}'");
                _i++;
            }

            private bool StartsNumber()
            {
                if (AtEnd) return false;
                char c = Peek;
                if (char.IsDigit(c) || c == '.') return true;
                return c == '-' && _i + 1 < _s.Length && (char.IsDigit(_s[_i + 1]) || _s[_i + 1] == '.');
            }

            private double ParseNumber()
            {
                int start = _i;
                if (Peek == '-') _i++;
                while (!AtEnd && (char.IsDigit(Peek) || Peek == '.')) _i++;
                string token = _s.Substring(start, _i - start);
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                {
                    _i = start;
                    throw Error($"bad number '{token}'");
                }
                return v;
            }
        }
    }
}
