namespace CyberGoal.Core.Util
{
    /// <summary>
    /// A seeded, reproducible random number generator (mulberry32).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>UnityEngine.Random</c> and <c>System.Random</c> are both unusable for
    /// anything that decides a match. Unity's is global mutable state that any
    /// script can disturb, and <c>System.Random</c>'s algorithm is not specified
    /// — it changed between .NET Framework and .NET Core, so the same seed
    /// produces different matches on different runtimes.
    /// </para>
    /// <para>
    /// mulberry32 is thirty characters of arithmetic with a stated algorithm, so
    /// a seed means the same thing forever and on every platform. That is what
    /// makes a replay a replay rather than a re-roll, and what will let a server
    /// re-run a client's match to check it.
    /// </para>
    /// <para>
    /// Any draw that can affect an outcome must come from an instance of this,
    /// never from a static. Cosmetic jitter — crowd animation offsets, particle
    /// lifetimes — is free to use whatever it likes.
    /// </para>
    /// </remarks>
    public sealed class Rng
    {
        private uint _state;

        public Rng(int seed)
        {
            _state = unchecked((uint)seed);
        }

        /// <summary>A double in [0, 1).</summary>
        public double NextDouble()
        {
            unchecked
            {
                _state += 0x6D2B79F5u;
                uint z = _state;
                z = (uint)((z ^ (z >> 15)) * (z | 1u));
                z ^= z + (uint)((z ^ (z >> 7)) * (z | 61u));
                return ((z ^ (z >> 14)) & 0xFFFFFFFFu) / 4294967296.0;
            }
        }

        /// <summary>An int in [minInclusive, maxExclusive).</summary>
        public int NextInt(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + (int)(NextDouble() * (maxExclusive - minInclusive));
        }

        /// <summary>True with the given probability.</summary>
        public bool Chance(double probability) => NextDouble() < probability;
    }
}
