using System;
using System.Collections.Generic;

namespace UnityEngine
{
    // Device storage is replaced by a fresh dictionary for each isolated scenario.
    // Failure injection checks that full/unavailable storage cannot stop gameplay.
    public static class PlayerPrefs
    {
        private static readonly Dictionary<string, string> Values = new Dictionary<string, string>();
        public static bool FailWrites;
        public static int SaveCalls;

        public static string GetString(string key, string fallback = "")
        {
            string value;
            return Values.TryGetValue(key, out value) ? value : fallback;
        }

        public static int GetInt(string key, int fallback = 0)
        {
            int value;
            return int.TryParse(GetString(key), out value) ? value : fallback;
        }

        public static void SetString(string key, string value)
        {
            if (FailWrites) throw new InvalidOperationException("Injected device storage failure.");
            Values[key] = value;
        }

        public static void SetInt(string key, int value) { SetString(key, value.ToString()); }
        public static void Save()
        {
            if (FailWrites) throw new InvalidOperationException("Injected device storage failure.");
            SaveCalls++;
        }

        public static void ResetForTests()
        {
            Values.Clear();
            FailWrites = false;
            SaveCalls = 0;
        }
    }
}
