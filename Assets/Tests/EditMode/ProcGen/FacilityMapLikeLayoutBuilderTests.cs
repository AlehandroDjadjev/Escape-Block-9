using EscapeBlock9.ProcGen.Data;
using EscapeBlock9.ProcGen.Placement;
using NUnit.Framework;
using UnityEditor;

namespace EscapeBlock9.ProcGen.Tests
{
    public sealed class FacilityMapLikeLayoutBuilderTests
    {
        private const string CatalogPath = "Assets/ProcGen/Catalogs/InitialBlock9TileCatalog.asset";

        [Test]
        public void TwelveRoomPreviewStyleLayoutBuildsWithoutOverlap()
        {
            TileCatalog catalog = AssetDatabase.LoadAssetAtPath<TileCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, $"Missing test catalog at {CatalogPath}.");

            const int seed = 657612182;
            bool success = FacilityMapLikeLayoutBuilder.TryBuild(catalog, 12, seed, out var graph, out var layout, out string diagnostics);

            Assert.IsTrue(success, diagnostics);
            Assert.AreEqual(graph.Nodes.Count, layout.Tiles.Count, diagnostics);
            Assert.IsTrue(ResolvedFacilityLayoutValidator.ValidateConnected(graph, layout, layout.Diagnostics), layout.Diagnostics.ToDebugString());
            Assert.IsFalse(
                OccupancyValidator.AnyOverlap(layout.Tiles, new FacilityPlacementSettings().OverlapTolerance, out string overlap),
                overlap);

            int fireExitCount = 0;
            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                if (graph.Nodes[i].Role == EscapeBlock9.ProcGen.Planning.FacilityGraphNodeRole.FireExit)
                {
                    fireExitCount++;
                }
            }

            Assert.AreEqual(2, fireExitCount, "Preview-style runtime layout should always include two fire exit rooms.");
        }
    }
}
