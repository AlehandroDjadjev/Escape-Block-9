using System;
using UnityEngine;

[Serializable]
public struct SerializedItemData
{
    public string itemId;
    public string displayName;
}

public class ItemSerializer : MonoBehaviour
{
    [SerializeField] private string saveKey = "player_single_item_inventory";

    public void Save(SerializedItemData data)
    {
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(saveKey, json);
        PlayerPrefs.Save();
    }

    public void Clear()
    {
        if (PlayerPrefs.HasKey(saveKey))
        {
            PlayerPrefs.DeleteKey(saveKey);
            PlayerPrefs.Save();
        }
    }

    public bool TryLoad(out SerializedItemData data)
    {
        data = default;
        if (!PlayerPrefs.HasKey(saveKey))
        {
            return false;
        }

        string json = PlayerPrefs.GetString(saveKey);
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        data = JsonUtility.FromJson<SerializedItemData>(json);
        return !string.IsNullOrWhiteSpace(data.itemId);
    }
}
