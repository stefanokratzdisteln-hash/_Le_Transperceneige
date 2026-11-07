using System;
using System.Collections.Generic;
using UnityEngine;

namespace Winterbourne.Pass
{
    public sealed partial class PassJourney
    {
        readonly PassPickup[] slots = new PassPickup[8];
        PassPickup[] items;
        PassLootContainer[] containers;
        Transform authoredSleeper;
        PassPickup openingWatch;
        LootSite[] sites;
        readonly List<PassPickup> searchResults = new List<PassPickup>();
        readonly Dictionary<string,string> fallbackNames = new Dictionary<string,string>();
        PassPickup looseOffer;
        Vector3 searchedContact;
        float lockTravel;
        bool pickingLock, restoreLook;
        public bool SearchOpen => OpenContainer || looseOffer;
        public bool PickingLock => pickingLock;
        public bool AwaitingSwap { get; private set; }
        public PassLootContainer OpenContainer { get; private set; }
        public int SelectedSearchIndex { get; private set; }
        public int QuickSlot { get; private set; }
        public int SearchCount => searchResults.Count;
        public int LootRevision { get; private set; }
        public float LockPosition => Mathf.PingPong(lockTravel,1f);
        public PassPickup SearchItem(int index) => index>=0 && index<SearchCount ? searchResults[index] : null;
        public string Slot(int index) => index>=0 && index<slots.Length && slots[index] ? slots[index].itemId : null;
        public bool HasParachute => Array.Exists(slots,p=>p && p.itemId=="parachute");
        public int CarriedValue
        {
            get
            {
                int value=0;
                foreach(var p in slots) if(p) value+=PassLootCatalogue.Value(p.itemId);
                return value;
            }
        }

        public string ItemName(string id)
        {
            if(id=="parachute") return PassText.Get("帆布跳伞包");
            if(PassLootCatalogue.Contains(id)) return PassText.Get("item."+id);
            return id!=null && fallbackNames.TryGetValue(id,out string label) ? PassText.Get(label) : id;
        }

        struct LootSite
        {
            public Transform parent;
            public Vector3 position;
            public Quaternion rotation;
            public PassLootContainer container;
        }

        sealed class LootSnapshot
        {
            public PassPickup[] bag;
            public int[] locations;
            public bool[] unlocked;
            public int quickSlot;
        }

        void RegisterLoot()
        {
            authoredSleeper=sleeperSpawn;
            var all=FindObjectsByType<PassPickup>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            items=Array.FindAll(all,p=>p.gameObject.scene==gameObject.scene);
            Array.Sort(items,(a,b)=>StringComparer.Ordinal.Compare(a.siteId,b.siteId));
            var allCases=FindObjectsByType<PassLootContainer>(FindObjectsInactive.Include,FindObjectsSortMode.None);
            containers=Array.FindAll(allCases,c=>c.gameObject.scene==gameObject.scene);
            sites=new LootSite[items.Length];
            var identifiers=new HashSet<string>();
            for(int i=0;i<items.Length;i++)
            {
                var item=items[i];
                if(!string.IsNullOrEmpty(item.siteId) && !identifiers.Add(item.siteId))
                    throw new InvalidOperationException("Duplicate loot site "+item.siteId);
                sites[i]=new LootSite{parent=item.transform.parent,position=item.transform.localPosition,
                    rotation=item.transform.localRotation,container=item.GetComponentInParent<PassLootContainer>(true)};
                item.currentSite=item.gameObject.activeSelf ? i : -1;
                fallbackNames[item.itemId]=item.label;
            }
        }

        void StartLootRoll()
        {
            Array.Clear(slots,0,slots.Length);
            QuickSlot=0;
            foreach(var c in containers) c.Restore(!c.locked);
            var random=new System.Random(Guid.NewGuid().GetHashCode());
            var groups=new Dictionary<string,List<int>>(StringComparer.Ordinal);
            for(int i=0;i<items.Length;i++)
            {
                string group=items[i].rollGroup;
                PlaceLoot(items[i],string.IsNullOrEmpty(group) ? i : -1);
                if(string.IsNullOrEmpty(group)) continue;
                if(!groups.TryGetValue(group,out var options)) groups[group]=options=new List<int>();
                options.Add(i);
            }
            foreach(var options in groups.Values)
            {
                int index=options[random.Next(options.Count)];
                PlaceLoot(items[index],index);
            }
            Physics.SyncTransforms();
            openingWatch=null; sleeperSpawn=authoredSleeper;
            foreach(var p in items)
                if(p.currentSite>=0 && p.itemId=="pocket_watch" && p.bedsideArrival)
                {
                    sleeperSpawn=p.bedsideArrival;
                    openingWatch=p;
                    break;
                }
            if(sleeperSpawn && initialRide!=null)
            {
                initialRide.position=consist.InverseTransformPoint(sleeperSpawn.position);
                BedsideView(out var rotation,out var pitch);
                initialRide.rotation=Quaternion.Inverse(consist.rotation)*rotation;
                initialRide.pitch=pitch;
            }
            if(groups.Count>0 && !RoutesAvailable())
                Debug.LogError("The authored loot roll cannot supply both extraction manifests.",this);
            LootRevision++;
        }

