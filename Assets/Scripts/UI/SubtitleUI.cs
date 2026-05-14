using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

public class SubtitleUI : MonoBehaviour
{
    [Header("Timing")]
    [SerializeField] private float wordsPerSecond = 2.5f;
    [SerializeField] private float minSubtitleDuration = 1.2f;
    [SerializeField] private float maxSubtitleDuration = 4.2f;
    [SerializeField] private float fadeDuration = 0.2f;
    [SerializeField] private int maxWordsPerSubtitle = 8;

    private CanvasGroup canvasGroup;
    private Text subtitleText;
    private Coroutine activeRoutine;

    public bool IsPlaying => activeRoutine != null;

    public void PlayLines(List<string> lines)
    {
        EnsureUi();

        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
        }

        activeRoutine = StartCoroutine(PlayRoutine(lines));
    }

    private IEnumerator PlayRoutine(List<string> lines)
    {
        if (lines == null || lines.Count == 0)
        {
            yield break;
        }

        foreach (string line in lines)
        {
            List<string> chunks = ChunkLine(line, maxWordsPerSubtitle);
            foreach (string chunk in chunks)
            {
                subtitleText.text = chunk;
                yield return FadeTo(1f);

                int wordCount = CountWords(chunk);
                float holdTime = Mathf.Clamp(wordCount / wordsPerSecond, minSubtitleDuration, maxSubtitleDuration);
                yield return new WaitForSeconds(holdTime);

                yield return FadeTo(0f);
                yield return new WaitForSeconds(0.05f);
            }
        }

        subtitleText.text = string.Empty;
        activeRoutine = null;
    }

    private IEnumerator FadeTo(float target)
    {
        float start = canvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = fadeDuration > 0f ? Mathf.Clamp01(elapsed / fadeDuration) : 1f;
            canvasGroup.alpha = Mathf.Lerp(start, target, t);
            yield return null;
        }

        canvasGroup.alpha = target;
    }

    private void EnsureUi()
    {
        if (canvasGroup != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("SubtitleCanvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 3100;
        canvasObject.AddComponent<GraphicRaycaster>();

        canvasGroup = canvasObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;

        GameObject panelObject = new GameObject("SubtitlePanel");
        panelObject.transform.SetParent(canvasObject.transform, false);
        RectTransform panelRect = panelObject.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.08f);
        panelRect.anchorMax = new Vector2(0.5f, 0.08f);
        panelRect.sizeDelta = new Vector2(940f, 100f);
        panelRect.anchoredPosition = Vector2.zero;

        GameObject textObject = new GameObject("SubtitleText");
        textObject.transform.SetParent(panelObject.transform, false);
        RectTransform textRect = textObject.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(20f, 10f);
        textRect.offsetMax = new Vector2(-20f, -10f);

        subtitleText = textObject.AddComponent<Text>();
        subtitleText.alignment = TextAnchor.MiddleCenter;
        subtitleText.color = Color.white;
        subtitleText.fontSize = 30;
        subtitleText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        subtitleText.horizontalOverflow = HorizontalWrapMode.Wrap;
        subtitleText.verticalOverflow = VerticalWrapMode.Overflow;
    }

    private static int CountWords(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return 0;
        }

        return line.Split(' ', System.StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static List<string> ChunkLine(string line, int maxWords)
    {
        List<string> chunks = new List<string>();
        if (string.IsNullOrWhiteSpace(line))
        {
            return chunks;
        }

        string normalized = Regex.Replace(line.Trim(), @"\s+", " ");
        string[] clauses = Regex.Split(normalized, @"(?<=[\.\,\!\?\;\:\-\u2014])\s+");
        if (clauses.Length == 0)
        {
            chunks.Add(normalized);
            return chunks;
        }

        string current = string.Empty;
        int currentWords = 0;

        foreach (string rawClause in clauses)
        {
            string clause = rawClause.Trim();
            if (string.IsNullOrWhiteSpace(clause))
            {
                continue;
            }

            int clauseWords = CountWords(clause);
            if (string.IsNullOrWhiteSpace(current))
            {
                current = clause;
                currentWords = clauseWords;
                continue;
            }

            // Keep subtitle cuts on punctuation-based clauses only.
            if (currentWords + clauseWords <= maxWords)
            {
                current += " " + clause;
                currentWords += clauseWords;
            }
            else
            {
                chunks.Add(current);
                current = clause;
                currentWords = clauseWords;
            }
        }

        if (!string.IsNullOrWhiteSpace(current))
        {
            chunks.Add(current);
        }

        if (chunks.Count == 0)
        {
            chunks.Add(normalized);
        }

        return chunks;
    }
}
