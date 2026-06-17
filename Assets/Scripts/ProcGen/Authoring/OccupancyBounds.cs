using UnityEngine;

namespace EscapeBlock9.ProcGen.Authoring
{
    [AddComponentMenu("Escape Block 9/ProcGen/Occupancy Bounds")]
    public sealed class OccupancyBounds : MonoBehaviour
    {
        [SerializeField] private Vector3 center;
        [SerializeField] private Vector3 size = Vector3.one;
        [SerializeField] private Color gizmoColor = new Color(1f, 0.75f, 0.15f, 0.25f);

        public Vector3 Center => center;
        public Vector3 Size => size;

        public Bounds LocalBounds => new Bounds(center, size);

        public Bounds WorldBounds
        {
            get
            {
                Vector3 worldCenter = transform.TransformPoint(center);
                Vector3 worldSize = Vector3.Scale(size, Abs(transform.lossyScale));
                return new Bounds(worldCenter, worldSize);
            }
        }

        private void OnValidate()
        {
            size = new Vector3(Mathf.Max(0f, size.x), Mathf.Max(0f, size.y), Mathf.Max(0f, size.z));
        }

        private void OnDrawGizmos()
        {
            DrawGizmos(false);
        }

        private void OnDrawGizmosSelected()
        {
            DrawGizmos(true);
        }

        private void DrawGizmos(bool selected)
        {
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Color fill = gizmoColor;
            fill.a = selected ? 0.32f : 0.16f;
            Gizmos.color = fill;
            Gizmos.DrawCube(center, size);

            Color wire = new Color(gizmoColor.r, gizmoColor.g, gizmoColor.b, 1f);
            Gizmos.color = wire;
            Gizmos.DrawWireCube(center, size);
            Gizmos.matrix = previous;

#if UNITY_EDITOR
            UnityEditor.Handles.color = wire;
            UnityEditor.Handles.Label(transform.TransformPoint(center + Vector3.up * (size.y * 0.5f + 0.15f)), "Occupancy");
#endif
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }
    }
}
