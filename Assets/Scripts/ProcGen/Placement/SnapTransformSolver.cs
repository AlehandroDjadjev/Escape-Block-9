using System;
using EscapeBlock9.ProcGen.Authoring;
using EscapeBlock9.ProcGen.Data;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Placement
{
    public readonly struct SnapTransformResult
    {
        public SnapTransformResult(Vector3 position, Quaternion rotation, float yawDegrees)
        {
            Position = position;
            Rotation = rotation;
            YawDegrees = yawDegrees;
        }

        public Vector3 Position { get; }
        public Quaternion Rotation { get; }
        public float YawDegrees { get; }
    }

    public static class SnapTransformSolver
    {
        public static SnapTransformResult Solve(Doorway openDoorway, PlacedTile openTile, Doorway candidateDoorway)
        {
            Vector3 targetPosition = openTile.DoorwayPosition(openDoorway);
            Vector3 targetForward = -Flatten(openTile.DoorwayForward(openDoorway));
            Vector3 candidateForward = Flatten(candidateDoorway.transform.localRotation * Vector3.forward);

            float targetYaw = Yaw(targetForward);
            float candidateYaw = Yaw(candidateForward);
            float rootYaw = NormalizeYaw(targetYaw - candidateYaw);
            Quaternion rootRotation = Quaternion.Euler(0f, rootYaw, 0f);
            Vector3 rootPosition = targetPosition - rootRotation * candidateDoorway.transform.localPosition;

            return new SnapTransformResult(rootPosition, rootRotation, rootYaw);
        }

        public static bool IsYawAllowed(float yawDegrees, AllowedYawRotations allowedRotations, float toleranceDegrees = 0.5f)
        {
            float normalized = NormalizeYaw(yawDegrees);
            int snapped = Mathf.RoundToInt(normalized / 90f) * 90;
            snapped = Mathf.RoundToInt(NormalizeYaw(snapped));

            if (Mathf.Abs(Mathf.DeltaAngle(normalized, snapped)) > toleranceDegrees)
            {
                return false;
            }

            if (allowedRotations == AllowedYawRotations.OnlyAuthored)
            {
                return snapped == 0 || snapped == 360;
            }

            if (snapped == 0 || snapped == 360)
            {
                return true;
            }

            if (snapped == 90)
            {
                return (allowedRotations & AllowedYawRotations.Yaw90) != 0;
            }

            if (snapped == 180)
            {
                return (allowedRotations & AllowedYawRotations.Yaw180) != 0;
            }

            if (snapped == 270)
            {
                return (allowedRotations & AllowedYawRotations.Yaw270) != 0;
            }

            return false;
        }

        private static Vector3 Flatten(Vector3 value)
        {
            value.y = 0f;
            if (value.sqrMagnitude <= 0.0001f)
            {
                return Vector3.forward;
            }

            return value.normalized;
        }

        private static float Yaw(Vector3 forward)
        {
            return NormalizeYaw(Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg);
        }

        private static float NormalizeYaw(float yaw)
        {
            yaw %= 360f;
            if (yaw < 0f)
            {
                yaw += 360f;
            }

            return yaw;
        }
    }
}
