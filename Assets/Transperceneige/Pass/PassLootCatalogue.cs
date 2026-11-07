using System.Collections.Generic;

namespace Winterbourne.Pass
{
    public static class PassLootCatalogue
    {
        static readonly Dictionary<string,int> Prices = new Dictionary<string,int>
        {
            {"parachute",0}, {"pocket_watch",52000}, {"crystal_cup",42000}, {"singing_bird_box",38000},
            {"portrait_snuffbox",48000}, {"emerald_tiara",62000}, {"intaglio_seal",32000}, {"cameo",23999},
            {"astrolabe",60000}, {"astronomical_globe",74000}, {"pearl_aigrette",36000},
            {"rose_egg",48000}, {"jewelled_fan",26000}, {"diptych_dial",22000}, {"cloisonne_vase",28000},
            {"silver_nef",30000}, {"scent_flacon",20000}
        };

        public static bool Contains(string id) => id!=null && Prices.ContainsKey(id);
        public static int Value(string id) => id!=null && Prices.TryGetValue(id,out int value) ? value : 0;
        public static string Format(long value) =>
            value.ToString("N0",System.Globalization.CultureInfo.InvariantCulture);
    }
}
