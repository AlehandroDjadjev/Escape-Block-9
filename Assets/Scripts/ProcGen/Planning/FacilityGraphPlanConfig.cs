using System;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Planning
{
    [Serializable]
    public struct IntRange
    {
        [SerializeField] private int min;
        [SerializeField] private int max;

        public IntRange(int min, int max)
        {
            this.min = min;
            this.max = max;
        }

        public int Min => min;
        public int Max => max;

        public IntRange Normalized(int minimumAllowed = 0)
        {
            int normalizedMin = Math.Max(minimumAllowed, min);
            int normalizedMax = Math.Max(normalizedMin, max);
            return new IntRange(normalizedMin, normalizedMax);
        }
    }

    [Serializable]
    public sealed class FacilityGraphPlanConfig
    {
        [SerializeField] private int masterSeed = 12345;
        [SerializeField] private IntRange mainPathLengthRange = new IntRange(5, 8);
        [SerializeField] private IntRange branchCountRange = new IntRange(1, 3);
        [SerializeField] private IntRange branchLengthRange = new IntRange(1, 3);
        [Range(0f, 1f)]
        [SerializeField] private float loopChance = 0.25f;
        [SerializeField] private IntRange fireExitCountRange = new IntRange(0, 1);
        [Range(0f, 1f)]
        [SerializeField] private float fireExitChance = 0.5f;
        [SerializeField] private bool allowFireExitNearStart;
        [SerializeField] private int minimumMainPathDistanceForFireExit = 2;
        [Range(0f, 1f)]
        [SerializeField] private float verticalTransitionChance = 0.25f;
        [Range(0f, 1f)]
        [SerializeField] private float portalChance;
        [SerializeField] private int maxAttempts = 8;

        public int MasterSeed
        {
            get => masterSeed;
            set => masterSeed = value;
        }

        public IntRange MainPathLengthRange
        {
            get => mainPathLengthRange;
            set => mainPathLengthRange = value;
        }

        public IntRange BranchCountRange
        {
            get => branchCountRange;
            set => branchCountRange = value;
        }

        public IntRange BranchLengthRange
        {
            get => branchLengthRange;
            set => branchLengthRange = value;
        }

        public float LoopChance
        {
            get => loopChance;
            set => loopChance = Clamp01(value);
        }

        public IntRange FireExitCountRange
        {
            get => fireExitCountRange;
            set => fireExitCountRange = value;
        }

        public float FireExitChance
        {
            get => fireExitChance;
            set => fireExitChance = Clamp01(value);
        }

        public bool AllowFireExitNearStart
        {
            get => allowFireExitNearStart;
            set => allowFireExitNearStart = value;
        }

        public int MinimumMainPathDistanceForFireExit
        {
            get => minimumMainPathDistanceForFireExit;
            set => minimumMainPathDistanceForFireExit = Math.Max(0, value);
        }

        public float VerticalTransitionChance
        {
            get => verticalTransitionChance;
            set => verticalTransitionChance = Clamp01(value);
        }

        public float PortalChance
        {
            get => portalChance;
            set => portalChance = Clamp01(value);
        }

        public int MaxAttempts
        {
            get => maxAttempts;
            set => maxAttempts = Math.Max(1, value);
        }

        public static FacilityGraphPlanConfig CreateDefault(int masterSeed)
        {
            return new FacilityGraphPlanConfig { MasterSeed = masterSeed };
        }

        public FacilityGraphPlanConfig Normalized()
        {
            return new FacilityGraphPlanConfig
            {
                MasterSeed = masterSeed,
                MainPathLengthRange = mainPathLengthRange.Normalized(2),
                BranchCountRange = branchCountRange.Normalized(0),
                BranchLengthRange = branchLengthRange.Normalized(1),
                LoopChance = loopChance,
                FireExitCountRange = fireExitCountRange.Normalized(0),
                FireExitChance = fireExitChance,
                AllowFireExitNearStart = allowFireExitNearStart,
                MinimumMainPathDistanceForFireExit = minimumMainPathDistanceForFireExit,
                VerticalTransitionChance = verticalTransitionChance,
                PortalChance = portalChance,
                MaxAttempts = maxAttempts,
            };
        }

        private static float Clamp01(float value)
        {
            if (value <= 0f)
            {
                return 0f;
            }

            return value >= 1f ? 1f : value;
        }
    }
}
