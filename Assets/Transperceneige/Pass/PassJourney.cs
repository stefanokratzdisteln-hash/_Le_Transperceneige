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
        PassDetectivePlayer detectiveView;
        public bool ViewingCharacter=>detectiveView && detectiveView.ViewingCharacter;
        public GameObject canopy;
        public float NoseDatum { get; private set; }
        public float TrainLength { get; private set; }
        public const float Stop = 1095f;
        public const float PlatformStart = 850f, PlatformEnd = 1110f, Bridge = 1400f, Break = 2120f;
        public const float PlatformEdge = -2.53f;
        public float Distance { get; private set; }
        public float Speed { get; private set; }
        public float StationSeconds { get; private set; }
        public float ColdSeconds { get; private set; }
        public JourneyPhase Phase { get; private set; }
        public bool Aboard { get; private set; } = true;
        public bool Started { get; private set; }
        public bool InventoryOpen { get; private set; }
        public bool ManualClock { get; set; }
        public bool Terminal => Phase==JourneyPhase.TimedOut || Phase==JourneyPhase.Frozen ||
            Phase==JourneyPhase.Crashed || Phase==JourneyPhase.Fell || Phase==JourneyPhase.TunnelImpact;
        public bool EscapeReady => Distance>=Bridge+TrainLength+4 && Distance<Break &&
            PassAtmosphere.GroundHeight(7,Distance-TrainLength)<-12;
        public float SecondsToBreak => Mathf.Max(0,(Break-Distance)/6);
        public string Notice => Time.unscaledTime<=noticeUntil ? PassText.Get(notice)+(noticeItem==null ? "" : ItemName(noticeItem)) : "";
        PassDoor[] doors;
        PassDoor rear, chase;
        Car07SlidingDoor[] sliders;
        CharacterController body;
        public CharacterController PlayerBody => body;
        AudioSource signal;
        AudioClip whistle, bell;
        Transform[] rollingStock;
        Light gripLamp;
        Vector3[] restPosition;
        Quaternion[] restRotation;
        float[] frontBehindNose;
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
            public LootSnapshot loot;
            public bool[] target, slideTarget;
            public float[] amount, slideAmount;
            public float[] coverAmount;
            public bool[] coverTarget;
            public bool crouched;
            public PassRoofLadder ladder;
            public int ladderTarget, ladderDirection;
        }

        void Start()
        {
            detectiveView=walker?walker.GetComponent<PassDetectivePlayer>():null;
            body=walker.GetComponent<CharacterController>();
            ladders=consist.GetComponentsInChildren<PassRoofLadder>();
            tunnelZones=FindObjectsByType<PassTunnelClearance>(FindObjectsSortMode.None);
            walker.canStandOverride=RoomToStand;
            walker.canCrouchDuringExternalMotion=() => ClimbingRoofLadder && !Terminal;
            doors=consist.GetComponentsInChildren<PassDoor>(true);
            sliders=consist.GetComponentsInChildren<Car07SlidingDoor>(true);
            RegisterLoot();
            foreach(var door in doors)
                if(door.kind==PassDoorKind.Escape) rear=door;
                else if(door.kind==PassDoorKind.Chase) chase=door;
            var air=FindFirstObjectByType<PassAtmosphere>();
            if(air) air.train=consist;
            if(chaseGrip) gripLamp=chaseGrip.GetComponentInChildren<Light>(true);
            var vehicles=new List<Transform>();
            foreach(Transform t in consist) if(t!=walker.transform && t.name.StartsWith("Car",StringComparison.Ordinal)) vehicles.Add(t);
            rollingStock=vehicles.ToArray();
            frontBehindNose=new float[rollingStock.Length];
            restPosition=new Vector3[rollingStock.Length];
            restRotation=new Quaternion[rollingStock.Length];
            float frontmost=float.PositiveInfinity, rearmost=float.NegativeInfinity;
            for(int i=0;i<rollingStock.Length;i++){
                var t=rollingStock[i];
                restPosition[i]=t.localPosition; restRotation[i]=t.localRotation;
                float front=CouplerZ(t,"Socket_A"), rearEnd=CouplerZ(t,"Socket_B");
                frontBehindNose[i]=front;
                frontmost=Mathf.Min(frontmost,front);
                rearmost=Mathf.Max(rearmost,rearEnd);
            }
            NoseDatum=-frontmost;
            TrainLength=rearmost-frontmost;
            for(int i=0;i<frontBehindNose.Length;i++) frontBehindNose[i]-=frontmost;
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

        float CouplerZ(Transform car,string name)
        {
            foreach(var part in car.GetComponentsInChildren<Transform>(true))
                if(part.name==name) return consist.InverseTransformPoint(part.position).z;
            throw new InvalidOperationException("Missing "+name+" on "+car.name);
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
            if(!Application.isFocused) return;
            if(InStartMenu) return;
            if(Terminal){ if(Key('R')) Retry(); }
            else {
                if(WorkingChest)
                {
                    if(Key((char)27) || Key('B') || !CabinetStillInReach()) CloseSearch();
                    else if(Key('F') || ChestClick()) TapFocusedChest();
                }
                else if(InventoryOpen && Key((char)27)) SetInventory(false);
                else if(Key('B') && !Airborne) SetInventory(!InventoryOpen);
                else if(SearchOpen)
                {
                    if(!ContactValid()) CloseSearch();
                    else if(Key('F')) UseFocused();
                    else if(Key('H')) TakeAllFocused();
                    else if(Key(']')) SelectSearchItem(SelectedSearchIndex+1);
                    else if(Key('[')) SelectSearchItem(SelectedSearchIndex-1);
                }
                else if(walker.HasMouse && !InventoryOpen && Key('F')) UseFocused();
                if(!InHub && !AtLanding) Started |= walker.HasMouse || InventoryOpen;
                if(!InventoryOpen && walker.HasMouse)
                    for(int i=0;i<4;i++) if(Key((char)('1'+i))) ChooseQuickSlot(i);
            }
            if(Started && !ManualClock) Advance(Time.deltaTime);
        }

        void LateUpdate()
        {
            if(!body || InStartMenu) return;
            // Test F against the pin shown on the previous frame. A slow frame
            // can then neither skip the green band nor move the pin mid-press.
            if(PickingLock) lockTravel+=.78f*Mathf.Min(Time.unscaledDeltaTime,.065f);
            AdvanceChestReveal();
            PollPortals();
            ReconcileBoarding();
            CheckTunnelImpact();
            if(gripLamp) gripLamp.enabled=Phase==JourneyPhase.Chase;
        }

        public void SetInventory(bool visible)
        {
            bool display=visible && !Terminal && !InStartMenu;
            bool wasOpen=InventoryOpen;
            if(display && !wasOpen) restoreLook=walker.HasMouse;
            if(!display) CancelSearch();
            InventoryOpen=display;
            walker.Overlay(display || Terminal || InStartMenu);
            if(wasOpen && !display && restoreLook && !Terminal && !InStartMenu) walker.ResumeLook();
            if(!display) restoreLook=false;
            LootRevision++;
        }

        bool UseLookedAtDoor()
        {
            TryUsePassage();
            // The journey owns E, including an empty or obstructed aim.
            return true;
        }

        bool TryUsePassage()
        {
            if(ViewingCharacter)return false;
            if(WorkingChest)return false;
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
                if (door) return TryDoor(door);
                slider.Toggle(body);
                return true;
            }
            return false;
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
            if(ViewingCharacter)return string.Empty;
            if (Terminal || InventoryOpen || InStartMenu || !walker || !walker.eyeCamera) return "";
            if (ClimbingRoofLadder) return PassText.Get("roof.climbing");
            if (walker.HasMouse && LootPrompt(out string loot)) return loot;
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
                float cruise=Distance<480f ? 6.5f : Distance<780f ? 4f : 5f;
                Speed=Mathf.Min(cruise,Mathf.Sqrt(Mathf.Max(.64f,2*.72f*(Stop-Distance))));
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
                float t=Mathf.Max(0,wreckAge-frontBehindNose[i]/6);
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
            CancelSearch();
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
                loot=SaveLoot(),
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
            RestoreLoot(c.loot);
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

        static bool ChestClick()
        {
#if ENABLE_INPUT_SYSTEM
            return Mouse.current!=null && Mouse.current.leftButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(0);
#else
            return false;
#endif
        }

        static bool Key(char key)
        {
#if ENABLE_INPUT_SYSTEM
            var k=Keyboard.current;
            if(k==null) return false;
            switch(key){
                case 'B':return k.bKey.wasPressedThisFrame || k.tabKey.wasPressedThisFrame;
                case 'F':return k.fKey.wasPressedThisFrame;
                case 'R':return k.rKey.wasPressedThisFrame;
                case 'H':return k.hKey.wasPressedThisFrame;
                case (char)27:return k.escapeKey.wasPressedThisFrame;
                case '[':return k.upArrowKey.wasPressedThisFrame;
                case ']':return k.downArrowKey.wasPressedThisFrame;
                case '1':return k.digit1Key.wasPressedThisFrame;
                case '2':return k.digit2Key.wasPressedThisFrame;
                case '3':return k.digit3Key.wasPressedThisFrame;
                case '4':return k.digit4Key.wasPressedThisFrame;
                default:return false;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            if(key==(char)27) return Input.GetKeyDown(KeyCode.Escape);
            if(key=='[') return Input.GetKeyDown(KeyCode.UpArrow);
            if(key==']') return Input.GetKeyDown(KeyCode.DownArrow);
            if(key>='1' && key<='4') return Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1+key-'1'));
            return Input.GetKeyDown((KeyCode)((int)KeyCode.A+key-'A')) || (key=='B' && Input.GetKeyDown(KeyCode.Tab));
#else
            return false;
#endif
        }
    }
}
