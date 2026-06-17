using EscapeBlock9.ProcGen.Authoring;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Data
{
    [CreateAssetMenu(menuName = "Escape Block 9/ProcGen/Tile Definition", fileName = "TileDefinition")]
    public sealed class TileDefinition : ScriptableObject
    {
        [SerializeField] private string moduleId;
        [SerializeField] private string displayName;
        [SerializeField] private GameObject prefab;
        [SerializeField] private TileCategory category = TileCategory.Room;
        [SerializeField] private string[] tags;
        [SerializeField] private ConnectorKind defaultConnectorKind = ConnectorKind.Door;
        [SerializeField] private float selectionWeight = 1f;
        [SerializeField] private int maxUseCount = -1;
        [SerializeField] private bool unique;
        [SerializeField] private AllowedYawRotations allowedYawRotations = AllowedYawRotations.OnlyAuthored;

        public string ModuleId => moduleId;
        public string DisplayName => displayName;
        public GameObject Prefab => prefab;
        public TileCategory Category => category;
        public string[] Tags => tags;
        public ConnectorKind DefaultConnectorKind => defaultConnectorKind;
        public float SelectionWeight => selectionWeight;
        public int MaxUseCount => maxUseCount;
        public bool Unique => unique;
        public AllowedYawRotations AllowedYawRotations => allowedYawRotations;

        public Tile TilePrefab => prefab != null ? prefab.GetComponent<Tile>() : null;
    }
}
