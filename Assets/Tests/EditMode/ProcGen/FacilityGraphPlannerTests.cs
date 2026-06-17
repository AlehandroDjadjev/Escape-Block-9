using EscapeBlock9.ProcGen.Planning;
using NUnit.Framework;

namespace EscapeBlock9.ProcGen.Tests
{
    public sealed class FacilityGraphPlannerTests
    {
        [Test]
        public void SameSeedProducesSameGraph()
        {
            FacilityGraphPlanConfig config = RichConfig(2701);
            var planner = new FacilityGraphPlanner();

            string first = planner.Plan(config).ToDebugString();
            string second = planner.Plan(config).ToDebugString();

            Assert.AreEqual(first, second);
        }

        [Test]
        public void DifferentSeedsProduceDifferentGraphPlans()
        {
            var planner = new FacilityGraphPlanner();

            string first = planner.Plan(RichConfig(2701)).ToDebugString();
            string second = planner.Plan(RichConfig(2702)).ToDebugString();

            Assert.AreNotEqual(first, second);
        }

        [Test]
        public void MainPathAndBranchRolesAreExplicit()
        {
            FacilityGraphPlanConfig config = RichConfig(99);
            config.LoopChance = 0f;
            config.PortalChance = 0f;

            FacilityGraph graph = new FacilityGraphPlanner().Plan(config);

            Assert.GreaterOrEqual(graph.MainPathNodeIds.Count, config.MainPathLengthRange.Min);
            Assert.AreEqual(FacilityGraphNodeRole.Start, graph.GetNode(graph.MainPathNodeIds[0]).Role);
            Assert.Greater(graph.Branches.Count, 0);

            bool foundBranchEdge = false;
            bool foundDeadEndNode = false;
            for (int i = 0; i < graph.Edges.Count; i++)
            {
                foundBranchEdge |= graph.Edges[i].Role == FacilityGraphEdgeRole.Branch ||
                                   graph.Edges[i].Role == FacilityGraphEdgeRole.DeadEnd;
            }

            for (int i = 0; i < graph.Nodes.Count; i++)
            {
                foundDeadEndNode |= graph.Nodes[i].Role == FacilityGraphNodeRole.DeadEnd;
            }

            Assert.IsTrue(foundBranchEdge);
            Assert.IsTrue(foundDeadEndNode);
        }

        [Test]
        public void PortalEdgesAreDisabledByDefault()
        {
            FacilityGraph graph = new FacilityGraphPlanner().Plan(FacilityGraphPlanConfig.CreateDefault(12345));

            for (int i = 0; i < graph.Edges.Count; i++)
            {
                Assert.AreNotEqual(FacilityGraphEdgeRole.Portal, graph.Edges[i].Role);
            }
        }

        [Test]
        public void OptionalRolesCanBePlannedWithoutPhysicalPlacement()
        {
            FacilityGraphPlanConfig config = RichConfig(777);
            config.FireExitChance = 1f;
            config.FireExitCountRange = new IntRange(1, 1);
            config.VerticalTransitionChance = 1f;
            config.PortalChance = 1f;
            config.LoopChance = 1f;

            FacilityGraph graph = new FacilityGraphPlanner().Plan(config);

            AssertHasEdge(graph, FacilityGraphEdgeRole.FireExit);
            AssertHasEdge(graph, FacilityGraphEdgeRole.Stair);
            AssertHasEdge(graph, FacilityGraphEdgeRole.Portal);
            AssertHasEdge(graph, FacilityGraphEdgeRole.Loop);
        }

        [Test]
        public void FireExitRespectsMinimumDistanceFromStart()
        {
            var config = new FacilityGraphPlanConfig
            {
                MasterSeed = 4040,
                MainPathLengthRange = new IntRange(7, 7),
                BranchCountRange = new IntRange(1, 1),
                BranchLengthRange = new IntRange(1, 1),
                FireExitChance = 1f,
                FireExitCountRange = new IntRange(1, 1),
                AllowFireExitNearStart = false,
                MinimumMainPathDistanceForFireExit = 3,
                LoopChance = 0f,
                PortalChance = 0f,
                VerticalTransitionChance = 0f,
                MaxAttempts = 3,
            };

            FacilityGraph graph = new FacilityGraphPlanner().Plan(config);
            for (int i = 0; i < graph.Edges.Count; i++)
            {
                FacilityGraphEdge edge = graph.Edges[i];
                if (edge.Role != FacilityGraphEdgeRole.FireExit)
                {
                    continue;
                }

                int mainPathDistance = MainPathDistance(graph, edge.FromNodeId);
                Assert.GreaterOrEqual(mainPathDistance, 3, "Fire exit edge spawned too close to start.");
            }
        }

        private static FacilityGraphPlanConfig RichConfig(int seed)
        {
            return new FacilityGraphPlanConfig
            {
                MasterSeed = seed,
                MainPathLengthRange = new IntRange(6, 9),
                BranchCountRange = new IntRange(2, 4),
                BranchLengthRange = new IntRange(2, 4),
                LoopChance = 0.75f,
                FireExitCountRange = new IntRange(0, 2),
                FireExitChance = 0.75f,
                VerticalTransitionChance = 0.75f,
                PortalChance = 0f,
                MaxAttempts = 4,
            };
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

        private static int MainPathDistance(FacilityGraph graph, int nodeId)
        {
            for (int i = 0; i < graph.MainPathNodeIds.Count; i++)
            {
                if (graph.MainPathNodeIds[i] == nodeId)
                {
                    return i;
                }
            }

            return int.MaxValue;
        }
    }
}
