using System;
using System.Collections.Generic;
using EscapeBlock9.ProcGen.Runtime;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Planning
{
    public sealed class FacilityGraphPlanner
    {
        public FacilityGraph Plan(FacilityGraphPlanConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            FacilityGraphPlanConfig normalized = config.Normalized();
            for (int attempt = 0; attempt < normalized.MaxAttempts; attempt++)
            {
                FacilityGraph graph = BuildAttempt(normalized, attempt);
                if (IsValid(graph))
                {
                    return graph;
                }
            }

            throw new InvalidOperationException($"Failed to build a valid facility graph after {normalized.MaxAttempts} attempt(s).");
        }

        private static FacilityGraph BuildAttempt(FacilityGraphPlanConfig config, int attempt)
        {
            var streams = new NamedRandomStreams(config.MasterSeed);
            SeededRandom mainPathRandom = streams.Stream($"graph-plan/main-path/{attempt}");
            SeededRandom branchRandom = streams.Stream($"graph-plan/branches/{attempt}");
            SeededRandom loopRandom = streams.Stream($"graph-plan/loops/{attempt}");
            SeededRandom exitRandom = streams.Stream($"graph-plan/fire-exits/{attempt}");
            SeededRandom verticalRandom = streams.Stream($"graph-plan/vertical/{attempt}");
            SeededRandom portalRandom = streams.Stream($"graph-plan/portals/{attempt}");

            var graph = new FacilityGraph(config.MasterSeed, attempt);

            IntRange mainRange = config.MainPathLengthRange.Normalized(2);
            int mainPathLength = mainPathRandom.RangeInclusive(mainRange.Min, mainRange.Max);
            bool hasVerticalTransition = mainPathLength >= 3 && verticalRandom.Chance(config.VerticalTransitionChance);
            int verticalIndex = hasVerticalTransition ? verticalRandom.RangeInclusive(1, mainPathLength - 2) : -1;

            int floor = 0;
            for (int i = 0; i < mainPathLength; i++)
            {
                FacilityGraphNodeRole role = i == 0 ? FacilityGraphNodeRole.Start : FacilityGraphNodeRole.MainPath;
                if (i == verticalIndex)
                {
                    role = FacilityGraphNodeRole.Stair;
                }

                graph.AddNode(role, i, -1, i, floor);

                if (i == verticalIndex)
                {
                    floor++;
                }
            }

            for (int i = 0; i < graph.MainPathNodeIds.Count - 1; i++)
            {
                FacilityGraphEdgeRole role = i == verticalIndex - 1 || i == verticalIndex
                    ? FacilityGraphEdgeRole.Stair
                    : FacilityGraphEdgeRole.MainPath;
                graph.AddEdge(graph.MainPathNodeIds[i], graph.MainPathNodeIds[i + 1], role);
            }

            AddBranches(graph, config, branchRandom);
            AddFireExits(graph, config, exitRandom);
            AddLoop(graph, config, loopRandom);
            AddPortal(graph, config, portalRandom);

            return graph;
        }

        private static void AddBranches(FacilityGraph graph, FacilityGraphPlanConfig config, SeededRandom random)
        {
            IntRange countRange = config.BranchCountRange.Normalized(0);
            IntRange lengthRange = config.BranchLengthRange.Normalized(1);
            int branchCount = random.RangeInclusive(countRange.Min, countRange.Max);

            for (int branchId = 0; branchId < branchCount; branchId++)
            {
                int rootMainIndex = random.RangeInclusive(1, Math.Max(1, graph.MainPathNodeIds.Count - 1));
                int rootNodeId = graph.MainPathNodeIds[rootMainIndex];
                int length = random.RangeInclusive(lengthRange.Min, lengthRange.Max);
                int floor = graph.GetNode(rootNodeId).Floor;
                int previousNodeId = rootNodeId;
                var nodeIds = new List<int>();

                for (int depth = 1; depth <= length; depth++)
                {
                    FacilityGraphNodeRole role = depth == length ? FacilityGraphNodeRole.DeadEnd : FacilityGraphNodeRole.Branch;
                    FacilityGraphNode node = graph.AddNode(role, -1, branchId, depth, floor);
                    nodeIds.Add(node.Id);

                    FacilityGraphEdgeRole edgeRole = depth == length ? FacilityGraphEdgeRole.DeadEnd : FacilityGraphEdgeRole.Branch;
                    graph.AddEdge(previousNodeId, node.Id, edgeRole);
                    previousNodeId = node.Id;
                }

                graph.AddBranch(new FacilityGraphBranch(branchId, rootNodeId, nodeIds, FacilityGraphEdgeRole.Branch));
            }
        }

        private static void AddFireExits(FacilityGraph graph, FacilityGraphPlanConfig config, SeededRandom random)
        {
            if (!random.Chance(config.FireExitChance))
            {
                return;
            }

            List<int> eligibleMainPathIndices = BuildEligibleFireExitMainPathIndices(graph, config);
            if (eligibleMainPathIndices.Count == 0)
            {
                return;
            }

            IntRange countRange = config.FireExitCountRange.Normalized(0);
            int fireExitCount = random.RangeInclusive(countRange.Min, countRange.Max);
            for (int i = 0; i < fireExitCount; i++)
            {
                if (eligibleMainPathIndices.Count == 0)
                {
                    break;
                }

                int pick = random.RangeInclusive(0, eligibleMainPathIndices.Count - 1);
                int rootMainIndex = eligibleMainPathIndices[pick];
                eligibleMainPathIndices.RemoveAt(pick);
                int rootNodeId = graph.MainPathNodeIds[rootMainIndex];
                int floor = graph.GetNode(rootNodeId).Floor;
                int branchId = graph.Branches.Count;
                FacilityGraphNode exitNode = graph.AddNode(FacilityGraphNodeRole.FireExit, -1, branchId, 1, floor);
                graph.AddEdge(rootNodeId, exitNode.Id, FacilityGraphEdgeRole.FireExit);
                graph.AddBranch(new FacilityGraphBranch(branchId, rootNodeId, new[] { exitNode.Id }, FacilityGraphEdgeRole.FireExit));
            }
        }

        private static List<int> BuildEligibleFireExitMainPathIndices(FacilityGraph graph, FacilityGraphPlanConfig config)
        {
            var indices = new List<int>();
            int minIndex = config.AllowFireExitNearStart ? 1 : Math.Max(1, config.MinimumMainPathDistanceForFireExit);
            int maxIndex = Math.Max(1, graph.MainPathNodeIds.Count - 1);
            if (minIndex > maxIndex)
            {
                return indices;
            }

            for (int i = minIndex; i <= maxIndex; i++)
            {
                indices.Add(i);
            }

            return indices;
        }

        private static void AddLoop(FacilityGraph graph, FacilityGraphPlanConfig config, SeededRandom random)
        {
            if (!random.Chance(config.LoopChance) || graph.Nodes.Count < 4)
            {
                return;
            }

            TryAddNonDuplicateEdge(graph, random, FacilityGraphEdgeRole.Loop, true);
        }

        private static void AddPortal(FacilityGraph graph, FacilityGraphPlanConfig config, SeededRandom random)
        {
            if (!random.Chance(config.PortalChance) || graph.Nodes.Count < 4)
            {
                return;
            }

            if (TryAddNonDuplicateEdge(graph, random, FacilityGraphEdgeRole.Portal, false))
            {
                MarkPortalEndpoint(graph, graph.Edges[graph.Edges.Count - 1].FromNodeId);
                MarkPortalEndpoint(graph, graph.Edges[graph.Edges.Count - 1].ToNodeId);
            }
        }

        private static bool TryAddNonDuplicateEdge(FacilityGraph graph, SeededRandom random, FacilityGraphEdgeRole role, bool bidirectional)
        {
            for (int attempt = 0; attempt < 16; attempt++)
            {
                int first = random.RangeInclusive(0, graph.Nodes.Count - 1);
                int second = random.RangeInclusive(0, graph.Nodes.Count - 1);
                if (first == second || graph.HasEdgeBetween(first, second))
                {
                    continue;
                }

                graph.AddEdge(first, second, role, bidirectional);
                return true;
            }

            return false;
        }

        private static void MarkPortalEndpoint(FacilityGraph graph, int nodeId)
        {
            FacilityGraphNode node = graph.GetNode(nodeId);
            if (node.Role != FacilityGraphNodeRole.Start && node.Role != FacilityGraphNodeRole.Stair)
            {
                node.Role = FacilityGraphNodeRole.Portal;
            }
        }

        private static bool IsValid(FacilityGraph graph)
        {
            if (graph.MainPathNodeIds.Count < 2 || graph.Nodes.Count < graph.MainPathNodeIds.Count)
            {
                return false;
            }

            for (int i = 0; i < graph.MainPathNodeIds.Count - 1; i++)
            {
                if (!graph.HasEdgeBetween(graph.MainPathNodeIds[i], graph.MainPathNodeIds[i + 1]))
                {
                    return false;
                }
            }

            return true;
        }
    }

    public static class FacilityGraphDebug
    {
        public static string BuildDebugText(FacilityGraph graph)
        {
            return graph != null ? graph.ToDebugString() : "<null graph>";
        }

        public static void Log(FacilityGraph graph)
        {
            Debug.Log(BuildDebugText(graph));
        }
    }
}
