using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Winterbourne.Pass;
using Object=UnityEngine.Object;

namespace Winterbourne.Editor
{
    internal static partial class PassExtractionReview
    {
        static IEnumerator TreasuryJourneys()
        {
            var options=PassLootCatalogue.GrandTreasures;
            int[] expected={30,25,20,17,8};
            for(int i=0;i<options.Count;i++)
                Check(Enumerable.Range(0,100).Count(ticket=>PassLootCatalogue.GrandTreasureForTicket(ticket)==options[i])
                    ==expected[i],"Wrong probability for "+options[i]);

            var consignments=Object.FindObjectsByType<PassPickup>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(p=>p.rollGroup==PassLootCatalogue.TreasuryGroup).ToArray();
            var visited=new HashSet<string>();
            PassPickup Roll()
            {
                route.NewGame();
                var visible=consignments.Where(p=>p.gameObject.activeInHierarchy).ToArray();
                Check(visible.Length==3 && visible.Select(p=>p.GetComponentInParent<PassLootContainer>()).Distinct().Count()==3,
                    "Every cabinet must hold exactly one active deposit");
                var prize=visible.Single(p=>options.Contains(p.itemId));
                visited.Add(prize.siteId);
                return prize;
            }
            for(int i=0;i<720 && visited.Count<15;i++)Roll();
            Check(visited.Count==15,"A treasure or cabinet never appeared across genuine new games");
            Debug.Log("TREASURY ROLLS: five prize weights verified and all 15 article/cabinet combinations observed");
            foreach(var trial in new[]{("mystery_clock","04",false),("touring_violin","09",false),("touring_violin","FB",true)})
            {
                PassPickup prize=null;
                for(int i=0;i<480;i++)
                {
                    var candidate=Roll();
                    if(candidate.itemId==trial.Item1 && candidate.siteId.StartsWith(trial.Item2))
                    { prize=candidate;break; }
                }
                Check(prize,"Failed to roll requested field test");
                yield return EnterGate(route.hubGate,()=>route.Aboard);
                if(trial.Item3)yield return Collect("pocket_watch");
                bool onTrain=prize.transform.IsChildOf(route.consist);
                if(onTrain)yield return Collect(prize.itemId);
                yield return PlatformExit(onTrain && trial.Item2=="04"?5:7);
                if(!onTrain)yield return Collect(prize.itemId);
                yield return ReturnFromStation();
                yield return SettleAtHome(prize.siteId,PassLootCatalogue.Value(prize.itemId)+(trial.Item3?52000:0));
            }
        }
    }
}
