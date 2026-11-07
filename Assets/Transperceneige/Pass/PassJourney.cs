using System;
using System.Collections.Generic;
using UnityEngine;
using Winterbourne.Demo;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Winterbourne.Pass
{
    public enum JourneyPhase { Running, Dwell, Chase, Freezing, Jumping, UnderCanopy, Won, Frozen, Fell, Crashed, TimedOut, TunnelImpact }

    [DefaultExecutionOrder(-90)]
    public sealed partial class PassJourney : MonoBehaviour
    {
        public Transform consist, chaseGrip, chaseArrival, escapeLaunch;
        public Car07Walker walker;
        public GameObject canopy;
        public const float NoseDatum = 110.67f, TrainLength = 159.82f, Stop = 1095f;
        public const float PlatformStart = 920f, PlatformEnd = 1110f, Bridge = 1400f, Break = 2120f;
        public float Distance { get; private set; }
        public float Speed { get; private set; }
        public float StationSeconds { get; private set; }
        public float ColdSeconds { get; private set; }
        public JourneyPhase Phase { get; private set; }
        public bool Aboard { get; private set; } = true;
        public bool Started { get; private set; }
        public bool InventoryOpen { get; private set; }
        public bool ManualClock { get; set; }
        public bool HasParachute => Array.IndexOf(slots,"parachute") >= 0;
        public bool Terminal => Phase==JourneyPhase.TimedOut || Phase==JourneyPhase.Frozen ||
            Phase==JourneyPhase.Crashed || Phase==JourneyPhase.Fell || Phase==JourneyPhase.TunnelImpact;
        public bool EscapeReady => Distance>=Bridge+TrainLength+4 && Distance<Break &&
            PassAtmosphere.GroundHeight(7,Distance-TrainLength)<-12;
        public float SecondsToBreak => Mathf.Max(0,(Break-Distance)/6);
        public string Notice => Time.unscaledTime<=noticeUntil ? PassText.Get(notice)+(noticeItem==null ? "" : ItemName(noticeItem)) : "";
        public string Slot(int index) => slots[index];
        public string ItemName(string id) => id!=null && labels.TryGetValue(id,out string name) ? PassText.Get(name) : id;
        readonly string[] slots=new string[8];
        readonly Dictionary<string,string> labels=new Dictionary<string,string>();
        PassDoor[] doors;
        PassDoor rear, chase;
        PassPickup[] items;
        Car07SlidingDoor[] sliders;
        CharacterController body;
        public CharacterController PlayerBody => body;
        AudioSource signal;
        AudioClip whistle, bell;
        Transform[] rollingStock;
        Light gripLamp;
        Vector3[] restPosition;
        Quaternion[] restRotation;
        Checkpoint saved;
        bool beforeStation, beforeBridge, sounded, wreck;
        string notice="", noticeItem;
        float noticeUntil, flightAge, fallRate, wreckAge, boardingAge=-1;
        Vector3 boardingStart;

        sealed class Checkpoint
        {
            public float distance, speed, dwell, cold, pitch;
            public int health;
            public JourneyPhase phase;
            public bool aboard, first, second, sounded;
            public Vector3 position;
            public Quaternion rotation;
            public string[] inventory;
            public bool[] present, target, slideTarget;
            public float[] amount, slideAmount;
            public float[] coverAmount;
            public bool[] coverTarget;
            public bool crouched;
            public PassRoofLadder ladder;
            public int ladderTarget, ladderDirection;
        }

        void Start()
        {
            body=walker.GetComponent<CharacterController>();
            ladders=consist.GetComponentsInChildren<PassRoofLadder>();
            tunnelZones=FindObjectsByType<PassTunnelClearance>(FindObjectsSortMode.None);
            walker.canStandOverride=RoomToStand;
            walker.canCrouchDuringExternalMotion=() => ClimbingRoofLadder && !Terminal;
            doors=consist.GetComponentsInChildren<PassDoor>(true);
            sliders=consist.GetComponentsInChildren<Car07SlidingDoor>(true);
            items=consist.GetComponentsInChildren<PassPickup>(true);
            foreach(var i in items) labels[i.itemId]=i.label;
            foreach(var door in doors)
                if(door.kind==PassDoorKind.Escape) rear=door;
                else if(door.kind==PassDoorKind.Chase) chase=door;
            var air=FindFirstObjectByType<PassAtmosphere>();
            if(air) air.train=consist;
            if(chaseGrip) gripLamp=chaseGrip.GetComponentInChildren<Light>(true);
            var vehicles=new List<Transform>();
            foreach(Transform t in consist) if(t!=walker.transform && t.name.StartsWith("Car",StringComparison.Ordinal)) vehicles.Add(t);
            rollingStock=vehicles.ToArray();
            restPosition=new Vector3[rollingStock.Length];
            restRotation=new Quaternion[rollingStock.Length];
            for(int i=0;i<rollingStock.Length;i++){
                var t=rollingStock[i];
                restPosition[i]=t.localPosition; restRotation[i]=t.localRotation;
            }
            walker.automaticRespawn=false;
            walker.interactionDistance=2.25f;
            walker.runningSpeed=5.8f;
            walker.jumpOverride=TryJump;
            walker.externalInput=ReadFlightInput;
            walker.groundImpact=ApplyGroundImpact;
            walker.useOverride=UseLookedAtDoor;
            if (sleeperSpawn) PlaceAt(sleeperSpawn, consist);
            else walker.transform.SetParent(consist,true);
            if(canopy) canopy.SetActive(false);
            signal=gameObject.AddComponent<AudioSource>();
            signal.playOnAwake=false; signal.spatialBlend=0;
            whistle=PassSignals.Make(false); bell=PassSignals.Make(true);
            SyncTrain();
            bookcases[0]=hubGate ? hubGate.cover : null;
            bookcases[1]=stationGate ? stationGate.cover : null;
            bookcases[2]=returnGate ? returnGate.cover : null;
            SaveCheckpoint();
            initialRide=saved;
            if(hubSpawn) NewGame();
        }

        void OnDestroy()
        {
            if(walker){ walker.useOverride=null; walker.jumpOverride=null; walker.externalInput=null; walker.groundImpact=null;
                walker.canStandOverride=null; walker.canCrouchDuringExternalMotion=null; }
            if(whistle) Destroy(whistle);
            if(bell) Destroy(bell);
        }

        void Update()
        {
            if(body==null) return;
            TickExpedition();
            if(!Application.isFocused || ManualClock) return;
            if(InStartMenu) return;
            if(Terminal){ if(Key('R')) Retry(); }
            else {
                if(Key('B') && Phase!=JourneyPhase.Jumping && Phase!=JourneyPhase.UnderCanopy)
                    SetInventory(!InventoryOpen);
                if(!InHub && !AtLanding) Started |= walker.HasMouse || InventoryOpen;
                if(walker.HasMouse && !InventoryOpen){
                    if(Key('F')) TryPick(LookedAt<PassPickup>());
                }
            }
            if(Started) Advance(Time.deltaTime);
        }

        void LateUpdate()
        {
            if(!body || InStartMenu) return;
            PollPortals();
            ReconcileBoarding();
            CheckTunnelImpact();
            if(gripLamp) gripLamp.enabled=Phase==JourneyPhase.Chase;
        }

        public void SetInventory(bool visible)
        {
            InventoryOpen=visible && !Terminal && !InStartMenu;
            walker.Overlay(InventoryOpen || Terminal || InStartMenu);
        }

        public bool MoveSlot(int from,int to)
        {
            if(!InventoryOpen || from<0 || from>=8 || to<0 || to>=8 || slots[from]==null) return false;
            (slots[from],slots[to])=(slots[to],slots[from]);
            return true;
        }

        public bool TryPick(PassPickup item)
        {
            if(!item || !item.gameObject.activeInHierarchy || Terminal || !Aboard ||
                Vector3.Distance(walker.eyeCamera.transform.position,item.transform.position)>2.35f) return false;
            int index=Array.IndexOf(slots,null);
            if(index<0){ Tell("背包已满"); return false; }
            slots[index]=item.itemId;
            labels[item.itemId]=item.label;
            item.gameObject.SetActive(false);
            Tell("已收好 · ");
            noticeItem=item.itemId;
            return true;
        }

        T LookedAt<T>() where T: Component
        {
            var eye=walker.eyeCamera.transform;
            return Physics.Raycast(eye.position,eye.forward,out var hit,2.25f,Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Collide) ? hit.collider.GetComponentInParent<T>() : null;
        }

        bool UseLookedAtDoor()
        {
            if (TryTargetLadder(out var ladder,out bool descend))
            {
                StartLadder(ladder,descend);
                return true;
            }
            if (!Terminal && !InventoryOpen && !InStartMenu && TryLookAtCover(out var cover))
            {
                cover.Toggle();
                return true;
            }
            if (!Terminal && !InventoryOpen && TryLookAtDoor(out var door, out var slider))
            {
                if (door) TryDoor(door);
                else slider.Toggle(body);
            }
            // The journey owns both types of door, including an empty or obstructed aim.
            return true;
        }

        bool TryLookAtDoor(out PassDoor door, out Car07SlidingDoor slider)
        {
            return PassDoorTarget.Resolve(walker.eyeCamera, walker.interactionDistance, out door, out slider);
        }

        public bool DoorAvailable(PassDoor door)
        {
            if (!door || Terminal || Phase == JourneyPhase.Jumping || Phase == JourneyPhase.UnderCanopy) return false;
            if (door.Interior || door.kind==PassDoorKind.Cottage) return true;
            if (door.kind == PassDoorKind.Escape) return EscapeReady && Aboard && Phase == JourneyPhase.Running;
            if (Phase == JourneyPhase.Dwell) return true;
            if (Phase != JourneyPhase.Chase || door.ExteriorSide != -1) return false;
            // Compute from chainage so accelerated simulation and normal frames agree,
            // including between the physics transform updates of a multi-step advance.
            float chainage = Distance - NoseDatum - (door.threshold.position.z - consist.position.z);
            return chainage >= PlatformStart && chainage < PlatformEnd;
        }

        public bool TryDoor(PassDoor door)
        {
            if (!door || Terminal || InventoryOpen || InStartMenu || ((InHub || AtLanding) && door.kind!=PassDoorKind.Cottage)) return false;
            if (!DoorAvailable(door))
            {
                Tell(door.kind == PassDoorKind.Escape ? "车尾进入峡谷后解锁" : "车门已锁闭");
                return false;
            }
            if (door.TargetOpen && door.Occupied(body)) { Tell("请先离开门槛"); return false; }
            door.SetOpen(!door.TargetOpen);
            return true;
        }

        void Board(bool board)
        {
            Aboard=board;
            walker.transform.SetParent(board ? consist : null,true);
            Physics.SyncTransforms();
        }

        public string ActionPrompt()
        {
            if (Terminal || InventoryOpen || InStartMenu || !walker || !walker.eyeCamera) return "";
            if (ClimbingRoofLadder) return PassText.Get("roof.climbing");
            if (walker.HasMouse && TryTargetLadder(out _,out bool descend))
                return PassText.Get(descend ? "roof.down" : "roof.up");
            if(walker.HasMouse && TryLookAtCover(out var cover))
                return PassText.Get(cover.TargetClosed ? "E   开门" : "E   关门");
            if (TryCottageDoorPrompt(out string entryPrompt)) return entryPrompt;
            if (InHub || AtLanding)
            {
                if (!walker.HasMouse) return PassText.Get("hub.click");
                return PassText.Get(AtLanding ? "landing.return" : MissedTrainInHub ? "hub.missed" : VisitingStation ? "hub.station" : "hub.depart");
            }
            if (!walker.HasMouse && !walker.externalMotion)
                return PassText.Get(Started ? "单击画面继续" : "单击开始行程");
            if (!Aboard && CanReturnToStation && stationGate &&
                (stationGate.transform.position-walker.transform.position).sqrMagnitude<13f)
                return PassText.Get("station.hub");
            var item = LookedAt<PassPickup>();
            if (item) return PassText.Get("F   拾取") + PassText.Get(item.label);
            if (TryLookAtDoor(out var door, out var slider))
            {
                if (slider) return PassText.Get(slider.TargetClosed ? "E   开门" : "E   关门");
                if (!DoorAvailable(door)) return PassText.Get(door.kind == PassDoorKind.Escape ? "车尾进入峡谷后解锁" : "车门已锁闭");
                if (!door.TargetOpen) return PassText.Get("E   开门");
                string action = MovementPrompt();
                return string.IsNullOrEmpty(action) ? PassText.Get("E   关门") : action + "    ·    " + PassText.Get("E   关门");
            }
            return MovementPrompt();
        }

        public void Advance(float seconds)
        {
            if(body==null || InStartMenu || AtLanding || (InHub && !VisitingStation) || seconds<=0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            Started=true;
            while(seconds>.00001f && !AtLanding && (!InHub || VisitingStation)){
                float dt=Mathf.Min(seconds,Airborne ? 1f/40f : .10f); seconds-=dt;
                if(Terminal){
                    if(Phase==JourneyPhase.Crashed) WreckStep(dt);
                    else break;
                }else{
                    Drive(dt);
                    if (ClimbingRoofLadder && !Terminal) StepLadder(dt);
                    CheckTunnelImpact();
                    if(boardingAge>=0) StepAboard(dt);
                    if(Phase==JourneyPhase.Freezing && (ColdSeconds-=dt)<=0) Finish(JourneyPhase.Frozen);
                    else if(Phase==JourneyPhase.Jumping || Phase==JourneyPhase.UnderCanopy) Fly(dt);
                }
            }
            SyncTrain();
            if (Distance >= Stop && Phase != JourneyPhase.Dwell) CloseDepartedDoors();
        }

        void Drive(float dt)
        {
            if(wreck){ WreckStep(dt); return; }
            if(Phase==JourneyPhase.Dwell){
                StationSeconds=Mathf.Max(0,StationSeconds-dt);
                if(StationSeconds<=15 && !sounded){ sounded=true; Sound(whistle); }
                if(StationSeconds<=0) Depart();
                return;
            }
            if(Distance<Stop){
                Speed=Mathf.Min(13f,Mathf.Sqrt(Mathf.Max(.64f,2*.72f*(Stop-Distance))));
                Distance=Mathf.Min(Stop,Distance+Speed*dt);
                if(Distance>=880 && !beforeStation){ beforeStation=true; SaveCheckpoint(); }
                if(Distance>=Stop){ Speed=0; StationSeconds=120; Phase=JourneyPhase.Dwell; }
                return;
            }
            float target=Distance<PlatformEnd+TrainLength ? 2.75f : 6f;
            Speed=Mathf.MoveTowards(Speed,target,.66f*dt);
            Distance=Mathf.Min(Break,Distance+Speed*dt);
            if(Phase==JourneyPhase.Chase && Distance>=PlatformEnd+TrainLength){

                if(VisitingStation)
                {
                    MissedTrainInHub=true;
                    hubGate.RefreshLabel();
                    Tell("hub.missed");
                }
                Phase=Aboard || InHub ? JourneyPhase.Running : JourneyPhase.Freezing;
                ColdSeconds=8;
            }
            if(Distance>=1370 && !beforeBridge && Aboard){ beforeBridge=true; SaveCheckpoint(); }
            if(Distance>=Bridge && Distance-Speed*dt<Bridge) Sound(bell);
            if(Distance>=Break){
                wreck=true;
                if(Aboard && !TryGorgeFall()) Finish(JourneyPhase.Crashed);
            }
        }

        void Depart()
        {
            ReconcileBoarding();
            Phase = JourneyPhase.Chase;
        }

        void CloseDepartedDoors()
        {
            foreach (var door in doors)
            {
                if (door.ExteriorSide == 0 || !door.TargetOpen || DoorAvailable(door)) continue;
                // The leaf waits for an occupied threshold or swing arc to clear.
                door.SetOpen(false);
            }
        }

        void WreckStep(float dt)
        {
            wreckAge=Mathf.Min(31,wreckAge+dt);
            // Each unit pitches when its forward end reaches the missing bridge span.
            for(int i=0;i<rollingStock.Length;i++){
                float behind=NoseDatum+restPosition[i].z;
                float half=behind<8 ? 6.9f : behind<21 ? 4.2f : 9.83f;
                float t=Mathf.Max(0,wreckAge-Mathf.Max(0,behind-half)/6);
                float sink=Mathf.Min(80,3.8f*t*t);
                rollingStock[i].localPosition=restPosition[i]+Vector3.down*sink;
                rollingStock[i].localRotation=Quaternion.Euler(-Mathf.Min(63,t*17),0,Mathf.Min(12,t*3))*restRotation[i];
            }
        }

        void SyncTrain()
        {
            consist.position=new Vector3(0,0,NoseDatum-Distance-(wreck ? 6*wreckAge : 0));
            Physics.SyncTransforms();
        }

        void Finish(JourneyPhase end)
        {
            LockExpedition(end);
            Phase=end; Speed=0; InventoryOpen=false;
            walker.externalMotion=true; walker.Overlay(true);
            if(end==JourneyPhase.Crashed && Aboard){
                Transform nearest=null;
                float best=float.MaxValue;
                foreach(var t in rollingStock){
                    float d=Mathf.Abs(walker.transform.position.z-t.position.z);
                    if(d<best){best=d;nearest=t;}
                }
                if(nearest) walker.transform.SetParent(nearest,true);
            }
            body.enabled=false;
        }

        void SaveCheckpoint()
        {
            saved=new Checkpoint{
                crouched=walker.CrouchSelected,ladder=activeLadder,ladderTarget=ladderTarget,ladderDirection=ladderDirection,
                distance=Distance,speed=Speed,dwell=StationSeconds,cold=ColdSeconds,phase=Phase,health=Health,
                first=beforeStation,second=beforeBridge,sounded=sounded,aboard=Aboard,
                position=Aboard ? walker.transform.localPosition : walker.transform.position,
                rotation=walker.transform.localRotation,pitch=walker.ViewPitch,
                inventory=(string[])slots.Clone(),present=Array.ConvertAll(items,p=>p.gameObject.activeSelf),
                target=Array.ConvertAll(doors,d=>d.TargetOpen),amount=Array.ConvertAll(doors,d=>d.Openness),
                slideTarget=Array.ConvertAll(sliders,d=>d.TargetClosed),slideAmount=Array.ConvertAll(sliders,d=>d.ClosedFraction),
                coverTarget=Array.ConvertAll(bookcases,d=>d && d.TargetClosed),
                coverAmount=Array.ConvertAll(bookcases,d=>d ? d.ClosedFraction : 0)
            };
        }

        public void Retry()
        {
            if(saved==null || (InHub && !Terminal) || InStartMenu || Phase==JourneyPhase.TimedOut) return;
            bool initialHut=InHub && saved==initialRide;
            RestoreRide(saved);
            if(initialHut){ Started=false; EnterHut(false); }
            ResumeExpedition();
            Tell("已回到检查点");
        }

        void RestoreRide(Checkpoint c)
        {
            location=JourneyLocation.Railway;
            stationVisit=false; MissedTrainInHub=false;
            if(returnGate) returnGate.gameObject.SetActive(false);
            ResetFlight(); Health=c.health; LastImpactDamage=0;
            Distance=c.distance; Speed=c.speed; Phase=c.phase; Aboard=c.aboard;
            beforeStation=c.first; beforeBridge=c.second; sounded=c.sounded;
            StationSeconds=c.dwell; ColdSeconds=c.cold; wreck=false; wreckAge=0; boardingAge=-1;
            for(int i=0;i<rollingStock.Length;i++) rollingStock[i].SetLocalPositionAndRotation(restPosition[i],restRotation[i]);
            SyncTrain();
            for(int i=0;i<doors.Length;i++) doors[i].Restore(c.amount[i],c.target[i]);
            for(int i=0;i<sliders.Length;i++) sliders[i].RestorePose(c.slideAmount[i],c.slideTarget[i]);
            for(int i=0;i<bookcases.Length;i++) if(bookcases[i]) bookcases[i].Restore(c.coverAmount[i],c.coverTarget[i]);
            for(int i=0;i<items.Length;i++) items[i].gameObject.SetActive(c.present[i]);
            Array.Copy(c.inventory,slots,8);
            if(canopy) canopy.SetActive(false);
            walker.Place(Aboard ? consist : null,c.position,c.rotation,c.pitch);
            ResetLocomotion();
            walker.externalMotion=false;
            walker.SetCrouched(c.crouched);
            activeLadder=c.ladder; ladderTarget=c.ladderTarget; ladderDirection=c.ladderDirection;
            walker.externalMotion=ClimbingRoofLadder;
            SetInventory(false);
            Physics.SyncTransforms();
        }

        public void Tell(string text){ notice=text; noticeItem=null; noticeUntil=Time.unscaledTime+2.4f; }
        void Sound(AudioClip clip){ if(signal && clip) signal.PlayOneShot(clip,.53f); }

        static bool Key(char key)
        {
#if ENABLE_INPUT_SYSTEM
            var k=Keyboard.current;
            if(k==null) return false;
            switch(key){
                case 'B':return k.bKey.wasPressedThisFrame || k.tabKey.wasPressedThisFrame;
                case 'F':return k.fKey.wasPressedThisFrame;
                case 'R':return k.rKey.wasPressedThisFrame;
                default:return false;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown((KeyCode)((int)KeyCode.A+key-'A')) || (key=='B' && Input.GetKeyDown(KeyCode.Tab));
#else
            return false;
#endif
        }
    }
}
