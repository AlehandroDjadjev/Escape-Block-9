using System.Collections.Generic;
using EscapeBlock9.ProcGen.Authoring;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Placement
{
    public static class OccupancyValidator
    {
        public static List<PlacedOccupancyBox> BuildBoxes(Tile tile, Vector3 rootPosition, Quaternion rootRotation, float padding)
        {
            var boxes = new List<PlacedOccupancyBox>();
            OccupancyBounds[] authoringBounds = tile.GetOccupancyBounds();
            Matrix4x4 root = Matrix4x4.TRS(rootPosition, rootRotation, Vector3.one);
            for (int i = 0; i < authoringBounds.Length; i++)
            {
                OccupancyBounds occupancy = authoringBounds[i];
                if (occupancy == null)
                {
                    continue;
                }

                Matrix4x4 localToRoot = occupancy.transform.localToWorldMatrix;
                Bounds localBounds = occupancy.LocalBounds;
                Bounds bounds = TransformBounds(root * localToRoot, localBounds);
                bounds.Expand(padding * 2f);
                boxes.Add(new PlacedOccupancyBox(bounds));
            }

            return boxes;
        }

        public static bool WouldOverlap(IReadOnlyList<PlacedOccupancyBox> candidateBoxes, IReadOnlyList<PlacedTile> placedTiles, float tolerance, out string detail)
        {
            for (int i = 0; i < candidateBoxes.Count; i++)
            {
                Bounds candidate = candidateBoxes[i].Bounds;
                for (int placedIndex = 0; placedIndex < placedTiles.Count; placedIndex++)
                {
                    PlacedTile placed = placedTiles[placedIndex];
                    for (int j = 0; j < placed.OccupancyBoxes.Count; j++)
                    {
                        Bounds existing = placed.OccupancyBoxes[j].Bounds;
                        if (Overlaps(candidate, existing, tolerance))
                        {
                            detail = $"candidate bounds {Format(candidate)} overlaps node {placed.NodeId} module {placed.ModuleId} bounds {Format(existing)}";
                            return true;
                        }
                    }
                }
            }

            detail = string.Empty;
            return false;
        }

        public static bool AnyOverlap(IReadOnlyList<PlacedTile> placedTiles, float tolerance, out string detail)
        {
            for (int i = 0; i < placedTiles.Count; i++)
            {
                for (int j = i + 1; j < placedTiles.Count; j++)
                {
                    for (int a = 0; a < placedTiles[i].OccupancyBoxes.Count; a++)
                    {
                        for (int b = 0; b < placedTiles[j].OccupancyBoxes.Count; b++)
                        {
                            if (Overlaps(placedTiles[i].OccupancyBoxes[a].Bounds, placedTiles[j].OccupancyBoxes[b].Bounds, tolerance))
                            {
                                detail = $"node {placedTiles[i].NodeId} overlaps node {placedTiles[j].NodeId}";
                                return true;
                            }
                        }
                    }
                }
            }

            detail = string.Empty;
            return false;
        }

        private static Bounds TransformBounds(Matrix4x4 matrix, Bounds bounds)
        {
            Vector3 center = matrix.MultiplyPoint3x4(bounds.center);
            Vector3 extents = bounds.extents;
            Vector3 axisX = matrix.MultiplyVector(new Vector3(extents.x, 0f, 0f));
            Vector3 axisY = matrix.MultiplyVector(new Vector3(0f, extents.y, 0f));
            Vector3 axisZ = matrix.MultiplyVector(new Vector3(0f, 0f, extents.z));
            extents = new Vector3(
                Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x),
                Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y),
                Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z));
            return new Bounds(center, extents * 2f);
        }

        private static bool Overlaps(Bounds first, Bounds second, float tolerance)
        {
            if (first.max.x <= second.min.x + tolerance || first.min.x >= second.max.x - tolerance)
            {
                return false;
            }

            if (first.max.y <= second.min.y + tolerance || first.min.y >= second.max.y - tolerance)
            {
                return false;
            }

            if (first.max.z <= second.min.z + tolerance || first.min.z >= second.max.z - tolerance)
            {
                return false;
            }

            return true;
        }

        private static string Format(Bounds bounds)
        {
            return $"center=({bounds.center.x:0.##},{bounds.center.y:0.##},{bounds.center.z:0.##}) size=({bounds.size.x:0.##},{bounds.size.y:0.##},{bounds.size.z:0.##})";
        }
    }
}
