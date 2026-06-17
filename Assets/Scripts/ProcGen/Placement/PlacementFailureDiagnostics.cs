using System.Collections.Generic;
using System.Text;

namespace EscapeBlock9.ProcGen.Placement
{
    public enum PlacementFailureReason
    {
        NoCandidates,
        CategoryMismatch,
        SocketMismatch,
        ConnectorKindMismatch,
        MaxUseExceeded,
        ConnectorUnavailable,
        RotationNotAllowed,
        Overlap,
        MissingPrefab,
        MissingDoorways,
        BacktrackingLimitReached,
        OptionalEdgeUnresolved,
        RequiredEdgeUnresolved,
        LayoutDisconnected,
        DoorwayMisaligned
    }

    public readonly struct PlacementFailure
    {
        public PlacementFailure(PlacementFailureReason reason, int nodeId, string moduleId, string detail)
        {
            Reason = reason;
            NodeId = nodeId;
            ModuleId = moduleId;
            Detail = detail;
        }

        public PlacementFailureReason Reason { get; }
        public int NodeId { get; }
        public string ModuleId { get; }
        public string Detail { get; }
    }

    public sealed class PlacementFailureDiagnostics
    {
        private readonly List<PlacementFailure> failures = new List<PlacementFailure>();

        public IReadOnlyList<PlacementFailure> Failures => failures;

        public void Add(PlacementFailureReason reason, int nodeId, string moduleId, string detail)
        {
            failures.Add(new PlacementFailure(reason, nodeId, moduleId, detail));
        }

        public string ToDebugString()
        {
            var builder = new StringBuilder();
            builder.AppendLine($"Placement diagnostics: {failures.Count} entr{(failures.Count == 1 ? "y" : "ies")}");
            for (int i = 0; i < failures.Count; i++)
            {
                PlacementFailure failure = failures[i];
                builder.AppendLine($"  {failure.Reason}: node={failure.NodeId} module={failure.ModuleId ?? "<none>"} {failure.Detail}");
            }

            return builder.ToString();
        }
    }
}