        void BedsideView(out Quaternion heading,out float pitch)
        {
            heading=sleeperSpawn.rotation;
            pitch=0;
            if(!openingWatch || !walker.eyeCamera) return;
            var shape=openingWatch.GetComponentInChildren<Collider>();
            Vector3 target=shape ? shape.bounds.center : openingWatch.transform.position;
            Vector3 eye=sleeperSpawn.position+Vector3.up*walker.eyeCamera.transform.localPosition.y;
            Vector3 look=(target-eye).normalized;
            Vector3 flat=Vector3.ProjectOnPlane(look,Vector3.up);
            if(flat.sqrMagnitude<.001f) return;
            heading=Quaternion.LookRotation(flat);
            pitch=-Mathf.Asin(Mathf.Clamp(look.y,-1f,1f))*Mathf.Rad2Deg;
        }

        // The two manifests are separate tests: the station route can never count an
        // article from the same model dropped elsewhere, and the train route reserves a slot for the pack.
        bool RoutesAvailable()
        {
            bool Available(string id,Transform zone,bool station)
            {
                foreach(var item in items)
                {
                    if(item.currentSite<0 || item.itemId!=id) continue;
                    if(station ? !item.transform.IsChildOf(consist) : zone && item.transform.IsChildOf(zone)) return true;
                }
                return false;
            }
            var shared=new[]{("pocket_watch",consist.Find("Car07")),("crystal_cup",consist.Find("Car06")),
                ("singing_bird_box",consist.Find("Car05"))};
            int commonValue=0;
            foreach(var (id,car) in shared)
            {
                if(!Available(id,car,false)) return false;
                commonValue+=PassLootCatalogue.Value(id);
            }
            bool Completes(string[] extra,bool station,int reserved)
            {
                if(shared.Length+extra.Length+reserved>slots.Length) return false;
                int value=commonValue;
                foreach(var id in extra)
                {
                    if(!Available(id,consist,station)) return false;
                    value+=PassLootCatalogue.Value(id);
                }
                return EnoughToExtract(value);
            }
            return Completes(new[]{"portrait_snuffbox","emerald_tiara"},true,0) &&
                Completes(new[]{"intaglio_seal","rose_egg","pearl_aigrette"},false,1) &&
                Available("parachute",consist,false);
        }

        void PlaceLoot(PassPickup item,int index)
        {
            item.currentSite=index;
            if(index>=0)
            {
                var site=sites[index];
                item.transform.SetParent(site.parent,false);
                item.transform.SetLocalPositionAndRotation(site.position,site.rotation);
            }
            item.gameObject.SetActive(index>=0);
        }

        LootSnapshot SaveLoot() => new LootSnapshot{bag=(PassPickup[])slots.Clone(),
            locations=Array.ConvertAll(items,p=>p.currentSite),unlocked=Array.ConvertAll(containers,c=>c.Unlocked),
            quickSlot=QuickSlot};

        void RestoreLoot(LootSnapshot snapshot)
        {
            CancelSearch();
            for(int i=0;i<items.Length;i++) PlaceLoot(items[i],snapshot.locations[i]);
            for(int i=0;i<containers.Length;i++) containers[i].Restore(snapshot.unlocked[i]);
            Array.Copy(snapshot.bag,slots,slots.Length);
            QuickSlot=snapshot.quickSlot;
            LootRevision++;
        }

        bool UsableWorld => !Terminal && !InStartMenu && !InHub && !AtLanding && !Airborne &&
            body && body.enabled && !walker.externalMotion;

        bool LootInSight(out PassPickup item,out PassLootContainer container,out Vector3 point)
        {
            item=null; container=null; point=default;
            if(!UsableWorld || !walker.eyeCamera) return false;
            var eye=walker.eyeCamera.transform;
            if(!Physics.Raycast(eye.position,eye.forward,out var hit,walker.interactionDistance,
                Physics.DefaultRaycastLayers,QueryTriggerInteraction.Collide)) return false;
            point=hit.point;
            item=hit.collider.GetComponentInParent<PassPickup>();
            if(item && item.currentSite>=0) container=sites[item.currentSite].container;
            else { item=null; container=hit.collider.GetComponentInParent<PassLootContainer>(); }
            return item || container;
        }

        bool LootPrompt(out string text)
        {
            text=null;
            if(!LootInSight(out var item,out var container,out _)) return false;
            text=container ? string.Format(PassText.Get(container.Unlocked?"search.open":"search.unlock"),PassText.Get(container.labelKey))
                : string.Format(PassText.Get("search.pick"),ItemName(item.itemId),PassLootCatalogue.Format(PassLootCatalogue.Value(item.itemId)));
            return true;
        }

