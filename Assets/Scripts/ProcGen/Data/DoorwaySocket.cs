using UnityEngine;

namespace EscapeBlock9.ProcGen.Data
{
    [CreateAssetMenu(menuName = "Escape Block 9/ProcGen/Doorway Socket", fileName = "DoorwaySocket")]
    public sealed class DoorwaySocket : ScriptableObject
    {
        [SerializeField] private string socketName = "Default";
        [SerializeField] private string[] compatibleSocketNames = { "Default" };

        public string SocketName => string.IsNullOrWhiteSpace(socketName) ? name : socketName.Trim();
        public string[] CompatibleSocketNames => compatibleSocketNames;

        public bool IsCompatibleWith(DoorwaySocket other, string otherFallbackName)
        {
            string otherName = other != null ? other.SocketName : otherFallbackName;
            if (string.IsNullOrWhiteSpace(otherName))
            {
                return false;
            }

            if (compatibleSocketNames == null || compatibleSocketNames.Length == 0)
            {
                return string.Equals(SocketName, otherName.Trim(), System.StringComparison.OrdinalIgnoreCase);
            }

            for (int i = 0; i < compatibleSocketNames.Length; i++)
            {
                if (string.Equals(compatibleSocketNames[i], otherName.Trim(), System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
