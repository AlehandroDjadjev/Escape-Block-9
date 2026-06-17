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
    public sealed class FacilityPopulationPipelineTests
    {
        private const string CatalogPath = "Assets/ProcGen/Catalogs/InitialBlock9TileCatalog.asset";
        private const string LootPrefabPath = "Assets/arhitektura/KeyItem.prefab";
        private const string EnemyPrefabPath = "Assets/enteties/StickNPC_01.prefab";

        [Test]
        public void SameSeedProducesSamePopulationReport()
        {
            TileCatalog catalog = LoadCatalog();
            FacilityGraph graph = BuildGraph(64123);
            ResolvedFacilityLayout layout = new CustomFacilityLayoutSolver().Solve(graph, catalog, 64123);
            Assert.AreEqual(graph.Nodes.Count, layout.Tiles.Count, layout.Diagnostics.ToDebugString());

            string first = RunPopulation(layout, graph).ToDebugString();
            string second = RunPopulation(layout, graph).ToDebugString();
            Assert.AreEqual(first, second);
        }

        [Test]
        public void PopulationMarksMarkerUsageAndSpawnsContent()
        {
            TileCatalog catalog = LoadCatalog();
            FacilityGraph graph = BuildGraph(91234);
            ResolvedFacilityLayout layout = new CustomFacilityLayoutSolver().Solve(graph, catalog, 91234);
            Assert.AreEqual(graph.Nodes.Count, layout.Tiles.Count, layout.Diagnostics.ToDebugString());

            FacilityPopulationReport report = RunPopulation(layout, graph);
            int used = 0;
            int skippedBlocked = 0;
            for (int i = 0; i < report.MarkerUsage.Count; i++)
            {
                if (report.MarkerUsage[i].Status == PopulationMarkerStatus.Used)
                {
                    used++;
                }

                if (report.MarkerUsage[i].Status == PopulationMarkerStatus.SkippedBlocked)
                {
                    skippedBlocked++;
                }
            }

            Assert.Greater(report.MarkerUsage.Count, 0);
            Assert.Greater(used, 0, "Expected some markers to be used by population.");
            Assert.Greater(report.Spawns.Count, 0, "Expected deterministic population content spawns.");
            Assert.GreaterOrEqual(skippedBlocked, 0);
        }

        [Test]
        public void PopulationReportsClearErrorsWhenPlayerMissing()
        {
            TileCatalog catalog = LoadCatalog();
            FacilityGraph graph = BuildGraph(77889);
            ResolvedFacilityLayout layout = new CustomFacilityLayoutSolver().Solve(graph, catalog, 77889);
            Assert.AreEqual(graph.Nodes.Count, layout.Tiles.Count, layout.Diagnostics.ToDebugString());

            FacilityPopulationReport report = RunPopulation(layout, graph);
            bool foundPlayerError = false;
            for (int i = 0; i < report.Errors.Count; i++)
            {
                if (report.Errors[i].IndexOf("Player", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    foundPlayerError = true;
                    break;
                }
            }

            Assert.IsTrue(foundPlayerError, "Expected a clear population error mentioning missing player target.");
        }

        private static FacilityPopulationReport RunPopulation(ResolvedFacilityLayout layout, FacilityGraph graph)
        {
            var root = new GameObject("PopulationPipelineTests_Root");
            var tilesRoot = new GameObject("Tiles");
            tilesRoot.transform.SetParent(root.transform, false);
            var instanceTiles = new Dictionary<int, Tile>();

            try
            {
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

                PostLayoutConnectionResolution resolution = new PostLayoutConnectionResolver().Resolve(layout, graph, instanceTiles);
                PostLayoutConnectionMetadata connectionMetadata = root.AddComponent<PostLayoutConnectionMetadata>();
                connectionMetadata.Apply(resolution);

                Transform populationRoot = new GameObject("Population").transform;
                populationRoot.SetParent(root.transform, false);

                var settings = new FacilityPopulationSettings
                {
                    LootPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LootPrefabPath),
                    ObjectivePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(LootPrefabPath),
                    EnemyPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyPrefabPath),
                    EnableVerbosePopulationLogs = false
                };

                return new FacilityPopulationPipeline(settings).Populate(populationRoot, graph, layout, instanceTiles, connectionMetadata);
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

        private static FacilityGraph BuildGraph(int seed)
        {
            var config = new FacilityGraphPlanConfig
            {
                MasterSeed = seed,
                MainPathLengthRange = new IntRange(6, 6),
                BranchCountRange = new IntRange(2, 3),
                BranchLengthRange = new IntRange(1, 2),
                FireExitChance = 1f,
                FireExitCountRange = new IntRange(1, 1),
                AllowFireExitNearStart = false,
                MinimumMainPathDistanceForFireExit = 2,
                VerticalTransitionChance = 0f,
                LoopChance = 0f,
                PortalChance = 0f,
                MaxAttempts = 4
            };

            return new FacilityGraphPlanner().Plan(config);
        }
    }
}
