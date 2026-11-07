using System;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.SceneManagement;

[assembly: UnityEngine.Scripting.AlwaysLinkAssembly]

namespace Winterbourne.Pass
{
    // The bundle carries its own StringTables. Only the journey installs its
    // provider; tables owned by the host project continue through the previous provider.
    static class PassNativeLanguage
    {
        const string Bundle = "WinterbourneNative/";
        const string Journey = "WinterbournePass";
        static StringTable english, chinese;
        static LocalizationSettings settings;
        static Locale englishLocale, chineseLocale;
        static TableProvider provider;
        static bool connected;
        static bool fromUnity;
        static AsyncOperationHandle<LocalizationSettings> initialization;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Register()
        {
            Disconnect();
            SceneManager.sceneLoaded -= Loaded;
            SceneManager.sceneUnloaded -= Unloaded;
            SceneManager.sceneLoaded += Loaded;
            SceneManager.sceneUnloaded += Unloaded;
        }

        static void Loaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != Journey || connected) return;
            english = Resources.Load<StringTable>(Bundle+"Interface-en");
            chinese = Resources.Load<StringTable>(Bundle+"Interface-zh-Hans");
            if (!english || !chinese) return;
            connected = true;
            PassText.UseTables((key, zh) => (zh ? chinese : english).GetEntry(key)?.Value);
            if (!LocalizationSettings.HasSettings) return;
            settings = LocalizationSettings.Instance;
            var db = settings.GetStringDatabase();
            provider = new TableProvider(db.TableProvider);
            db.TableProvider = provider;
            initialization = settings.GetInitializationOperation();
            if (initialization.IsDone) Ready(initialization);
            else initialization.Completed += Ready;
        }

        static void Unloaded(Scene scene)
        {
            if (scene.name == Journey) Disconnect();
        }

        static void Ready(AsyncOperationHandle<LocalizationSettings> operation)
        {
            if (!connected || !settings || operation.Status != AsyncOperationStatus.Succeeded || operation.Result != settings) return;
            englishLocale = RegisterLocale("en");
            chineseLocale = RegisterLocale("zh-Hans");
            // The saved journey preference takes precedence on entering the game.
            ApplySelection();
            settings.OnSelectedLocaleChanged += FromUnity;
            PassText.Changed += ApplySelection;
        }

        static Locale RegisterLocale(string code)
        {
            var available = settings.GetAvailableLocales();
            foreach (var existing in available.Locales)
                if (existing && existing.Identifier.Code.Equals(code,StringComparison.OrdinalIgnoreCase)) return existing;
            var bundled = Resources.Load<Locale>(Bundle+"Locale-"+code);
            if (bundled) available.AddLocale(bundled);
            return bundled;
        }

        static void ApplySelection()
        {
            var chosen = PassText.Chinese ? chineseLocale : englishLocale;
            if (!fromUnity && chosen && settings) settings.SetSelectedLocale(chosen);
        }

        static void FromUnity(Locale locale)
        {
            if (!locale) return;
            fromUnity = true;
            try
            {
                if (IsLanguage(locale, "en")) PassText.SetChinese(false);
                else if (IsLanguage(locale, "zh")) PassText.SetChinese(true);
            }
            finally { fromUnity = false; }
        }

        static bool IsLanguage(Locale locale, string code)
        {
            string id = locale.Identifier.Code;
            return string.Equals(id,code,StringComparison.OrdinalIgnoreCase) ||
                   id.StartsWith(code+"-",StringComparison.OrdinalIgnoreCase);
        }

        static void Disconnect()
        {
            if (!connected) return;
            connected = false;
            if (initialization.IsValid()) initialization.Completed -= Ready;
            initialization = default;
            PassText.Changed -= ApplySelection;
            if (settings)
            {
                settings.OnSelectedLocaleChanged -= FromUnity;
                var db = settings.GetStringDatabase();
                if (provider != null && ReferenceEquals(db.TableProvider,provider)) db.TableProvider = provider.Previous;
            }
            PassText.UseTables(null);
            settings = null;
            provider = null;
            english = chinese = null;
            englishLocale = chineseLocale = null;
        }

        sealed class TableProvider : ITableProvider
        {
            public readonly ITableProvider Previous;
            public TableProvider(ITableProvider previous) { Previous = previous; }
            public AsyncOperationHandle<T> ProvideTableAsync<T>(string collection, Locale locale) where T : LocalizationTable
            {
                if (typeof(T) == typeof(StringTable) && locale && english && chinese && collection == english.TableCollectionName)
                {
                    StringTable table = IsLanguage(locale,"en") ? english : IsLanguage(locale,"zh") ? chinese : null;
                    if (table) return Addressables.ResourceManager.CreateCompletedOperation(table as T,null);
                }
                return Previous == null ? default : Previous.ProvideTableAsync<T>(collection,locale);
            }
        }
    }
}
