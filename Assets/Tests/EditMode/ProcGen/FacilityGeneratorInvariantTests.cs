using System.Collections.Generic;
using EscapeBlock9.ProcGen.Authoring;
using EscapeBlock9.ProcGen.Data;
using EscapeBlock9.ProcGen.Placement;
using EscapeBlock9.ProcGen.Planning;
using EscapeBlock9.ProcGen.Population;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Tests
{
    public sealed class FacilityGeneratorInvariantTests
    {
        private const string CatalogPath = "Assets/ProcGen/Catalogs/InitialBlock9TileCatalog.asset";

        [Test]
        public void DeterministicGraphGenerationWithSameSeed()
        {
            FacilityGraphPlanConfig config = BuildConfig(38191, includePortal: true);
            var planner = new FacilityGraphPlanner();
            Assert.AreEqual(planner.Plan(config).ToDebugString(), planner.Plan(config).ToDebugString());
        }

        [Test]
        public void DeterministicPhysicalPlacementWithSameSeed()
        {
            TileCatalog catalog = LoadCatalog();
            FacilityGraph graph = new FacilityGraphPlanner().Plan(BuildConfig(38191, includePortal: false));
            var solver = new CustomFacilityLayoutSolver();
            Assert.AreEqual(solver.Solve(graph, catalog, 38191).ToDebugString(), solver.Solve(graph, catalog, 38191).ToDebugString());
        }

        [Test]
        public void MainPathConnectivityAndNoOverlapAreMaintained()
        {
            TileCatalog catalog = LoadCatalog();
            FacilityGraph graph = new FacilityGraphPlanner().Plan(BuildConfig(64021, includePortal: false));
            ResolvedFacilityLayout layout = new CustomFacilityLayoutSolver().Solve(graph, catalog, 64021);

            Assert.AreEqual(graph.Nodes.Count, layout.Tiles.Count, layout.Diagnostics.ToDebugString());
            Assert.IsFalse(OccupancyValidator.AnyOverlap(layout.Tiles, 0.45f, out string overlap), overlap);
            Assert.IsTrue(EveryMainPathEdgeHasPhysicalConnection(graph, layout), "Main path connectivity broken in physical layout.");
        }

        [Test]
        public void FireExitNodesRemainReachable()
        {
            TileCatalog catalog = LoadCatalog();
            FacilityGraph graph = new FacilityGraphPlanner().Plan(BuildConfig(64022, includePortal: false));
            ResolvedFacilityLayout layout = new CustomFacilityLayoutSolver().Solve(graph, catalog, 64022);

            Assert.AreEqual(graph.Nodes.Count, layout.Tiles.Count, layout.Diagnostics.ToDebugString());
            Assert.IsTrue(FireExitsReachableViaLayout(graph, layout), "Fire exit node is unreachable from start node.");
        }

        [Test]
        public void PortalPairsAreValidWhenPortalEdgesEnabled()
        {
            TileCatalog catalog = LoadCatalog();
            FacilityGraph graph = new FacilityGraphPlanner().Plan(BuildConfig(77801, includePortal: true));
            ResolvedFacilityLayout layout = new CustomFacilityLayoutSolver().Solve(graph, catalog, 77801);
            Assert.AreEqual(graph.Nodes.Count, layout.Tiles.Count, layout.Diagnostics.ToDebugString());

            PostLayoutConnectionResolution resolution = BuildResolution(layout, graph, out GameObject root, out _);
            try
            {
                int portalEdges = CountEdges(graph, FacilityGraphEdgeRole.Portal);
                if (portalEdges > 0)
                {
                    Assert.Greater(resolution.PortalPairs.Count, 0);
                    for (int i = 0; i < resolution.PortalPairs.Count; i++)
                    {
                        Assert.IsTrue(resolution.PortalPairs[i].HasResolvedDoorways);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BlockedDoorwaysExposeBlockerReferences()
        {
            TileCatalog catalog = LoadCatalog();
            FacilityGraph graph = new FacilityGraphPlanner().Plan(BuildConfig(90111, includePortal: false));
            ResolvedFacilityLayout layout = new CustomFacilityLayoutSolver().Solve(graph, catalog, 90111);
            Assert.AreEqual(graph.Nodes.Count, layout.Tiles.Count, layout.Diagnostics.ToDebugString());

            PostLayoutConnectionResolution resolution = BuildResolution(layout, graph, out GameObject root, out Dictionary<int, Tile> instanceTiles);
            try
            {
                for (int i = 0; i < resolution.Doorways.Count; i++)
                {
                    ResolvedDoorwayMetadata doorway = resolution.Doorways[i];
                    if (doorway.ResolutionKind != DoorwayResolutionKind.Blocked)
                    {
                        continue;
                    }

                    Assert.IsTrue(instanceTiles.TryGetValue(doorway.NodeId, out Tile tile));
                    Doorway[] authoredDoorways = tile.GetDoorways();
                    Assert.GreaterOrEqual(doorway.DoorwayIndex, 0);
                    Assert.Less(doorway.DoorwayIndex, authoredDoorways.Length);
                    Assert.IsTrue(authoredDoorways[doorway.DoorwayIndex].HasBlockerReference, $"Blocked doorway at node {doorway.NodeId} index {doorway.DoorwayIndex} lacks blocker visuals.");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PopulationUsedMarkersRemainSafetyFiltered()
        {
            TileCatalog catalog = LoadCatalog();
            FacilityGraph graph = new FacilityGraphPlanner().Plan(BuildConfig(90222, includePortal: false));
            ResolvedFacilityLayout layout = new CustomFacilityLayoutSolver().Solve(graph, catalog, 90222);
            Assert.AreEqual(graph.Nodes.Count, layout.Tiles.Count, layout.Diagnostics.ToDebugString());

            PostLayoutConnectionResolution resolution = BuildResolution(layout, graph, out GameObject root, out Dictionary<int, Tile> instanceTiles);
            try
            {
                var connectionMetadata = root.AddComponent<PostLayoutConnectionMetadata>();
                connectionMetadata.Apply(resolution);
                Transform populationRoot = new GameObject("Population").transform;
                populationRoot.SetParent(root.transform, false);

                FacilityPopulationReport report = new FacilityPopulationPipeline(new FacilityPopulationSettings
                {
                    EnableVerbosePopulationLogs = false
                }).Populate(populationRoot, graph, layout, instanceTiles, connectionMetadata);

                for (int i = 0; i < report.MarkerUsage.Count; i++)
                {
                    PopulationMarkerUsage marker = report.MarkerUsage[i];
                    if (marker.Status == PopulationMarkerStatus.Used)
                    {
                        Assert.AreNotEqual(PopulationMarkerStatus.SkippedBlocked, marker.Status);
                        Assert.AreNotEqual(PopulationMarkerStatus.SkippedRule, marker.Status);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static TileCatalog LoadCatalog()
        {
            TileCatalog catalog = AssetDatabase.LoadAssetAtPath<TileCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, $"Missing test catalog at {CatalogPath}.");
            return catalog;
        }

        private static FacilityGraphPlanConfig BuildConfig(int seed, bool includePortal)
        {
            return new FacilityGraphPlanConfig
            {
                MasterSeed = seed,
                MainPathLengthRange = new IntRange(6, 7),
                BranchCountRange = new IntRange(2, 3),
                BranchLengthRange = new IntRange(1, 2),
                FireExitChance = 1f,
                FireExitCountRange = new IntRange(1, 1),
                AllowFireExitNearStart = false,
                MinimumMainPathDistanceForFireExit = 2,
                VerticalTransitionChance = 0f,
                LoopChance = 0.35f,
                PortalChance = includePortal ? 0.75f : 0f,
                MaxAttempts = 6
            };
        }

        private static bool EveryMainPathEdgeHasPhysicalConnection(FacilityGraph graph, ResolvedFacilityLayout layout)
        {
            var connectedEdges = new HashSet<int>();
            for (int i = 0; i < layout.Connections.Count; i++)
            {
                connectedEdges.Add(layout.Connections[i].EdgeId);
            }

            for (int i = 0; i < graph.Edges.Count; i++)
            {
                FacilityGraphEdge edge = graph.Edges[i];
                if (edge.Role != FacilityGraphEdgeRole.MainPath && edge.Role != FacilityGraphEdgeRole.Stair)
                {
                    continue;
                }

                if (!connectedEdges.Contains(edge.Id))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool FireExitsReachableViaLayout(FacilityGraph graph, ResolvedFacilityLayout layout)
        {
            if (graph.MainPathNodeIds.Count == 0)
            {
                return false;
            }

            int startNode = graph.MainPathNodeIds[0];
            var adjacency = new Dictionary<int, List<int>>();
            for (int i = 0; i < layout.Connections.Count; i++)
            {
                PlacedDoorwayConnection connection = layout.Connections[i];
                if (!adjacency.TryGetValue(connection.FromNodeId, out List<int> from))
                {
                    from = new List<int>();
                    adjacency[connection.FromNodeId] = from;
                }

                if (!adjacency.TryGetValue(connection.ToNodeId, out List<int> to))
                {
                    to = new List<int>();
                    adjacency[connection.ToNodeId] = to;
                }

                from.Add(connection.ToNodeId);
                to.Add(connection.FromNodeId);
            }

            var visited = new HashSet<int> { startNode };
            var queue = new Queue<int>();
            queue.Enqueue(startNode);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (!adjacency.TryGetValue(current, out List<int> neighbors))
                {
                    continue;
                }

                for (int i = 0; i < neighbors.Count; i++)
                {
                    if (visited.Add(neighbors[i]))
                    {
                        queue.Enqueue(neighbors[i]);
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

        private static PostLayoutConnectionResolution BuildResolution(
            ResolvedFacilityLayout layout,
            FacilityGraph graph,
            out GameObject root,
            out Dictionary<int, Tile> instanceTiles)
        {
            root = new GameObject("FacilityGeneratorInvariantTests_Root");
            var tilesRoot = new GameObject("Tiles");
            tilesRoot.transform.SetParent(root.transform, false);
            instanceTiles = new Dictionary<int, Tile>();
            for (int i = 0; i < layout.Tiles.Count; i++)
            {
                PlacedTile placed = layout.Tiles[i];
                GameObject instance = Object.Instantiate(placed.Definition.Prefab, tilesRoot.transform);
                instance.transform.SetPositionAndRotation(placed.Position, placed.Rotation);
                Tile tile = instance.GetComponent<Tile>();
                if (tile != null)
                {
                    instanceTiles[placed.NodeId] = tile;
                }
            }

            return new PostLayoutConnectionResolver().Resolve(layout, graph, instanceTiles);
        }

        private static int CountEdges(FacilityGraph graph, FacilityGraphEdgeRole role)
        {
            int count = 0;
            for (int i = 0; i < graph.Edges.Count; i++)
            {
                if (graph.Edges[i].Role == role)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
