using System;

namespace EscapeBlock9.ProcGen.Runtime
{
    public sealed class SeededRandom
    {
        private readonly Random random;

        public SeededRandom(int seed)
        {
            Seed = seed;
            random = new Random(seed);
        }

        public int Seed { get; }

        public int RangeInclusive(int minInclusive, int maxInclusive)
        {
            if (minInclusive > maxInclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(minInclusive), "Minimum must be less than or equal to maximum.");
            }

            if (minInclusive == maxInclusive)
            {
                return minInclusive;
            }

            long exclusiveMax = (long)maxInclusive + 1L;
            return random.Next(minInclusive, (int)exclusiveMax);
        }

        public float Value01()
        {
            return (float)random.NextDouble();
        }

        public bool Chance(float probability)
        {
            if (probability <= 0f)
            {
                return false;
            }

            if (probability >= 1f)
            {
                return true;
            }

            return Value01() < probability;
        }
    }

    public readonly struct NamedRandomStreams
    {
        public NamedRandomStreams(int masterSeed)
        {
            MasterSeed = masterSeed;
        }

        public int MasterSeed { get; }

        public SeededRandom Stream(string streamName)
        {
            return new SeededRandom(DeriveSeed(MasterSeed, streamName));
        }

        public int DeriveSeed(string streamName)
        {
            return DeriveSeed(MasterSeed, streamName);
        }

        public static int DeriveSeed(int masterSeed, string streamName)
        {
            unchecked
            {
                uint hash = 2166136261u;
                MixByte(ref hash, (byte)masterSeed);
                MixByte(ref hash, (byte)(masterSeed >> 8));
                MixByte(ref hash, (byte)(masterSeed >> 16));
                MixByte(ref hash, (byte)(masterSeed >> 24));

                string name = streamName ?? string.Empty;
                for (int i = 0; i < name.Length; i++)
                {
                    char c = char.ToLowerInvariant(name[i]);
                    MixByte(ref hash, (byte)c);
                    MixByte(ref hash, (byte)(c >> 8));
                }

                hash ^= hash >> 16;
                hash *= 2246822519u;
                hash ^= hash >> 13;
                hash *= 3266489917u;
                hash ^= hash >> 16;

                return (int)(hash & 0x7fffffff);
            }
        }

        private static void MixByte(ref uint hash, byte value)
        {
            hash ^= value;
            hash *= 16777619u;
        }
    }
}