        public bool UseFocused()
        {
            if(SearchOpen)
            {
                if(!ContactValid()) { CloseSearch(); return false; }
                if(!pickingLock) return PickSelected();
                if(LockPosition<.53f || LockPosition>.77f)
                {
                    Tell("search.lockMiss"); lockTravel=0;
                    return false;
                }
                pickingLock=false;
                OpenContainer.Open();
                RebuildResults();
                return true;
            }
            if(Terminal || InStartMenu || InventoryOpen || walker.externalMotion) return false;
            if(!LootInSight(out var item,out var fixture,out var contact)) return TryUsePassage();
            if(!fixture) return TryPick(item);
            noticeUntil=-1;
            OpenContainer=fixture;
            searchedContact=fixture.transform.InverseTransformPoint(contact);
            pickingLock=!fixture.Unlocked;
            lockTravel=0;
            if(!pickingLock) fixture.Open();
            SetInventory(true);
            RebuildResults();
            return true;
        }

        void RebuildResults()
        {
            searchResults.Clear();
            if(OpenContainer && !pickingLock)
            {
                foreach(var p in items)
                    if(p.currentSite>=0 && sites[p.currentSite].container==OpenContainer)
                        searchResults.Add(p);
            }
            else if(looseOffer && looseOffer.currentSite>=0) searchResults.Add(looseOffer);
            SelectedSearchIndex=Mathf.Clamp(SelectedSearchIndex,0,Mathf.Max(0,SearchCount-1));
            LootRevision++;
        }

        bool ContactValid()
        {
            var t=OpenContainer ? OpenContainer.transform : looseOffer ? looseOffer.transform : null;
            if(!UsableWorld || !t || !t.gameObject.activeInHierarchy) return false;
            return (walker.eyeCamera.transform.position-t.TransformPoint(searchedContact)).sqrMagnitude
                <=Mathf.Pow(walker.interactionDistance+.15f,2);
        }

        public void CloseSearch() => SetInventory(false);

        void CancelSearch()
        {
            if(OpenContainer) OpenContainer.Close();
            OpenContainer=null; looseOffer=null; AwaitingSwap=false; pickingLock=false;
            SelectedSearchIndex=0; searchResults.Clear();
            LootRevision++;
        }

        public void SelectSearchItem(int index)
        {
            if(index<0 || index>=SearchCount) return;
            SelectedSearchIndex=index;
            AwaitingSwap=false; LootRevision++;
        }

        public bool TryPick(PassPickup item)
        {
            if(!item || !UsableWorld || !item.gameObject.activeInHierarchy || item.currentSite<0) return false;
            if(SearchOpen)
            {
                if(!ContactValid() || pickingLock || !searchResults.Contains(item)) return false;
            }
            else
            {
                if(InventoryOpen || !LootInSight(out var aimed,out var fixture,out var contact) || aimed!=item || fixture) return false;
                searchedContact=item.transform.InverseTransformPoint(contact);
            }
            int slot=Array.IndexOf(slots,null);
            if(slot<0)
            {
                if(!SearchOpen) { looseOffer=item; SetInventory(true); RebuildResults(); }
                AwaitingSwap=true; LootRevision++; Tell("背包已满");
                return false;
            }
            StoreItem(item,slot);
            return true;
        }

        public bool PickSelected() => TryPick(SearchItem(SelectedSearchIndex));

        public int TakeAllFocused()
        {
            if(!SearchOpen || pickingLock || !ContactValid()) return 0;
            int taken=0,initialValue=CarriedValue;
            while(SearchCount>0)
            {
                int slot=Array.IndexOf(slots,null);
                if(slot<0) { Tell("背包已满"); break; }
                StoreItem(searchResults[0],slot);
                taken++;
            }
            if(initialValue<TargetValue && CarriedValue>=TargetValue) Tell("search.targetMet");
            return taken;
        }

        public bool ExchangeSlot(int index)
        {
            if(!SearchOpen || !AwaitingSwap || !ContactValid() || index<0 || index>=slots.Length) return false;
            var item=SearchItem(SelectedSearchIndex);
            if(!item || item.currentSite<0) return false;
            StoreItem(item,index);
            return true;
        }

        void StoreItem(PassPickup item,int slot)
        {
            int priorValue=CarriedValue;
            var old=slots[slot];
            int source=item.currentSite;
            PlaceLoot(item,-1);
            slots[slot]=item;
            if(old) PlaceLoot(old,source);
            AwaitingSwap=false;
            if(looseOffer) CloseSearch();
            else if(OpenContainer) RebuildResults();
            LootRevision++;
            if(priorValue<TargetValue && CarriedValue>=TargetValue) Tell("search.targetMet");
            else { Tell("已收好 · "); noticeItem=item.itemId; }
        }

        public bool MoveSlot(int from,int to)
        {
            if(!InventoryOpen || AwaitingSwap || from<0 || from>=8 || to<0 || to>=8 || !slots[from]) return false;
            (slots[from],slots[to])=(slots[to],slots[from]);
            LootRevision++; return true;
        }

        public bool ChooseQuickSlot(int index)
        {
            if(index<0 || index>=4 || Terminal || InStartMenu) return false;
            QuickSlot=index; LootRevision++;
            return true;
        }
    }
}
