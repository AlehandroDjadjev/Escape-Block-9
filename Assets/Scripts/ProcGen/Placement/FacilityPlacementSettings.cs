using System;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Placement
{
    [Serializable]
    public sealed class FacilityPlacementSettings
    {
        [SerializeField] private float occupancyPadding;
        [SerializeField] private float overlapTolerance = 0.45f;
        [SerializeField] private int maxBacktrackingSteps = 20000;

        public float OccupancyPadding
        {
            get => occupancyPadding;
            set => occupancyPadding = value;
        }

        public float OverlapTolerance
        {
            get => overlapTolerance;
            set => overlapTolerance = Mathf.Max(0f, value);
        }

        public int MaxBacktrackingSteps
        {
            get => maxBacktrackingSteps;
            set => maxBacktrackingSteps = Math.Max(1, value);
        }
    }
}
