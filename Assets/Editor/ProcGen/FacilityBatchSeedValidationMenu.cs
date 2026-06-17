using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using EscapeBlock9.ProcGen.Authoring;
using EscapeBlock9.ProcGen.Data;
using EscapeBlock9.ProcGen.Placement;
using EscapeBlock9.ProcGen.Planning;
using EscapeBlock9.ProcGen.Population;
using UnityEditor;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Editor
{
    public static class FacilityBatchSeedValidationMenu
    {
        private const string CatalogPath = "Assets/ProcGen/Catalogs/InitialBlock9TileCatalog.asset";
        private const int DefaultSeedStart = 1000;
        private const int DefaultSeedCount = 100;
        private const string ReportPath = "Assets/Docs/ProcGen/FacilityHardeningReport.md";

        [MenuItem("Tools/ProcGen/Validate Seed Batch (100 seeds)")]
        public static void ValidateDefaultBatch()
        {
            ValidateBatch(DefaultSeedStart, DefaultSeedCount);
        }

        private static void ValidateBatch(int seedStart, int seedCount)
        {
            TileCatalog catalog = AssetDatabase.LoadAssetAtPath<TileCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError($"Seed batch validation failed: missing catalog at {CatalogPath}.");
                return;
            }

            int passCount = 0;
            var failures = new List<string>();
            var moduleUsageCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var failureReasonCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < seedCount; i++)
            {
                int seed = seedStart + i;
                if (ValidateSeed(seed, catalog, moduleUsageCounts, out string failure))
                {
                    passCount++;
                }
                else
                {
                    failures.Add($"seed={seed} :: {failure}");
                    string normalizedReason = NormalizeFailureReason(failure);
                    failureReasonCounts[normalizedReason] = failureReasonCounts.TryGetValue(normalizedReason, out int count) ? count + 1 : 1;
                }
            }

            float failRate = seedCount <= 0 ? 0f : (float)failures.Count / seedCount;
            Debug.Log($"ProcGen seed batch validation complete. Passed={passCount} Failed={failures.Count} FailRate={failRate:P2} Start={seedStart} Count={seedCount}");
            for (int i = 0; i < failures.Count; i++)
            {
                Debug.LogError(failures[i]);
            }

            WriteHardeningReport(seedStart, seedCount, passCount, failures, failureReasonCounts, moduleUsageCounts);
            AssetDatabase.Refresh();
        }

        private static bool ValidateSeed(int seed, TileCatalog catalog, IDictionary<string, int> moduleUsageCounts, out string failure)
        {
            failure = string.Empty;
            FacilityGraph graph;
            try
            {
                graph = new FacilityGraphPlanner().Plan(BuildConfig(seed));
            }
            catch (Exception ex)
            {
                failure = $"graph planning exception: {ex.Message}";
                return false;
            }

            ResolvedFacilityLayout layout = new CustomFacilityLayoutSolver().Solve(graph, catalog, seed);
            if (layout.Tiles.Count != graph.Nodes.Count)
            {
                failure = $"layout incomplete: nodes={graph.Nodes.Count} tiles={layout.Tiles.Count}";
                return false;
            }

            for (int i = 0; i < layout.Tiles.Count; i++)
            {
                string moduleId = layout.Tiles[i].ModuleId ?? "<unknown>";
                moduleUsageCounts[moduleId] = moduleUsageCounts.TryGetValue(moduleId, out int count) ? count + 1 : 1;
            }

            if (OccupancyValidator.AnyOverlap(layout.Tiles, 0.45f, out string overlap))
            {
                failure = $"occupancy overlap: {overlap}";
                return false;
            }

            if (!AllMainPathEdgesConnected(graph, layout))
            {
                failure = "main path edge missing physical connection";
                return false;
            }

            if (!FireExitsReachable(graph))
            {
                failure = "fire exit node unreachable from start";
                return false;
            }

            if (!ValidateResolvedMetadata(seed, graph, layout, out failure))
            {
                return false;
            }

            return true;
        }

        private static void WriteHardeningReport(
            int seedStart,
            int seedCount,
            int passCount,
            IReadOnlyList<string> failures,
            Dictionary<string, int> failureReasonCounts,
            Dictionary<string, int> moduleUsageCounts)
        {
            int failCount = Mathf.Max(0, seedCount - passCount);
            float failRate = seedCount <= 0 ? 0f : (float)failCount / seedCount;
            int totalModulePlacements = 0;
            foreach (KeyValuePair<string, int> pair in moduleUsageCounts)
            {
                totalModulePlacements += pair.Value;
            }

            var failureRanking = new List<KeyValuePair<string, int>>(failureReasonCounts);
            failureRanking.Sort((a, b) => b.Value.CompareTo(a.Value));

            var moduleRanking = new List<KeyValuePair<string, int>>(moduleUsageCounts);
            moduleRanking.Sort((a, b) => b.Value.CompareTo(a.Value));

            var builder = new StringBuilder();
            builder.AppendLine("# Facility Hardening Batch Report");
            builder.AppendLine();
            builder.AppendLine($"Generated: {DateTime.UtcNow:O} UTC");
            builder.AppendLine();
            builder.AppendLine("## Batch Summary");
            builder.AppendLine();
            builder.AppendLine($"- Seed range: `{seedStart}` to `{seedStart + Mathf.Max(0, seedCount - 1)}`");
            builder.AppendLine($"- Seeds tested: `{seedCount}`");
            builder.AppendLine($"- Passed: `{passCount}`");
            builder.AppendLine($"- Failed: `{failCount}`");
            builder.AppendLine($"- Failure rate: `{failRate:P2}`");
            builder.AppendLine();
            builder.AppendLine("## Common Failure Reasons");
            builder.AppendLine();
            if (failureRanking.Count == 0)
            {
                builder.AppendLine("- None in this batch.");
            }
            else
            {
                int max = Mathf.Min(10, failureRanking.Count);
                for (int i = 0; i < max; i++)
                {
                    KeyValuePair<string, int> pair = failureRanking[i];
                    builder.AppendLine($"- `{pair.Key}`: `{pair.Value}`");
                }
            }

            builder.AppendLine();
            builder.AppendLine("## Most Used Modules");
            builder.AppendLine();
            if (moduleRanking.Count == 0)
            {
                builder.AppendLine("- No successful layout placements.");
            }
            else
            {
                int max = Mathf.Min(12, moduleRanking.Count);
                for (int i = 0; i < max; i++)
                {
                    KeyValuePair<string, int> pair = moduleRanking[i];
                    float usageShare = totalModulePlacements <= 0 ? 0f : (float)pair.Value / totalModulePlacements;
                    builder.AppendLine($"- `{pair.Key}`: `{pair.Value}` placements (`{usageShare:P1}`)");
                }
            }

            builder.AppendLine();
            builder.AppendLine("## Failed Seeds");
            builder.AppendLine();
            if (failures.Count == 0)
            {
                builder.AppendLine("- None.");
            }
            else
            {
                int max = Mathf.Min(30, failures.Count);
                for (int i = 0; i < max; i++)
                {
                    builder.AppendLine($"- {failures[i]}");
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? "Assets/Docs/ProcGen");
            File.WriteAllText(ReportPath, builder.ToString());
            Debug.Log($"Wrote hardening report to {ReportPath}");
        }

        private static string NormalizeFailureReason(string failure)
        {
            if (string.IsNullOrWhiteSpace(failure))
            {
                return "<unknown>";
            }

            int index = failure.IndexOf(':');
            if (index > 0)
            {
                return failure.Substring(0, index).Trim();
            }

            return failure.Trim();
        }

        private static bool ValidateResolvedMetadata(int seed, FacilityGraph graph, ResolvedFacilityLayout layout, out string failure)
        {
            var root = new GameObject($"SeedValidation_{seed}");
            var tilesRoot = new GameObject("Tiles");
            tilesRoot.transform.SetParent(root.transform, false);
            var instanceTiles = new Dictionary<int, Tile>();
            failure = string.Empty;

            try
            {
                for (int i = 0; i < layout.Tiles.Count; i++)
                {
                    PlacedTile placed = layout.Tiles[i];
                    GameObject instance = UnityEngine.Object.Instantiate(placed.Definition.Prefab, tilesRoot.transform);
                    instance.transform.SetPositionAndRotation(placed.Position, placed.Rotation);
                    Tile tile = instance.GetComponent<Tile>();
                    if (tile != null)
                    {
                        instanceTiles[placed.NodeId] = tile;
                    }
                }

                PostLayoutConnectionResolution resolution = new PostLayoutConnectionResolver().Resolve(layout, graph, instanceTiles);
                if (!PortalPairsValid(graph, resolution))
                {
                    failure = "portal pair metadata invalid";
                    return false;
                }

                if (!BlockedDoorwaysHaveBlockers(resolution, instanceTiles))
                {
                    failure = "blocked doorway missing blocker visual reference";
                    return false;
                }

                var metadata = root.AddComponent<PostLayoutConnectionMetadata>();
                metadata.Apply(resolution);
                Transform populationRoot = new GameObject("Population").transform;
                populationRoot.SetParent(root.transform, false);

                FacilityPopulationReport population = new FacilityPopulationPipeline(new FacilityPopulationSettings
                {
                    EnableVerbosePopulationLogs = false
                }).Populate(populationRoot, graph, layout, instanceTiles, metadata);

                if (!UsedPopulationMarkersAreSafe(population))
                {
                    failure = "population marker safety failed (used marker flagged blocked/rule-skipped)";
                    return false;
                }

                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static FacilityGraphPlanConfig BuildConfig(int seed)
        {
            var config = FacilityGraphPlanConfig.CreateDefault(seed);
            config.MainPathLengthRange = new IntRange(6, 8);
            config.BranchCountRange = new IntRange(2, 3);
            config.BranchLengthRange = new IntRange(1, 2);
            config.FireExitChance = 1f;
            config.FireExitCountRange = new IntRange(1, 1);
            config.AllowFireExitNearStart = false;
            config.MinimumMainPathDistanceForFireExit = 2;
            config.PortalChance = 0.35f;
            config.LoopChance = 0.45f;
            config.VerticalTransitionChance = 0.35f;
            config.MaxAttempts = 6;
            return config;
        }

        private static bool AllMainPathEdgesConnected(FacilityGraph graph, ResolvedFacilityLayout layout)
        {
            var edgeConnectionLookup = new HashSet<int>();
            for (int i = 0; i < layout.Connections.Count; i++)
            {
                edgeConnectionLookup.Add(layout.Connections[i].EdgeId);
            }

            for (int i = 0; i < graph.Edges.Count; i++)
            {
                FacilityGraphEdge edge = graph.Edges[i];
                if (edge.Role != FacilityGraphEdgeRole.MainPath && edge.Role != FacilityGraphEdgeRole.Stair)
                {
                    continue;
                }

                if (!edgeConnectionLookup.Contains(edge.Id))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool FireExitsReachable(FacilityGraph graph)
        {
            if (graph.MainPathNodeIds.Count == 0)
            {
                return false;
            }

            int start = graph.MainPathNodeIds[0];
            var visited = new HashSet<int> { start };
            var queue = new Queue<int>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                int nodeId = queue.Dequeue();
                for (int i = 0; i < graph.Edges.Count; i++)
                {
                    FacilityGraphEdge edge = graph.Edges[i];
                    if (edge.FromNodeId != nodeId && edge.ToNodeId != nodeId)
                    {
                        continue;
                    }

                    int next = edge.FromNodeId == nodeId ? edge.ToNodeId : edge.FromNodeId;
                    if (visited.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                if (graph.Nodes[i].Role == FacilityGraphNodeRole.FireExit && !visited.Contains(graph.Nodes[i].Id))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool PortalPairsValid(FacilityGraph graph, PostLayoutConnectionResolution resolution)
        {
            int expectedPortalEdges = 0;
            for (int i = 0; i < graph.Edges.Count; i++)
            {
                if (graph.Edges[i].Role == FacilityGraphEdgeRole.Portal)
                {
                    expectedPortalEdges++;
                }
            }

            if (expectedPortalEdges == 0)
            {
                return true;
            }

            if (resolution.PortalPairs.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < resolution.PortalPairs.Count; i++)
            {
                PortalPairMetadata pair = resolution.PortalPairs[i];
                if (!pair.HasResolvedDoorways)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool BlockedDoorwaysHaveBlockers(PostLayoutConnectionResolution resolution, IReadOnlyDictionary<int, Tile> instanceTiles)
        {
            for (int i = 0; i < resolution.Doorways.Count; i++)
            {
                ResolvedDoorwayMetadata doorway = resolution.Doorways[i];
                if (doorway.ResolutionKind != DoorwayResolutionKind.Blocked)
                {
                    continue;
                }

                if (!instanceTiles.TryGetValue(doorway.NodeId, out Tile tile) || tile == null)
                {
                    return false;
                }

                Doorway[] doorways = tile.GetDoorways();
                if (doorway.DoorwayIndex < 0 || doorway.DoorwayIndex >= doorways.Length)
                {
                    return false;
                }

                Doorway authoredDoorway = doorways[doorway.DoorwayIndex];
                if (authoredDoorway == null || !authoredDoorway.HasBlockerReference)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool UsedPopulationMarkersAreSafe(FacilityPopulationReport population)
        {
            for (int i = 0; i < population.MarkerUsage.Count; i++)
            {
                PopulationMarkerUsage marker = population.MarkerUsage[i];
                if (marker.Status == PopulationMarkerStatus.Used &&
                    (marker.Reason?.IndexOf("blocked", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
