using EscapeBlock9.ProcGen.Data;
using EscapeBlock9.ProcGen.Placement;
using EscapeBlock9.ProcGen.Planning;
using NUnit.Framework;
using UnityEditor;

namespace EscapeBlock9.ProcGen.Tests
{
    public sealed class PostLayoutConnectionResolverTests
    {
        private const string CatalogPath = "Assets/ProcGen/Catalogs/InitialBlock9TileCatalog.asset";

        [Test]
        public void ResolveMarksUsedDoorwaysAndBlocksUnusedDoorways()
        {
            TileCatalog catalog = LoadCatalog();
            FacilityGraph graph = BuildGraph(seed: 24680, includePortal: false);
            ResolvedFacilityLayout layout = new CustomFacilityLayoutSolver().Solve(graph, catalog, 24680);

            Assert.AreEqual(graph.Nodes.Count, layout.Tiles.Count, layout.Diagnostics.ToDebugString());
            PostLayoutConnectionResolution resolution = new PostLayoutConnectionResolver().Resolve(layout, graph);

            int expectedConnectedEndpoints = layout.Connections.Count * 2;
            int connected = 0;
            int blocked = 0;
            for (int i = 0; i < resolution.Doorways.Count; i++)
            {
                DoorwayResolutionKind kind = resolution.Doorways[i].ResolutionKind;
                if (kind == DoorwayResolutionKind.Blocked)
                {
                    blocked++;
                    continue;
                }

                if (kind == DoorwayResolutionKind.Connected || kind == DoorwayResolutionKind.FireExit)
                {
                    connected++;
                }
            }

            Assert.AreEqual(expectedConnectedEndpoints, connected, "Connected endpoint count should match solved doorway connections.");
            Assert.Greater(blocked, 0, "Expected at least one unused doorway to be blocked.");
        }

        [Test]
        public void ResolveRegistersFireExitMetadata()
        {
            TileCatalog catalog = LoadCatalog();
            var config = new FacilityGraphPlanConfig
            {
                MasterSeed = 9090,
                MainPathLengthRange = new IntRange(7, 7),
                BranchCountRange = new IntRange(1, 2),
                BranchLengthRange = new IntRange(1, 2),
                FireExitChance = 1f,
                FireExitCountRange = new IntRange(1, 1),
                AllowFireExitNearStart = false,
                MinimumMainPathDistanceForFireExit = 2,
                VerticalTransitionChance = 0f,
                LoopChance = 0f,
                PortalChance = 0f,
                MaxAttempts = 3,
            };

            FacilityGraph graph = new FacilityGraphPlanner().Plan(config);
            ResolvedFacilityLayout layout = new CustomFacilityLayoutSolver().Solve(graph, catalog, config.MasterSeed);
            Assert.AreEqual(graph.Nodes.Count, layout.Tiles.Count, layout.Diagnostics.ToDebugString());

            PostLayoutConnectionResolution resolution = new PostLayoutConnectionResolver().Resolve(layout, graph);
            Assert.Greater(resolution.FireExits.Count, 0, "Expected fire exit metadata entries.");
            for (int i = 0; i < resolution.FireExits.Count; i++)
            {
                Assert.GreaterOrEqual(resolution.FireExits[i].MainPathDistanceFromStart, 2);
                Assert.IsFalse(resolution.FireExits[i].SuppressedNearStart);
            }
        }

        [Test]
        public void PortalPairsAreMetadataOnlyByDefault()
        {
            TileCatalog catalog = LoadCatalog();
            FacilityGraph graph = BuildGraph(seed: 1357, includePortal: true);
            AssertHasEdge(graph, FacilityGraphEdgeRole.Portal);
            ResolvedFacilityLayout layout = new CustomFacilityLayoutSolver().Solve(graph, catalog, 1357);
            Assert.AreEqual(graph.Nodes.Count, layout.Tiles.Count, layout.Diagnostics.ToDebugString());

            PostLayoutConnectionResolution resolution = new PostLayoutConnectionResolver().Resolve(layout, graph);
            Assert.GreaterOrEqual(resolution.PortalPairs.Count, 1);

            for (int i = 0; i < resolution.PortalPairs.Count; i++)
            {
                Assert.IsFalse(resolution.PortalPairs[i].VisualsEnabled);
            }
        }

        private static TileCatalog LoadCatalog()
        {
            TileCatalog catalog = AssetDatabase.LoadAssetAtPath<TileCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, $"Missing test catalog at {CatalogPath}.");
            return catalog;
        }

        private static FacilityGraph BuildGraph(int seed, bool includePortal)
        {
            var config = new FacilityGraphPlanConfig
            {
                MasterSeed = seed,
                MainPathLengthRange = new IntRange(6, 6),
                BranchCountRange = new IntRange(2, 2),
                BranchLengthRange = new IntRange(1, 1),
                FireExitCountRange = new IntRange(1, 1),
                FireExitChance = 1f,
                AllowFireExitNearStart = false,
                MinimumMainPathDistanceForFireExit = 2,
                VerticalTransitionChance = 0f,
                LoopChance = 0f,
                PortalChance = includePortal ? 1f : 0f,
                MaxAttempts = 4,
            };

            return new FacilityGraphPlanner().Plan(config);
        }

        private static void AssertHasEdge(FacilityGraph graph, FacilityGraphEdgeRole role)
        {
            for (int i = 0; i < graph.Edges.Count; i++)
            {
                if (graph.Edges[i].Role == role)
                {
                    return;
                }
            }

            Assert.Fail($"Expected a graph edge with role {role}.");
        }
    }
}
