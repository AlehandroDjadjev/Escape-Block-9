using System.Collections.Generic;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Data
{
    [CreateAssetMenu(menuName = "Escape Block 9/ProcGen/Tile Catalog", fileName = "TileCatalog")]
    public sealed class TileCatalog : ScriptableObject
    {
        [SerializeField] private string catalogId;
        [SerializeField] private string version = "0.1";
        [SerializeField] private List<TileDefinition> definitions = new List<TileDefinition>();

        public string CatalogId => catalogId;
        public string Version => version;
        public IReadOnlyList<TileDefinition> Definitions => definitions;
    }
}
