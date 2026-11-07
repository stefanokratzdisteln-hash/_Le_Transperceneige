using System;
using System.Collections.Generic;
using UnityEngine;

namespace Winterbourne.Pass
{
    public static class PassText
    {
        const string Preference = "Winterbourne.Language";
        static Dictionary<string, string[]> catalogue;
        static bool chinese;
        static bool initialized;
        static Func<string, bool, string> tableLookup;
        public static event Action Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            initialized = false;
            catalogue = null;
            tableLookup = null;
            Changed = null;
        }

        public static bool Chinese {
            get {
                if (!initialized) {
                    chinese = PlayerPrefs.GetString(Preference, "en") == "zh-Hans";
                    initialized = true;
                }
                return chinese;
            }
        }

        public static void SetChinese(bool value)
        {
            if (Chinese == value) return;
            chinese = value;
            PlayerPrefs.SetString(Preference, value ? "zh-Hans" : "en");
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void UseTables(Func<string,bool,string> lookup) => tableLookup = lookup;

        // The same catalogue is also used by the scene package in plain URP projects.
        public static IReadOnlyDictionary<string,string[]> Entries
        {
            get {
                if (catalogue != null) return catalogue;
                catalogue = new Dictionary<string,string[]>(StringComparer.Ordinal);
                var text = Resources.Load<TextAsset>("WinterbourneText");
                foreach (string line in text.text.Split('\n')) {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var fields = line.TrimEnd('\r').Split('\t');
                    if (fields.Length != 3) throw new FormatException("Invalid Winterbourne translation: "+line);
                    catalogue.Add(fields[0],new[] {fields[1].Replace("\\n","\n"),fields[2].Replace("\\n","\n")});
                }
                return catalogue;
            }
        }

        public static string Get(string key)
        {
            if (string.IsNullOrEmpty(key)) return "";
            bool zh = Chinese;
            return tableLookup?.Invoke(key,zh) ?? (Entries.TryGetValue(key,out var line) ? line[zh?1:0] : key);
        }
    }
}
