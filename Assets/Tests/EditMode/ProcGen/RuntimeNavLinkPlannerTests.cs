using System.Collections.Generic;
using EscapeBlock9.ProcGen.Authoring;
using EscapeBlock9.ProcGen.Data;
using EscapeBlock9.ProcGen.Navigation;
using EscapeBlock9.ProcGen.Placement;
using EscapeBlock9.ProcGen.Planning;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Tests
{
    public sealed class RuntimeNavLinkPlannerTests
    {
        private const string CatalogPath = "Assets/ProcGen/Catalogs/InitialBlock9TileCatalog.asset";

        [Test]
        public void PlannerBuildsStairLinksWhenVerticalEdgesExist()
        {
            TileCatalog catalog = LoadCatalog();
            var config = new FacilityGraphPlanConfig
            {
                MasterSeed = 11223,
                MainPathLengthRange = new IntRange(6, 6),
                BranchCountRange = new IntRange(1, 2),
                BranchLengthRange = new IntRange(1, 2),
                VerticalTransitionChance = 1f,
                FireExitChance = 0f,
                PortalChance = 0f,
                LoopChance = 0f,
                MaxAttempts = 4
            };

            FacilityGraph graph = new FacilityGraphPlanner().Plan(config);
            ResolvedFacilityLayout layout = new CustomFacilityLayoutSolver().Solve(graph, catalog, config.MasterSeed);
            Assert.AreEqual(graph.Nodes.Count, layout.Tiles.Count, layout.Diagnostics.ToDebugString());

            var root = new GameObject("RuntimeNavLinkPlannerTests_Root");
            try
            {
                var instanceTiles = new Dictionary<int, Tile>();
                for (int i = 0; i < layout.Tiles.Count; i++)
                {
                    PlacedTile placed = layout.Tiles[i];
                    GameObject instance = Object.Instantiate(placed.Definition.Prefab, root.transform);
                    instance.transform.SetPositionAndRotation(placed.Position, placed.Rotation);
                    Tile tile = instance.GetComponent<Tile>();
                    if (tile != null)
                    {
                        instanceTiles[placed.NodeId] = tile;
                    }
                }

                PostLayoutConnectionResolution resolution = new PostLayoutConnectionResolver().Resolve(layout, graph, instanceTiles);
                PostLayoutConnectionMetadata metadata = root.AddComponent<PostLayoutConnectionMetadata>();
                metadata.Apply(resolution);

                List<RuntimeNavLinkRequest> requests = RuntimeNavLinkPlanner.Build(graph, layout, instanceTiles, metadata, enablePortalLinks: false);
                Assert.Greater(requests.Count, 0, "Expected at least one navigation link for vertical transition.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PortalLinksOnlyAddedWhenEnabled()
        {
            TileCatalog catalog = LoadCatalog();
            var config = new FacilityGraphPlanConfig
            {
                MasterSeed = 44556,
                MainPathLengthRange = new IntRange(6, 6),
                BranchCountRange = new IntRange(2, 2),
                BranchLengthRange = new IntRange(1, 2),
                VerticalTransitionChance = 0f,
                FireExitChance = 0f,
                PortalChance = 1f,
                LoopChance = 0f,
                MaxAttempts = 6
            };

            FacilityGraph graph = new FacilityGraphPlanner().Plan(config);
            ResolvedFacilityLayout layout = new CustomFacilityLayoutSolver().Solve(graph, catalog, config.MasterSeed);
            Assert.AreEqual(graph.Nodes.Count, layout.Tiles.Count, layout.Diagnostics.ToDebugString());

            var root = new GameObject("RuntimeNavLinkPlannerTests_PortalRoot");
            try
            {
                var instanceTiles = new Dictionary<int, Tile>();
                for (int i = 0; i < layout.Tiles.Count; i++)
                {
                    PlacedTile placed = layout.Tiles[i];
                    GameObject instance = Object.Instantiate(placed.Definition.Prefab, root.transform);
                    instance.transform.SetPositionAndRotation(placed.Position, placed.Rotation);
                    Tile tile = instance.GetComponent<Tile>();
                    if (tile != null)
                    {
                        instanceTiles[placed.NodeId] = tile;
                    }
                }

                PostLayoutConnectionResolution resolution = new PostLayoutConnectionResolver().Resolve(layout, graph, instanceTiles);
                PostLayoutConnectionMetadata metadata = root.AddComponent<PostLayoutConnectionMetadata>();
                metadata.Apply(resolution);

                List<RuntimeNavLinkRequest> disabled = RuntimeNavLinkPlanner.Build(graph, layout, instanceTiles, metadata, enablePortalLinks: false);
                List<RuntimeNavLinkRequest> enabled = RuntimeNavLinkPlanner.Build(graph, layout, instanceTiles, metadata, enablePortalLinks: true);
                Assert.GreaterOrEqual(enabled.Count, disabled.Count, "Enabling portal links should not reduce total links.");
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
    }
}
