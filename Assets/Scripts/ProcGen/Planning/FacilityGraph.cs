using System;
using System.Collections.Generic;
using System.Text;

namespace EscapeBlock9.ProcGen.Planning
{
    public enum FacilityGraphNodeRole
    {
        Start,
        MainPath,
        Branch,
        DeadEnd,
        Stair,
        FireExit,
        Portal
    }

    public enum FacilityGraphEdgeRole
    {
        MainPath,
        Branch,
        DeadEnd,
        Loop,
        Stair,
        FireExit,
        Portal
    }

    public sealed class FacilityGraphNode
    {
        public FacilityGraphNode(int id, FacilityGraphNodeRole role, int mainPathIndex, int branchId, int depth, int floor)
        {
            Id = id;
            Role = role;
            MainPathIndex = mainPathIndex;
            BranchId = branchId;
            Depth = depth;
            Floor = floor;
        }

        public int Id { get; }
        public FacilityGraphNodeRole Role { get; internal set; }
        public int MainPathIndex { get; }
        public int BranchId { get; }
        public int Depth { get; }
        public int Floor { get; }
    }

    public sealed class FacilityGraphEdge
    {
        public FacilityGraphEdge(int id, int fromNodeId, int toNodeId, FacilityGraphEdgeRole role, bool bidirectional = true)
        {
            Id = id;
            FromNodeId = fromNodeId;
            ToNodeId = toNodeId;
            Role = role;
            Bidirectional = bidirectional;
        }

        public int Id { get; }
        public int FromNodeId { get; }
        public int ToNodeId { get; }
        public FacilityGraphEdgeRole Role { get; }
        public bool Bidirectional { get; }
    }

    public sealed class FacilityGraphBranch
    {
        public FacilityGraphBranch(int id, int rootNodeId, IReadOnlyList<int> nodeIds, FacilityGraphEdgeRole edgeRole)
        {
            Id = id;
            RootNodeId = rootNodeId;
            NodeIds = nodeIds;
            EdgeRole = edgeRole;
        }

        public int Id { get; }
        public int RootNodeId { get; }
        public IReadOnlyList<int> NodeIds { get; }
        public FacilityGraphEdgeRole EdgeRole { get; }
    }

    public sealed class FacilityGraph
    {
        private readonly List<FacilityGraphNode> nodes = new List<FacilityGraphNode>();
        private readonly List<FacilityGraphEdge> edges = new List<FacilityGraphEdge>();
        private readonly List<int> mainPathNodeIds = new List<int>();
        private readonly List<FacilityGraphBranch> branches = new List<FacilityGraphBranch>();

        public FacilityGraph(int seed, int attempt)
        {
            Seed = seed;
            Attempt = attempt;
        }

        public int Seed { get; }
        public int Attempt { get; }
        public IReadOnlyList<FacilityGraphNode> Nodes => nodes;
        public IReadOnlyList<FacilityGraphEdge> Edges => edges;
        public IReadOnlyList<int> MainPathNodeIds => mainPathNodeIds;
        public IReadOnlyList<FacilityGraphBranch> Branches => branches;

        public FacilityGraphNode AddNode(FacilityGraphNodeRole role, int mainPathIndex = -1, int branchId = -1, int depth = 0, int floor = 0)
        {
            var node = new FacilityGraphNode(nodes.Count, role, mainPathIndex, branchId, depth, floor);
            nodes.Add(node);
            if (mainPathIndex >= 0)
            {
                mainPathNodeIds.Add(node.Id);
            }

            return node;
        }

        public FacilityGraphEdge AddEdge(int fromNodeId, int toNodeId, FacilityGraphEdgeRole role, bool bidirectional = true)
        {
            if (fromNodeId == toNodeId)
            {
                throw new ArgumentException("Graph edges must connect two distinct nodes.");
            }

            var edge = new FacilityGraphEdge(edges.Count, fromNodeId, toNodeId, role, bidirectional);
            edges.Add(edge);
            return edge;
        }

        public void AddBranch(FacilityGraphBranch branch)
        {
            branches.Add(branch);
        }

        public FacilityGraphNode GetNode(int nodeId)
        {
            return nodes[nodeId];
        }

        public bool HasEdgeBetween(int firstNodeId, int secondNodeId)
        {
            for (int i = 0; i < edges.Count; i++)
            {
                FacilityGraphEdge edge = edges[i];
                if ((edge.FromNodeId == firstNodeId && edge.ToNodeId == secondNodeId) ||
                    (edge.FromNodeId == secondNodeId && edge.ToNodeId == firstNodeId))
                {
                    return true;
                }
            }

            return false;
        }

        public string ToDebugString()
        {
            var builder = new StringBuilder();
            builder.AppendLine($"FacilityGraph seed={Seed} attempt={Attempt}");
            builder.AppendLine("Nodes:");
            for (int i = 0; i < nodes.Count; i++)
            {
                FacilityGraphNode node = nodes[i];
                builder.AppendLine($"  {node.Id}: role={node.Role} main={node.MainPathIndex} branch={node.BranchId} depth={node.Depth} floor={node.Floor}");
            }

            builder.AppendLine("Edges:");
            for (int i = 0; i < edges.Count; i++)
            {
                FacilityGraphEdge edge = edges[i];
                builder.AppendLine($"  {edge.Id}: {edge.FromNodeId}->{edge.ToNodeId} role={edge.Role} bidirectional={edge.Bidirectional}");
            }

            return builder.ToString();
        }
    }
}
