using System.Collections.Generic;
using EscapeBlock9.ProcGen.Authoring;
using EscapeBlock9.ProcGen.Data;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Validation
{
    public static class TileAuthoringValidator
    {
        public static List<TileAuthoringIssue> Validate(Tile tile)
        {
            var issues = new List<TileAuthoringIssue>();
            Validate(tile, issues);
            return issues;
        }

        public static void Validate(Tile tile, List<TileAuthoringIssue> issues)
        {
            if (issues == null)
            {
                return;
            }

            if (tile == null)
            {
                issues.Add(new TileAuthoringIssue(TileAuthoringSeverity.Error, null, "Tile is null."));
                return;
            }

            if (string.IsNullOrWhiteSpace(tile.ModuleId))
            {
                issues.Add(new TileAuthoringIssue(TileAuthoringSeverity.Error, tile, $"Tile '{tile.name}' is missing a module ID."));
            }

            if (tile.SelectionWeight <= 0f)
            {
                issues.Add(new TileAuthoringIssue(TileAuthoringSeverity.Warning, tile, $"Tile '{DisplayId(tile)}' has a non-positive selection weight."));
            }

            if (tile.Unique && tile.MaxUseCount != 1)
            {
                issues.Add(new TileAuthoringIssue(TileAuthoringSeverity.Warning, tile, $"Tile '{DisplayId(tile)}' is unique but max use count is not 1."));
            }

            Doorway[] doorways = tile.GetDoorways();
            if (doorways == null || doorways.Length == 0)
            {
                issues.Add(new TileAuthoringIssue(TileAuthoringSeverity.Error, tile, $"Tile '{DisplayId(tile)}' has no connectors/doorways."));
            }
            else
            {
                ValidateDoorways(tile, doorways, issues);
            }

            OccupancyBounds[] occupancyBounds = tile.GetOccupancyBounds();
            if (occupancyBounds == null || occupancyBounds.Length == 0)
            {
                issues.Add(new TileAuthoringIssue(TileAuthoringSeverity.Error, tile, $"Tile '{DisplayId(tile)}' has no occupancy volumes."));
            }
            else
            {
                for (int i = 0; i < occupancyBounds.Length; i++)
                {
                    OccupancyBounds bounds = occupancyBounds[i];
                    if (bounds == null)
                    {
                        continue;
                    }

                    Vector3 size = bounds.Size;
                    if (size.x <= 0f || size.y <= 0f || size.z <= 0f)
                    {
                        issues.Add(new TileAuthoringIssue(TileAuthoringSeverity.Error, bounds, $"Occupancy bounds '{bounds.name}' on tile '{DisplayId(tile)}' has zero or negative size."));
                    }
                }
            }
        }

        private static void ValidateDoorways(Tile tile, Doorway[] doorways, List<TileAuthoringIssue> issues)
        {
            var connectorIds = new HashSet<string>();
            var duplicateIds = new HashSet<string>();

            for (int i = 0; i < doorways.Length; i++)
            {
                Doorway doorway = doorways[i];
                if (doorway == null)
                {
                    continue;
                }

                string connectorId = string.IsNullOrWhiteSpace(doorway.ConnectorId) ? doorway.name : doorway.ConnectorId.Trim();
                if (!connectorIds.Add(connectorId))
                {
                    duplicateIds.Add(connectorId);
                }

                if (RequiresSocket(doorway.ConnectorKind) && doorway.Socket == null && string.IsNullOrWhiteSpace(doorway.SocketName))
                {
                    issues.Add(new TileAuthoringIssue(TileAuthoringSeverity.Error, doorway, $"Connector '{connectorId}' on tile '{DisplayId(tile)}' is missing a socket asset or socket name."));
                }

                if (doorway.Width <= 0f || doorway.Height <= 0f)
                {
                    issues.Add(new TileAuthoringIssue(TileAuthoringSeverity.Error, doorway, $"Connector '{connectorId}' on tile '{DisplayId(tile)}' has zero or negative dimensions."));
                }

                ValidateConnectorReferences(tile, doorway, connectorId, issues);
                ValidateConnectorKind(tile, doorway, connectorId, issues);
            }

            foreach (string duplicateId in duplicateIds)
            {
                issues.Add(new TileAuthoringIssue(TileAuthoringSeverity.Error, tile, $"Tile '{DisplayId(tile)}' has duplicate connector ID '{duplicateId}'."));
            }
        }

        private static void ValidateConnectorReferences(Tile tile, Doorway doorway, string connectorId, List<TileAuthoringIssue> issues)
        {
            switch (doorway.ConnectorKind)
            {
                case ConnectorKind.Door:
                case ConnectorKind.OpenFrame:
                case ConnectorKind.CorridorJoin:
                case ConnectorKind.Stair:
                case ConnectorKind.FireExit:
                case ConnectorKind.Portal:
                    if (!doorway.HasConnectorReference)
                    {
                        issues.Add(new TileAuthoringIssue(TileAuthoringSeverity.Warning, doorway, $"Connector '{connectorId}' on tile '{DisplayId(tile)}' has kind '{doorway.ConnectorKind}' but no connector prefab/object reference."));
                    }

                    if (!doorway.HasBlockerReference)
                    {
                        issues.Add(new TileAuthoringIssue(TileAuthoringSeverity.Warning, doorway, $"Connector '{connectorId}' on tile '{DisplayId(tile)}' has no blocker prefab/object for unused doorway sealing."));
                    }
                    break;
                case ConnectorKind.Sealed:
                    if (doorway.HasConnectorReference)
                    {
                        issues.Add(new TileAuthoringIssue(TileAuthoringSeverity.Error, doorway, $"Connector '{connectorId}' on tile '{DisplayId(tile)}' is sealed but has a connector reference."));
                    }

                    if (!doorway.HasBlockerReference)
                    {
                        issues.Add(new TileAuthoringIssue(TileAuthoringSeverity.Warning, doorway, $"Connector '{connectorId}' on tile '{DisplayId(tile)}' is sealed but has no blocker reference."));
                    }
                    break;
                case ConnectorKind.None:
                    if (doorway.HasConnectorReference || doorway.HasBlockerReference)
                    {
                        issues.Add(new TileAuthoringIssue(TileAuthoringSeverity.Warning, doorway, $"Connector '{connectorId}' on tile '{DisplayId(tile)}' has kind None but still references connector/blocker objects."));
                    }
                    break;
            }
        }

        private static void ValidateConnectorKind(Tile tile, Doorway doorway, string connectorId, List<TileAuthoringIssue> issues)
        {
            if (doorway.FloorDelta != 0 && doorway.ConnectorKind != ConnectorKind.Stair && doorway.ConnectorKind != ConnectorKind.Portal)
            {
                issues.Add(new TileAuthoringIssue(TileAuthoringSeverity.Error, doorway, $"Connector '{connectorId}' on tile '{DisplayId(tile)}' changes floors but is not a Stair or Portal connector."));
            }

            if (doorway.ConnectorKind == ConnectorKind.Stair && doorway.FloorDelta == 0)
            {
                issues.Add(new TileAuthoringIssue(TileAuthoringSeverity.Warning, doorway, $"Stair connector '{connectorId}' on tile '{DisplayId(tile)}' has floor delta 0."));
            }

            if (doorway.ConnectorKind == ConnectorKind.FireExit && tile.Category != TileCategory.Exit && tile.Category != TileCategory.Special)
            {
                issues.Add(new TileAuthoringIssue(TileAuthoringSeverity.Warning, doorway, $"Fire exit connector '{connectorId}' is on a '{tile.Category}' tile rather than an Exit or Special tile."));
            }
        }

        private static bool RequiresSocket(ConnectorKind connectorKind)
        {
            return connectorKind != ConnectorKind.None && connectorKind != ConnectorKind.Sealed;
        }

        private static string DisplayId(Tile tile)
        {
            if (tile == null)
            {
                return "<null>";
            }

            return string.IsNullOrWhiteSpace(tile.ModuleId) ? tile.name : tile.ModuleId;
        }
    }
}
