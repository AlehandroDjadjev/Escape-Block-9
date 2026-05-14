using UnityEngine;

[RequireComponent(typeof(EntitySerializer))]
public class Entity : MonoBehaviour
{
    [SerializeField] private string entityId = "entity_default";
    [SerializeField] private string entityName = "Entity";
    [TextArea(5, 20)]
    [SerializeField] private string dialogueJson;
    [SerializeField] private TextAsset dialogueTreeJson;
    [SerializeField] private string talkKeyLabel = "F";
    [SerializeField] private string talkPromptText = "Talk";
    [SerializeField] private float talkRadius = 2.6f;
    [SerializeField] private Transform talkPoint;

    private EntitySerializer serializer;

    public string EntityId => entityId;
    public string EntityName => entityName;
    public string TalkKeyLabel => talkKeyLabel;
    public string TalkPromptText => talkPromptText;
    public float TalkRadius => talkRadius;
    public Transform TalkPoint => talkPoint != null ? talkPoint : transform;

    private void Awake()
    {
        serializer = GetComponent<EntitySerializer>();
    }

    public bool TryGetDialogueTree(out DialogueTreeData tree)
    {
        tree = null;
        string sourceJson = dialogueTreeJson != null ? dialogueTreeJson.text : dialogueJson;
        if (string.IsNullOrWhiteSpace(sourceJson))
        {
            return false;
        }

        tree = JsonUtility.FromJson<DialogueTreeData>(sourceJson);
        return tree != null && tree.nodes != null && tree.nodes.Length > 0;
    }

    public void MarkTalked(string lastNodeId, int talksCount)
    {
        SerializedEntityData data = new SerializedEntityData
        {
            entityId = entityId,
            entityName = entityName,
            talksCount = talksCount,
            lastNodeId = lastNodeId
        };

        serializer.Save(data);
    }
}
