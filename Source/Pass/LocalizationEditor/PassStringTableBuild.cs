using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Tables;
using Winterbourne.Pass;

namespace Winterbourne.Editor
{
    public static class PassStringTableBuild
    {
        const string Folder = "Assets/Transperceneige/Pass/Language/Native/Resources/WinterbourneNative";
        const string Collection = "Winterbourne Interface";

        [MenuItem("Tools/Winterbourne/Rebuild EN and ZH String Tables")]
        public static void Bake()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var shared = Asset<SharedTableData>("Interface-Keys", ScriptableObject.CreateInstance<SharedTableData>);
            shared.TableCollectionName = Collection;
            // Unity's editor uses the .meta GUID as the portable TableReference GUID.
            string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(shared));
            if (shared.TableCollectionNameGuid != Guid.Parse(guid))
            {
                var serialized = new SerializedObject(shared);
                serialized.FindProperty("m_TableCollectionNameGuidString").stringValue = guid;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                shared.OnAfterDeserialize();
            }
            var catalogue = PassText.Entries;
            foreach (var entry in shared.Entries.Where(e=>!catalogue.ContainsKey(e.Key)).ToArray())
                shared.RemoveKey(entry.Id);
            foreach (var key in catalogue.Keys.OrderBy(k=>k,StringComparer.Ordinal))
                if (shared.GetEntry(key)==null) shared.AddKey(key);
            EditorUtility.SetDirty(shared);

            foreach (string code in new[]{"en","zh-Hans"})
            {
                var locale = Asset("Locale-"+code, () => Locale.CreateLocale(code));
                var table = Asset<StringTable>("Interface-"+code, ScriptableObject.CreateInstance<StringTable>);
                table.SharedData = shared;
                table.LocaleIdentifier = locale.Identifier;
                table.Clear();
                foreach (var text in catalogue)
                    table.AddEntry(text.Key, text.Value[code=="en" ? 0 : 1]);
                EditorUtility.SetDirty(table);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("Winterbourne native string tables: "+catalogue.Count+" bilingual entries.");
        }

        static T Asset<T>(string name, Func<T> create) where T : ScriptableObject
        {
            string path = Folder+"/"+name+".asset";
            var resource = AssetDatabase.LoadAssetAtPath<T>(path);
            if (resource) return resource;
            resource = create();
            resource.name = name;
            AssetDatabase.CreateAsset(resource,path);
            return resource;
        }
    }
}
