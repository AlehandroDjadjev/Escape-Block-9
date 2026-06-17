using EscapeBlock9.ProcGen.Data;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Authoring
{
    [AddComponentMenu("Escape Block 9/ProcGen/Doorway")]
    public sealed class Doorway : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private string connectorId;

        [Header("Compatibility")]
        [SerializeField] private DoorwaySocket socket;
        [SerializeField] private string socketName;
        [SerializeField] private ConnectorKind connectorKind = ConnectorKind.Door;

        [Header("Visuals")]
        [SerializeField] private GameObject connectorPrefab;
        [SerializeField] private GameObject connectorObject;
        [SerializeField] private GameObject blockerPrefab;
        [SerializeField] private GameObject blockerObject;

        [Header("Dimensions")]
        [Min(0f)]
        [SerializeField] private float width = 1.2f;
        [Min(0f)]
        [SerializeField] private float height = 2.2f;
        [SerializeField] private int floorDelta;

        [Header("Gizmos")]
        [SerializeField] private Color gizmoColor = new Color(0.15f, 0.7f, 1f, 1f);
        [SerializeField] private float gizmoLength = 0.9f;

        public string ConnectorId => connectorId;
        public DoorwaySocket Socket => socket;
        public string SocketName => socket != null ? socket.SocketName : socketName;
        public ConnectorKind ConnectorKind => connectorKind;
        public GameObject ConnectorPrefab => connectorPrefab;
        public GameObject ConnectorObject => connectorObject;
        public GameObject BlockerPrefab => blockerPrefab;
        public GameObject BlockerObject => blockerObject;
        public float Width => width;
        public float Height => height;
        public int FloorDelta => floorDelta;

        public bool HasConnectorReference => connectorPrefab != null || connectorObject != null;
        public bool HasBlockerReference => blockerPrefab != null || blockerObject != null;

        private void OnValidate()
        {
            width = Mathf.Max(0f, width);
            height = Mathf.Max(0f, height);
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
            float length = selected ? gizmoLength * 1.35f : gizmoLength;
            Vector3 start = transform.position;
            Vector3 end = start + transform.forward * length;

            Gizmos.color = gizmoColor;
            Gizmos.DrawLine(start, end);
            Gizmos.DrawSphere(start, selected ? 0.08f : 0.05f);
            DrawArrowHead(end, transform.forward, selected ? 0.18f : 0.12f);

#if UNITY_EDITOR
            UnityEditor.Handles.color = gizmoColor;
            string label = $"{ConnectorIdOrName()} | {SocketName} | {connectorKind}";
            UnityEditor.Handles.Label(end + Vector3.up * 0.15f, label);
#endif
        }

        private void DrawArrowHead(Vector3 position, Vector3 direction, float size)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return;
            }

            Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            Vector3 left = rotation * Quaternion.Euler(0f, 150f, 0f) * Vector3.forward;
            Vector3 right = rotation * Quaternion.Euler(0f, -150f, 0f) * Vector3.forward;
            Gizmos.DrawLine(position, position + left * size);
            Gizmos.DrawLine(position, position + right * size);
        }

        private string ConnectorIdOrName()
        {
            return string.IsNullOrWhiteSpace(connectorId) ? name : connectorId.Trim();
        }
    }
}
