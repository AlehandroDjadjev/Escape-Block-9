using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DialogueChoiceData
{
    public string text;
    public string nextNodeId;
}

[Serializable]
public class DialogueNodeData
{
    public string id;
    public string[] lines;
    public string nextNodeId;
    public DialogueChoiceData[] choices;
}

[Serializable]
public class DialogueTreeData
{
    public string rootNodeId;
    public DialogueNodeData[] nodes;
}

public static class DialogueInterpreter
{
    public static Dictionary<string, DialogueNodeData> BuildLookup(DialogueTreeData tree)
    {
        Dictionary<string, DialogueNodeData> lookup = new Dictionary<string, DialogueNodeData>();
        if (tree == null || tree.nodes == null)
        {
            return lookup;
        }

        foreach (DialogueNodeData node in tree.nodes)
        {
            if (node != null && !string.IsNullOrWhiteSpace(node.id))
            {
                lookup[node.id] = node;
            }
        }

        return lookup;
    }

    public static DialogueNodeData GetStartNode(DialogueTreeData tree, Dictionary<string, DialogueNodeData> lookup)
    {
        if (tree == null || tree.nodes == null || tree.nodes.Length == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(tree.rootNodeId) &&
            lookup.TryGetValue(tree.rootNodeId, out DialogueNodeData root))
        {
            return root;
        }

        return tree.nodes[0];
    }

    public static string ResolveNextNodeId(DialogueNodeData node, int chosenIndex)
    {
        if (node == null)
        {
            return null;
        }

        if (node.choices != null && node.choices.Length > 0)
        {
            int index = Mathf.Clamp(chosenIndex, 0, node.choices.Length - 1);
            DialogueChoiceData choice = node.choices[index];
            return choice != null ? choice.nextNodeId : null;
        }

        return node.nextNodeId;
    }

    public static bool TryGetNode(
        string nodeId,
        Dictionary<string, DialogueNodeData> lookup,
        out DialogueNodeData node)
    {
        node = null;
        if (string.IsNullOrWhiteSpace(nodeId))
        {
            return false;
        }

        return lookup.TryGetValue(nodeId, out node);
    }

    public static List<string> Flatten(DialogueTreeData tree)
    {
        List<string> result = new List<string>();
        if (tree == null || tree.nodes == null || tree.nodes.Length == 0)
        {
            return result;
        }

        Dictionary<string, DialogueNodeData> lookup = BuildLookup(tree);

        DialogueNodeData current = GetStartNode(tree, lookup);
        HashSet<string> visited = new HashSet<string>();

        while (current != null)
        {
            if (!string.IsNullOrWhiteSpace(current.id) && visited.Contains(current.id))
            {
                break;
            }

            if (!string.IsNullOrWhiteSpace(current.id))
            {
                visited.Add(current.id);
            }

            if (current.lines != null)
            {
                foreach (string line in current.lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        result.Add(line.Trim());
                    }
                }
            }

            string next = ResolveNextNodeId(current, 0);
            if (string.IsNullOrWhiteSpace(next) || !lookup.TryGetValue(next, out current))
            {
                break;
            }
        }

        return result;
    }
}
