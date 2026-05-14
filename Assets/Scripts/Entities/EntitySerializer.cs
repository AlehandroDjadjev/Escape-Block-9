using UnityEngine;

[System.Serializable]
public struct SerializedEntityData
{
    public string entityId;
    public string entityName;
    public int talksCount;
    public string lastNodeId;
}

public class EntitySerializer : MonoBehaviour
{
    [SerializeField] private string saveKeyPrefix = "entity_state_";

    public void Save(SerializedEntityData data)
    {
        if (string.IsNullOrWhiteSpace(data.entityId))
        {
            return;
        }

        string key = saveKeyPrefix + data.entityId;
        PlayerPrefs.SetString(key, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    public bool TryLoad(string entityId, out SerializedEntityData data)
    {
        data = default;
        if (string.IsNullOrWhiteSpace(entityId))
        {
            return false;
        }

        string key = saveKeyPrefix + entityId;
        if (!PlayerPrefs.HasKey(key))
        {
            return false;
        }

        string json = PlayerPrefs.GetString(key);
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        data = JsonUtility.FromJson<SerializedEntityData>(json);
        return !string.IsNullOrWhiteSpace(data.entityId);
    }
}
