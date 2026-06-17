using EscapeBlock9.ProcGen.Authoring;
using EscapeBlock9.ProcGen.Data;
using EscapeBlock9.ProcGen.Placement;
using EscapeBlock9.ProcGen.Planning;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Tests
{
    public sealed class CustomFacilityLayoutSolverTests
    {
        private const string CatalogPath = "Assets/ProcGen/Catalogs/InitialBlock9TileCatalog.asset";

        [Test]
        public void SameSeedProducesSamePhysicalLayout()
        {
            TileCatalog catalog = LoadCatalog();
            FacilityGraph graph = BuildGraph(13579);
            var solver = new CustomFacilityLayoutSolver();

            string first = solver.Solve(graph, catalog, 13579).ToDebugString();
            string second = solver.Solve(graph, catalog, 13579).ToDebugString();

            Assert.AreEqual(first, second);
        }

        [Test]
        public void PhysicalPlacementHasNoOccupancyOverlap()
        {
            TileCatalog catalog = LoadCatalog();
            FacilityGraph graph = BuildGraph(13579);
            ResolvedFacilityLayout layout = new CustomFacilityLayoutSolver().Solve(graph, catalog, 13579);

            Assert.AreEqual(graph.Nodes.Count, layout.Tiles.Count, layout.Diagnostics.ToDebugString());
            Assert.IsFalse(OccupancyValidator.AnyOverlap(layout.Tiles, 0.45f, out string overlap), overlap);
        }

        [Test]
        public void ConnectedDoorwaysAreAligned()
        {
            TileCatalog catalog = LoadCatalog();
            FacilityGraph graph = BuildGraph(13579);
            ResolvedFacilityLayout layout = new CustomFacilityLayoutSolver().Solve(graph, catalog, 13579);

            Assert.AreEqual(graph.Nodes.Count, layout.Tiles.Count, layout.Diagnostics.ToDebugString());
            for (int i = 0; i < layout.Connections.Count; i++)
            {
                PlacedDoorwayConnection connection = layout.Connections[i];
                PlacedTile fromTile = layout.GetTile(connection.FromNodeId);
                PlacedTile toTile = layout.GetTile(connection.ToNodeId);
                Doorway fromDoorway = fromTile.Definition.Prefab.GetComponent<Tile>().GetDoorways()[connection.FromDoorwayIndex];
                Doorway toDoorway = toTile.Definition.Prefab.GetComponent<Tile>().GetDoorways()[connection.ToDoorwayIndex];

                Vector3 fromPosition = fromTile.DoorwayPosition(fromDoorway);
                Vector3 toPosition = toTile.DoorwayPosition(toDoorway);
                Vector3 fromForward = fromTile.DoorwayForward(fromDoorway);
                Vector3 toForward = toTile.DoorwayForward(toDoorway);

                Assert.LessOrEqual(Vector3.Distance(fromPosition, toPosition), 0.05f, $"Connection {connection.EdgeId} positions differ.");
                Assert.LessOrEqual(Vector3.Dot(fromForward, toForward), -0.99f, $"Connection {connection.EdgeId} forwards are not opposed.");
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
                MainPathLengthRange = new IntRange(4, 4),
                BranchCountRange = new IntRange(2, 2),
                BranchLengthRange = new IntRange(1, 1),
                LoopChance = 0f,
                FireExitCountRange = new IntRange(1, 1),
                FireExitChance = 1f,
                VerticalTransitionChance = 0f,
                PortalChance = 0f,
                MaxAttempts = 4,
            };

            return new FacilityGraphPlanner().Plan(config);
        }
    }
}
