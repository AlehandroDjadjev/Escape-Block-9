using EscapeBlock9.ProcGen.Data;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Authoring
{
    [AddComponentMenu("Escape Block 9/ProcGen/Spawn Marker")]
    public sealed class SpawnMarker : MonoBehaviour
    {
        [SerializeField] private string markerId;
        [SerializeField] private SpawnMarkerKind kind = SpawnMarkerKind.Debug;
        [SerializeField] private string[] tags;
        [Min(0f)]
        [SerializeField] private float weight = 1f;
        [SerializeField] private bool requireCriticalPath;
        [SerializeField] private bool requireReachableFromStart = true;
        [SerializeField] private Color gizmoColor = new Color(0.35f, 1f, 0.45f, 1f);
        [SerializeField] private float gizmoRadius = 0.16f;

        public string MarkerId => markerId;
        public SpawnMarkerKind Kind => kind;
        public string[] Tags => tags;
        public float Weight => weight;
        public bool RequireCriticalPath => requireCriticalPath;
        public bool RequireReachableFromStart => requireReachableFromStart;

        private void OnValidate()
        {
            weight = Mathf.Max(0f, weight);
            gizmoRadius = Mathf.Max(0.01f, gizmoRadius);
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
            Gizmos.color = gizmoColor;
            float radius = selected ? gizmoRadius * 1.35f : gizmoRadius;
            Gizmos.DrawSphere(transform.position, radius);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * 0.45f);

#if UNITY_EDITOR
            UnityEditor.Handles.color = gizmoColor;
            string labelId = string.IsNullOrWhiteSpace(markerId) ? name : markerId.Trim();
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.25f, $"{kind}: {labelId}");
#endif
        }
    }
}
