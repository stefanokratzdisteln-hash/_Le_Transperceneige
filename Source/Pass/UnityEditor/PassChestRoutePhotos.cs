using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using Winterbourne.Pass;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif
using Object=UnityEngine.Object;

namespace Winterbourne.Editor
{
    internal static partial class PassExtractionReview
    {
        // Invoke the normal walkthrough with -extractionCase chestPhotos
        // -extractionNativeKeys -chestReviewPhotos <external folder>.
        static IEnumerator WalkChestPhotos()
        {
            yield return PrepareNativeGameView();
            var container=Object.FindObjectsByType<PassLootContainer>(FindObjectsSortMode.None);
            var coach=container.Single(c=>c.name=="04 drawing room treasury");
            var parcels=container.Single(c=>c.name=="FB parcels office treasury");
            var wood=container.Single(c=>c.name=="05 forwarded passenger chest");
            var stationWood=container.Single(c=>c.name=="FB parcels office chest");
            // Find an actual consignment at one of the two camera positions by
            // rolling normal New Games. Never assign a site, price, item or slot.
            int rolls=0;
            do {
                route.NewGame();
                rolls++;
            } while(rolls<100 && !coach.GetComponentsInChildren<PassPickup>().Any(i=>PassLootCatalogue.GrandTreasures.Contains(i.itemId)));
            Debug.Log("CABINET PHOTO: new-game rolls="+rolls);
#if ENABLE_INPUT_SYSTEM
            Mouse captureMouse=keyboard==null?null:InputSystem.AddDevice<Mouse>();
#endif
            yield return EnterGate(route.hubGate,()=>route.Aboard);
            yield return PhotographCabinet(wood,"Car05ParcelChest");
            yield return PhotographCabinet(coach,"Car04Treasury");
            yield return PlatformExit(5);
            yield return PhotographCabinet(stationWood,"FellbridgeParcelChest");
            yield return PhotographCabinet(parcels,"FellbridgeTreasury");
#if ENABLE_INPUT_SYSTEM
            if(captureMouse!=null)InputSystem.RemoveDevice(captureMouse);
#endif
            Debug.Log("CABINET PHOTO PASS: four working cases reached on foot through five carriages, passenger door and parcels room");
        }

        static IEnumerator PhotographCabinet(PassLootContainer box,string title)
        {
            bool aboard=box.transform.IsChildOf(route.consist);
            Transform frame=aboard?route.consist:house;
            float floor=aboard?1.405f:.025f;
            yield return ApproachChestFront(box,frame,floor);
            Check(!walker.IsCrouched,"photograph must show the real transition from standing");
            var active=box.GetComponentsInChildren<PassPickup>().Single();
            int initial=route.CarriedValue;
            if(aboard)StartChestFilm(title);
            yield return PauseForPhoto(.33f);
            yield return Press();
            Check(route.WorkingChest && route.OpenContainer==box,"F failed on "+box.name+" at "+Feet+" "+route.Notice);
            Check(walker.IsCrouched,"F did not lower the player to the physical latch");
            yield return PauseForPhoto(.27f);
            for(int i=0;i<box.tapsRequired;i++)
            {
#if ENABLE_INPUT_SYSTEM
                if(keyboard!=null && i%2==1 && i+1<box.tapsRequired)
                {
                    var pointer=Mouse.current;
                    InputSystem.QueueStateEvent(pointer,new MouseState().WithButton(MouseButton.Left));
                    for(int f=0;f<2;f++){Step(Vector3.zero);yield return null;}
                    InputSystem.QueueStateEvent(pointer,new MouseState());
                    for(int f=0;f<2;f++){Step(Vector3.zero);yield return null;}
                }
                else
#endif
                    yield return Press();
                if(i==box.tapsRequired/2)yield return SaveCabinetFrame(title+"Working");
                if(i+1<box.tapsRequired)yield return PauseForPhoto(.19f);
            }
            Check(route.RevealingChest && !route.InventoryOpen && route.CarriedValue==initial,"last tap collected "+active.itemId);
            while(box.Openness<.995f || box.HandsVisible) {Step(Vector3.zero);yield return null;}
            yield return SaveCabinetFrame(title+"Revealed");
            if(!route.WorkingChest && !route.InventoryOpen)
                Debug.LogWarning($"Lost chest before folio, focused={Application.isFocused}, feet={box.transform.InverseTransformPoint(Feet):F2}, eye={box.transform.InverseTransformPoint(Eye.position):F2}");
            double timeout=Time.realtimeSinceStartupAsDouble+5;
            while(!route.InventoryOpen && Time.realtimeSinceStartupAsDouble<timeout)
            {Step(Vector3.zero);yield return null;}
            Check(route.InventoryOpen && route.SearchCount==1,$"folio failed to reveal {active.itemId}, shots={route.ChestStrokes}, home={route.InHub}, open={route.SearchCount}, bag={route.InventoryOpen}");
            if(aboard){yield return PauseForPhoto(.25f); StopChestFilm();}
            yield return Press();
            Check(route.CarriedValue-initial==PassLootCatalogue.Value(active.itemId),$"value mismatch for {active.itemId}, funds={route.CarriedValue}, displayed count={route.SearchCount}, focus={Application.isFocused}, inventory={route.InventoryOpen}, working={route.WorkingChest}, range={Vector3.Distance(Eye.position,box.transform.TransformPoint(box.gripLocal)):F2}");
            if(route.SearchOpen)yield return Press(true);
            Check(!walker.CrouchSelected,"leaving the case failed to restore the standing approach");
            for(int i=0;i<15;i++)Step(Vector3.zero);
            Debug.Log("CABINET PHOTO "+box.name+": F, "+box.tapsRequired+" strokes, actual "+active.itemId+"; held="+route.CarriedValue);
        }

        static IEnumerator PauseForPhoto(float seconds)
        {
            double stop=Time.realtimeSinceStartupAsDouble+seconds;
            while(Time.realtimeSinceStartupAsDouble<stop){Step(Vector3.zero);yield return null;}
        }
        static IEnumerator SaveCabinetFrame(string name)
        {
            string dir=FleetImport.Argument("-chestReviewPhotos");
            if(!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
                WriteGameFrame(Path.Combine(dir,name+".png"),true);
            }
            Step(Vector3.zero);yield return null;Step(Vector3.zero);yield return null;
        }
    }
}
