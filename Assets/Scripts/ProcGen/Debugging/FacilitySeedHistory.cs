using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EscapeBlock9.ProcGen.Debugging
{
    [Serializable]
    public struct FailedSeedEntry
    {
        public int Seed;
        public string TimestampUtc;
        public string Context;
        public string ErrorSummary;
    }

    public static class FacilitySeedHistory
    {
        private const string FailedSeedsFileName = "procgen_failed_seeds.jsonl";

        public static void AppendFailedSeed(int seed, string context, string errorSummary)
        {
            var entry = new FailedSeedEntry
            {
                Seed = seed,
                TimestampUtc = DateTime.UtcNow.ToString("O"),
                Context = context ?? string.Empty,
                ErrorSummary = errorSummary ?? string.Empty
            };

            string path = GetFailedSeedsPath();
            string json = JsonUtility.ToJson(entry);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path) ?? string.Empty);
                File.AppendAllText(path, json + Environment.NewLine);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to append failed-seed log: {ex.Message}");
            }
        }

        public static IReadOnlyList<FailedSeedEntry> ReadRecent(int maxEntries = 20)
        {
            var entries = new List<FailedSeedEntry>();
            string path = GetFailedSeedsPath();
            if (!File.Exists(path))
            {
                return entries;
            }

            try
            {
                string[] lines = File.ReadAllLines(path);
                int start = Mathf.Max(0, lines.Length - Mathf.Max(1, maxEntries));
                for (int i = start; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    entries.Add(JsonUtility.FromJson<FailedSeedEntry>(line));
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to read failed-seed log: {ex.Message}");
            }

            return entries;
        }

        public static string GetFailedSeedsPath()
        {
            return Path.Combine(Application.persistentDataPath, FailedSeedsFileName);
        }
    }
}
