using System;
using System.Collections.Generic;
using EscapeBlock9.ProcGen.Data;
using EscapeBlock9.ProcGen.Placement;
using EscapeBlock9.ProcGen.Planning;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Debugging
{
    [Serializable]
    public struct ModuleUsageCount
    {
        public string ModuleId;
        public int Count;
    }

    [Serializable]
    public struct FailureReasonCount
    {
        public PlacementFailureReason Reason;
        public int Count;
    }

    [Serializable]
    public struct DebugNodeRecord
    {
        public int NodeId;
        public string ModuleId;
        public FacilityGraphNodeRole Role;
        public bool IsMainPath;
        public int MainPathIndex;
        public Vector3 WorldPosition;
        public Vector3 Size;
    }

    [Serializable]
    public struct DebugOccupancyRecord
    {
        public int NodeId;
        public Bounds Bounds;
    }

    [Serializable]
    public struct DebugEdgeRecord
    {
        public int EdgeId;
        public int FromNodeId;
        public int ToNodeId;
        public FacilityGraphEdgeRole Role;
        public bool Connected;
    }

    [Serializable]
    public struct GenerationStatistics
    {
        public int Seed;
        public int BranchCount;
        public int DeadEndCount;
        public int LoopCount;
        public int FireExitCount;
        public int PortalCount;
        public int PlacementAttempts;
        public float GenerationDurationSeconds;
    }

    [DisallowMultipleComponent]
    public sealed class FacilityGenerationDebugData : MonoBehaviour
    {
        [SerializeField] private GenerationStatistics statistics;
        [SerializeField] private List<ModuleUsageCount> moduleUsage = new List<ModuleUsageCount>();
        [SerializeField] private List<FailureReasonCount> failureReasons = new List<FailureReasonCount>();
        [SerializeField] private List<DebugNodeRecord> nodes = new List<DebugNodeRecord>();
        [SerializeField] private List<DebugEdgeRecord> edges = new List<DebugEdgeRecord>();
        [SerializeField] private List<DebugOccupancyRecord> occupancy = new List<DebugOccupancyRecord>();
        [SerializeField] private string failedSeedSummary;

        public GenerationStatistics Statistics => statistics;
        public IReadOnlyList<ModuleUsageCount> ModuleUsage => moduleUsage;
        public IReadOnlyList<FailureReasonCount> FailureReasons => failureReasons;
        public IReadOnlyList<DebugNodeRecord> Nodes => nodes;
        public IReadOnlyList<DebugEdgeRecord> Edges => edges;
        public IReadOnlyList<DebugOccupancyRecord> Occupancy => occupancy;
        public string FailedSeedSummary => failedSeedSummary;

        public void Apply(
            GenerationStatistics statistics,
            IReadOnlyList<ModuleUsageCount> moduleUsage,
            IReadOnlyList<FailureReasonCount> failureReasons,
            IReadOnlyList<DebugNodeRecord> nodes,
            IReadOnlyList<DebugEdgeRecord> edges,
            IReadOnlyList<DebugOccupancyRecord> occupancy,
            string failedSeedSummary)
        {
            this.statistics = statistics;
            this.failedSeedSummary = failedSeedSummary ?? string.Empty;
            this.moduleUsage.Clear();
            this.failureReasons.Clear();
            this.nodes.Clear();
            this.edges.Clear();
            this.occupancy.Clear();

            if (moduleUsage != null)
            {
                this.moduleUsage.AddRange(moduleUsage);
            }

            if (failureReasons != null)
            {
                this.failureReasons.AddRange(failureReasons);
            }

            if (nodes != null)
            {
                this.nodes.AddRange(nodes);
            }

            if (edges != null)
            {
                this.edges.AddRange(edges);
            }

            if (occupancy != null)
            {
                this.occupancy.AddRange(occupancy);
            }
        }
    }
}
