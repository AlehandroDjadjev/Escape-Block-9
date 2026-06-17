using EscapeBlock9.ProcGen.Data;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Authoring
{
    [AddComponentMenu("Escape Block 9/ProcGen/Tile")]
    [DisallowMultipleComponent]
    public sealed class Tile : MonoBehaviour
    {
        [Header("Identity")]
        [SerializeField] private TileDefinition definition;
        [SerializeField] private string moduleId;
        [SerializeField] private TileCategory category = TileCategory.Room;
        [SerializeField] private string[] tags;

        [Header("Selection")]
        [Min(0f)]
        [SerializeField] private float selectionWeight = 1f;
        [Tooltip("Use -1 for unlimited.")]
        [SerializeField] private int maxUseCount = -1;
        [SerializeField] private bool unique;
        [SerializeField] private AllowedYawRotations allowedYawRotations = AllowedYawRotations.OnlyAuthored;

        [Header("Authoring")]
        [SerializeField] private bool includeInactiveAuthoringChildren = true;

        public TileDefinition Definition => definition;
        public string ModuleId => !string.IsNullOrWhiteSpace(moduleId) ? moduleId.Trim() : definition != null ? definition.ModuleId : string.Empty;
        public TileCategory Category => definition != null && string.IsNullOrWhiteSpace(moduleId) ? definition.Category : category;
        public string[] Tags => tags;
        public float SelectionWeight => definition != null && Mathf.Approximately(selectionWeight, 1f) ? definition.SelectionWeight : selectionWeight;
        public int MaxUseCount => definition != null && maxUseCount == -1 ? definition.MaxUseCount : maxUseCount;
        public bool Unique => unique || (definition != null && definition.Unique);
        public AllowedYawRotations AllowedYawRotations => definition != null && allowedYawRotations == AllowedYawRotations.OnlyAuthored ? definition.AllowedYawRotations : allowedYawRotations;
        public bool IncludeInactiveAuthoringChildren => includeInactiveAuthoringChildren;

        public Doorway[] GetDoorways()
        {
            return GetComponentsInChildren<Doorway>(includeInactiveAuthoringChildren);
        }

        public OccupancyBounds[] GetOccupancyBounds()
        {
            return GetComponentsInChildren<OccupancyBounds>(includeInactiveAuthoringChildren);
        }

        public SpawnMarker[] GetSpawnMarkers()
        {
            return GetComponentsInChildren<SpawnMarker>(includeInactiveAuthoringChildren);
        }

        private void OnValidate()
        {
            selectionWeight = Mathf.Max(0f, selectionWeight);
            if (unique && maxUseCount != 1)
            {
                maxUseCount = 1;
            }
        }
    }
}
