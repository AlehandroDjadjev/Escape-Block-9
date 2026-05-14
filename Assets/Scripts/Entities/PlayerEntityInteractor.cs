using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerEntityInteractor : MonoBehaviour
{
    [SerializeField] private Camera playerCamera;
    [SerializeField] private PickupPromptUI talkPromptUi;
    [SerializeField] private SubtitleUI subtitleUi;
    [SerializeField] private DialogueChoiceUI dialogueChoiceUi;

    private Entity currentTarget;
    private bool isPromptCursorMode;
    private int talksCount;
    private bool isInConversation;
    private int selectedChoiceIndex;
    private bool choiceSelected;

    private void Awake()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }

        if (talkPromptUi == null)
        {
            talkPromptUi = FindAnyObjectByType<PickupPromptUI>();
            if (talkPromptUi == null)
            {
                GameObject uiObj = new GameObject("TalkPromptUI");
                talkPromptUi = uiObj.AddComponent<PickupPromptUI>();
            }
        }

        if (subtitleUi == null)
        {
            subtitleUi = FindAnyObjectByType<SubtitleUI>();
            if (subtitleUi == null)
            {
                GameObject uiObj = new GameObject("SubtitleUI");
                subtitleUi = uiObj.AddComponent<SubtitleUI>();
            }
        }

        if (dialogueChoiceUi == null)
        {
            dialogueChoiceUi = FindAnyObjectByType<DialogueChoiceUI>();
            if (dialogueChoiceUi == null)
            {
                GameObject uiObj = new GameObject("DialogueChoiceUI");
                dialogueChoiceUi = uiObj.AddComponent<DialogueChoiceUI>();
            }
        }
    }

    private void Update()
    {
        if (isInConversation)
        {
            talkPromptUi.Hide();
            return;
        }

        if (subtitleUi != null && subtitleUi.IsPlaying)
        {
            talkPromptUi.Hide();
            SetPromptCursorMode(false);
            return;
        }

        currentTarget = FindNearestEntity();
        if (currentTarget == null)
        {
            talkPromptUi.Hide();
            SetPromptCursorMode(false);
            return;
        }

        string prompt = $"{currentTarget.TalkPromptText} {currentTarget.EntityName}";
        talkPromptUi.Show(currentTarget.TalkKeyLabel, prompt, TryTalkCurrentTarget);
        SetPromptCursorMode(false);

        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            TryTalkCurrentTarget();
        }
    }

    private void TryTalkCurrentTarget()
    {
        if (currentTarget == null || subtitleUi == null)
        {
            return;
        }

        if (!currentTarget.TryGetDialogueTree(out DialogueTreeData tree))
        {
            return;
        }

        StartCoroutine(RunConversation(currentTarget, tree));
        talkPromptUi.Hide();
        SetPromptCursorMode(false);
    }

    private IEnumerator RunConversation(Entity entity, DialogueTreeData tree)
    {
        isInConversation = true;
        dialogueChoiceUi.Hide();
        SetPromptCursorMode(false);

        Dictionary<string, DialogueNodeData> lookup = DialogueInterpreter.BuildLookup(tree);
        DialogueNodeData currentNode = DialogueInterpreter.GetStartNode(tree, lookup);
        string lastNodeId = string.Empty;
        HashSet<string> visited = new HashSet<string>();

        while (currentNode != null)
        {
            if (!string.IsNullOrWhiteSpace(currentNode.id) && visited.Contains(currentNode.id))
            {
                break;
            }

            if (!string.IsNullOrWhiteSpace(currentNode.id))
            {
                visited.Add(currentNode.id);
                lastNodeId = currentNode.id;
            }

            if (currentNode.lines != null && currentNode.lines.Length > 0)
            {
                subtitleUi.PlayLines(new List<string>(currentNode.lines));
                while (subtitleUi.IsPlaying)
                {
                    yield return null;
                }
            }

            int chosen = 0;
            if (currentNode.choices != null && currentNode.choices.Length > 0)
            {
                choiceSelected = false;
                selectedChoiceIndex = 0;
                SetPromptCursorMode(true);
                dialogueChoiceUi.Show(currentNode.choices, OnChoiceSelected);

                while (!choiceSelected)
                {
                    yield return null;
                }

                dialogueChoiceUi.Hide();
                SetPromptCursorMode(false);
                chosen = selectedChoiceIndex;
            }

            string nextNodeId = DialogueInterpreter.ResolveNextNodeId(currentNode, chosen);
            if (!DialogueInterpreter.TryGetNode(nextNodeId, lookup, out currentNode))
            {
                break;
            }
        }

        talksCount++;
        entity.MarkTalked(lastNodeId, talksCount);
        isInConversation = false;
        SetPromptCursorMode(false);
    }

    private void OnChoiceSelected(int index)
    {
        selectedChoiceIndex = index;
        choiceSelected = true;
    }

    private Entity FindNearestEntity()
    {
        Entity[] entities = FindObjectsByType<Entity>();
        if (entities == null || entities.Length == 0)
        {
            return null;
        }

        Vector3 origin = playerCamera != null ? playerCamera.transform.position : transform.position;
        Entity nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (Entity entity in entities)
        {
            if (entity == null)
            {
                continue;
            }

            float distance = Vector3.Distance(origin, entity.TalkPoint.position);
            if (distance <= entity.TalkRadius && distance < nearestDistance)
            {
                nearest = entity;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private void SetPromptCursorMode(bool enabled)
    {
        if (isPromptCursorMode == enabled)
        {
            return;
        }

        isPromptCursorMode = enabled;
        Cursor.lockState = enabled ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = enabled;
    }
}
