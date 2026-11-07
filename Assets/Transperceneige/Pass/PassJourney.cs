using System;
using System.Collections.Generic;
using UnityEngine;
using Winterbourne.Demo;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Winterbourne.Pass
{
    public enum JourneyPhase { Running, Dwell, Chase, Freezing, Jumping, UnderCanopy, Won, Frozen, Fell, Crashed }

    [DefaultExecutionOrder(-90)]
    public sealed class PassJourney : MonoBehaviour
    {
        public Transform consist, chaseGrip, chaseArrival, escapeLaunch;
        public Car07Walker walker;
        public GameObject canopy;
        public const float NoseDatum = 110.67f, TrainLength = 159.82f, Stop = 1095f;
        public const float PlatformEnd = 1110f, Bridge = 1400f, Break = 2120f;
        public float Distance { get; private set; }
        public float Speed { get; private set; }
        public float StationSeconds { get; private set; }
        public float ColdSeconds { get; private set; }
        public JourneyPhase Phase { get; private set; }
        public bool Aboard { get; private set; } = true;
        public bool Started { get; private set; }
        public bool InventoryOpen { get; private set; }
        public int Lighting { get; private set; }
        public bool ManualClock { get; set; }
        public bool HasParachute => Array.IndexOf(slots,"parachute") >= 0;
        public bool Terminal => Phase==JourneyPhase.Won || Phase==JourneyPhase.Frozen ||
            Phase==JourneyPhase.Crashed || Phase==JourneyPhase.Fell;
        public bool EscapeReady => Distance>=Bridge+TrainLength+4 && Distance<Break &&
            PassAtmosphere.GroundHeight(7,Distance-TrainLength)<-12;
        public float SecondsToBreak => Mathf.Max(0,(Break-Distance)/6);
        public string Notice => Time.unscaledTime<=noticeUntil ? notice : "";
        public string Slot(int index) => slots[index];
        public string ItemName(string id) => id!=null && labels.TryGetValue(id,out string name) ? name : id;
        readonly string[] slots=new string[8];
        readonly Dictionary<string,string> labels=new Dictionary<string,string>();
        PassAtmosphere air;
        PassDoor[] doors;
        PassDoor rear, chase;
        PassPickup[] items;
        Car07SlidingDoor[] sliders;
        CharacterController body;
        AudioSource signal;
        AudioClip whistle, bell;
        Transform[] rollingStock;
        Light gripLamp;
        Vector3[] restPosition;
        Quaternion[] restRotation;
        Checkpoint saved;
        bool beforeStation, beforeBridge, sounded, wreck;
        string notice="";
        float noticeUntil, flightAge, fallRate, wreckAge, boardingAge=-1;
        Vector3 jumpStart,boardingStart;

        sealed class Checkpoint
        {
            public float distance, speed, dwell, cold, pitch;
            public JourneyPhase phase;
            public bool aboard, first, second, sounded;
            public Vector3 position;
            public Quaternion rotation;
            public string[] inventory;
            public bool[] present, target, slideTarget;
            public float[] amount, slideAmount;
        }

        void Start()
        {
            body=walker.GetComponent<CharacterController>();
            doors=consist.GetComponentsInChildren<PassDoor>(true);
            sliders=consist.GetComponentsInChildren<Car07SlidingDoor>(true);
            items=consist.GetComponentsInChildren<PassPickup>(true);
            foreach(var i in items) labels[i.itemId]=i.label;
            foreach(var door in doors)
                if(door.kind==PassDoorKind.Escape) rear=door;
                else if(door.kind==PassDoorKind.Chase) chase=door;
            air=FindFirstObjectByType<PassAtmosphere>();
            if(air) air.train=consist;
            if(chaseGrip) gripLamp=chaseGrip.GetComponentInChildren<Light>(true);
            var vehicles=new List<Transform>();
            foreach(Transform t in consist) if(t!=walker.transform) vehicles.Add(t);
            rollingStock=vehicles.ToArray();
            restPosition=new Vector3[rollingStock.Length];
            restRotation=new Quaternion[rollingStock.Length];
            for(int i=0;i<rollingStock.Length;i++){
                var t=rollingStock[i];
                restPosition[i]=t.localPosition; restRotation[i]=t.localRotation;
            }
            walker.automaticRespawn=false;
            walker.runningSpeed=4.5f;
            walker.useOverride=UseLookedAtDoor;
            walker.transform.SetParent(consist,true);
            if(canopy) canopy.SetActive(false);
            signal=gameObject.AddComponent<AudioSource>();
            signal.playOnAwake=false; signal.spatialBlend=0;
            whistle=PassSignals.Make(false); bell=PassSignals.Make(true);
            SyncTrain();
            SaveCheckpoint();
        }

        void OnDestroy()
        {
            if(walker) walker.useOverride=null;
            if(whistle) Destroy(whistle);
            if(bell) Destroy(bell);
        }

        void Update()
        {
            if(body==null) return;
            if(!Application.isFocused || ManualClock) return;
            if(Key('L')) CycleLighting();
            if(Terminal){ if(Key('R')) Retry(); }
            else {
                if(Key('B') && (Aboard || Phase==JourneyPhase.Dwell || Phase==JourneyPhase.Chase))
                    SetInventory(!InventoryOpen);
                Started |= walker.HasMouse || InventoryOpen;
                if(walker.HasMouse && !InventoryOpen){
                    if(Key('F')) TryPick(LookedAt<PassPickup>());
                    if(Key(' ')) TryJump();
                }
            }
            if(Started) Advance(Time.deltaTime);
        }

        void LateUpdate()
        {
            if(!body) return;
            ReconcileBoarding();
            if(!Terminal && Aboard && walker.transform.position.y < -3.5f) Finish(JourneyPhase.Fell);
            if(gripLamp) gripLamp.enabled=Phase==JourneyPhase.Chase;
            if(air) air.Apply(Lighting,-walker.transform.position.z);
        }

        public void CycleLighting()
        {
            Lighting=(Lighting+1)%3;
            if(air) air.Apply(Lighting,-walker.transform.position.z);
            Tell(new[]{"雪山黄昏","风雪白昼","暴雪深夜"}[Lighting]);
        }

        public void SetInventory(bool visible)
        {
            InventoryOpen=visible && !Terminal;
            walker.Overlay(InventoryOpen || Terminal);
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
            Tell("已收好 · "+item.label);
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
            var door=LookedAt<PassDoor>();
            if(!door) return false;
            TryDoor(door);
            return true;
        }

        public bool TryDoor(PassDoor door)
        {
            if(!door || Terminal || Vector3.Distance(walker.eyeCamera.transform.position,door.threshold.position)>2.6f) return false;
            bool allowed=door.kind==PassDoorKind.Passenger ? Phase==JourneyPhase.Dwell :
                door.kind==PassDoorKind.Escape && EscapeReady && Aboard && Phase==JourneyPhase.Running;
            if(!allowed){Tell(door.kind==PassDoorKind.Escape ? "尾门封闭 · 等候车尾进入峡谷" : "运行中门锁闭"); return false;}
            if(door.TargetOpen && door.Occupied(body)){ Tell("请先离开门槛"); return false; }
            door.SetOpen(!door.TargetOpen);
            return true;
        }

        public void ReconcileBoarding()
        {
            if(boardingAge>=0 || (Phase!=JourneyPhase.Dwell && Phase!=JourneyPhase.Chase)) return;
            float x=walker.transform.position.x;
            if(Aboard && x< -1.77f) Board(false);
            else if(!Aboard && Phase==JourneyPhase.Dwell && x> -1.52f && x<1.45f) Board(true);
        }

        void Board(bool board)
        {
            Aboard=board;
            walker.transform.SetParent(board ? consist : null,true);
            Physics.SyncTransforms();
        }

        public bool TryJump()
        {
            if(Terminal || InventoryOpen) return false;
            if(Phase==JourneyPhase.Chase && !Aboard && Distance-TrainLength<PlatformEnd &&
               Vector3.Distance(walker.transform.position+Vector3.up*.76f,chaseGrip.position)<2.65f){
                boardingStart=consist.InverseTransformPoint(walker.transform.position);
                walker.externalMotion=true;
                body.enabled=false;
                Board(true);
                boardingAge=0;
                Tell("已抓住扶手 · 返回列车");
                return true;
            }
            if(Phase!=JourneyPhase.Running || !Aboard || !EscapeReady || !rear || !rear.IsOpen ||
               Vector3.Distance(walker.transform.position,escapeLaunch.position)>1.65f) return false;
            jumpStart=escapeLaunch.position+Vector3.up*.04f;
            Board(false);
            walker.externalMotion=true;
            body.enabled=false;
            walker.transform.position=jumpStart;
            Phase=JourneyPhase.Jumping; flightAge=0;
            return true;
        }

        public string ActionPrompt()
        {
            if(Terminal || InventoryOpen || !walker || !walker.eyeCamera) return "";
            if(!walker.HasMouse && !walker.externalMotion)
                return Started ? "单击画面继续" : "单击开始行程    ·    WASD 行走";
            var p=walker.transform.position;
            if(!Aboard && Phase==JourneyPhase.Chase && Vector3.Distance(p+Vector3.up*.76f,chaseGrip.position)<2.65f)
                return "空格   抓住车尾扶手";
            if(EscapeReady && rear && rear.IsOpen && Vector3.Distance(p,escapeLaunch.position)<1.65f)
                return HasParachute ? "空格   跃入峡谷 · 伞包已就绪" : "空格   跃入峡谷 · 未携带伞包";
            var item=LookedAt<PassPickup>();
            if(item) return "F   拾取"+item.label;
            var d=LookedAt<PassDoor>();
            if(d && (d.kind==PassDoorKind.Passenger && Phase==JourneyPhase.Dwell ||
                     d.kind==PassDoorKind.Escape && EscapeReady))
                return d.TargetOpen ? "E   关门" : "E   开门";
            if(LookedAt<Car07SlidingDoor>()) return "E   推拉包厢门";
            return "";
        }

        public void Advance(float seconds)
        {
            if(body==null || seconds<=0 || float.IsNaN(seconds) || float.IsInfinity(seconds)) return;
            Started=true;
            while(seconds>.00001f){
                float dt=Mathf.Min(seconds,.10f); seconds-=dt;
                if(Terminal){
                    if(Phase==JourneyPhase.Crashed) WreckStep(dt);
                    else break;
                }else{
                    Drive(dt);
                    if(boardingAge>=0) StepAboard(dt);
                    if(Phase==JourneyPhase.Freezing && (ColdSeconds-=dt)<=0) Finish(JourneyPhase.Frozen);
                    else if(Phase==JourneyPhase.Jumping || Phase==JourneyPhase.UnderCanopy) Fly(dt);
                }
            }
            SyncTrain();
        }

        void StepAboard(float dt)
        {
            boardingAge+=dt;
            float t=Mathf.Clamp01(boardingAge/.54f);
            var landing=consist.InverseTransformPoint(chaseArrival.position);
            walker.transform.localPosition=Vector3.Lerp(boardingStart,landing,Mathf.SmoothStep(0,1,t))+
                Vector3.up*(4*t*(1-t)*.24f);
            if(t>=1){
                walker.Place(consist,landing,walker.transform.localRotation,walker.ViewPitch);
                walker.externalMotion=false; boardingAge=-1;
            }
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
                if(Distance>=Stop){ Speed=0; StationSeconds=60; Phase=JourneyPhase.Dwell; }
                return;
            }
            float target=Distance<PlatformEnd+TrainLength ? 2.75f : 6f;
            Speed=Mathf.MoveTowards(Speed,target,.66f*dt);
            Distance=Mathf.Min(Break,Distance+Speed*dt);
            if(Phase==JourneyPhase.Chase && Distance>=PlatformEnd+TrainLength){
                if(chase) chase.SetOpen(false);
                Phase=Aboard ? JourneyPhase.Running : JourneyPhase.Freezing;
                ColdSeconds=8;
            }
            if(Distance>=1370 && !beforeBridge && Aboard){ beforeBridge=true; SaveCheckpoint(); }
            if(Distance>=Bridge && Distance-Speed*dt<Bridge) Sound(bell);
            if(Distance>=Break){
                wreck=true;
                if(Aboard) Finish(JourneyPhase.Crashed);
            }
        }

        void Depart()
        {
            // Move a straddling passenger onto the nearest safe surface before latching.
            ReconcileBoarding();
            foreach(var d in doors){
                if(d.kind!=PassDoorKind.Passenger) continue;
                if(d.NeedsDepartureClearance(body)){
                    Vector3 pos=walker.transform.position;
                    bool inside=pos.x> -1.64f;
                    pos.x=inside ? -1.11f : -2.90f;
                    pos.y=1.399f;
                    var support=inside ? consist : null;
                    walker.Place(support,inside ? consist.InverseTransformPoint(pos) : pos,
                        walker.transform.localRotation,walker.ViewPitch);
                    Aboard=inside;
                }
                d.SetOpen(false);
            }
            if(chase) chase.SetOpen(true);
            Phase=JourneyPhase.Chase;
        }

        void Fly(float dt)
        {
            float previousHeight=walker.transform.position.y;
            flightAge+=dt;
            if(Phase==JourneyPhase.Jumping){
                var p=jumpStart+new Vector3(7.5f*flightAge,
                    4.8f*flightAge-4.905f*flightAge*flightAge,-2.4f*flightAge);
                walker.transform.position=p;
                if(HasParachute && flightAge>=.92f){
                    Phase=JourneyPhase.UnderCanopy; fallRate=2;
                    if(canopy) canopy.SetActive(true);
                    Tell("伞已展开");
                }
            }else{
                fallRate=Mathf.MoveTowards(fallRate,4.2f,dt*1.5f);
                walker.transform.position+=new Vector3(2.35f,-fallRate,-1.05f)*dt;
            }
            var now=walker.transform.position;
            float floor=PassAtmosphere.GroundHeight(now.x,-now.z);
            // Sample the triangulated snowy bank, whose surface can differ slightly
            // from the continuous terrain profile. The pack opens beyond the parapet.
            if(now.x>4.4f && now.y<floor+4){
                float from=Mathf.Max(previousHeight,floor)+6;
                if(Physics.Raycast(new Vector3(now.x,from,now.z),Vector3.down,out var hit,
                    Mathf.Max(12,from-now.y+3),Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                    floor=hit.point.y;
            }
            if(flightAge>.55f && now.y<=floor+.09f){
                now.y=floor+.09f; walker.transform.position=now;
                Finish(Phase==JourneyPhase.UnderCanopy ? JourneyPhase.Won : JourneyPhase.Fell);
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
                distance=Distance,speed=Speed,dwell=StationSeconds,cold=ColdSeconds,phase=Phase,
                first=beforeStation,second=beforeBridge,sounded=sounded,aboard=Aboard,
                position=Aboard ? walker.transform.localPosition : walker.transform.position,
                rotation=walker.transform.localRotation,pitch=walker.ViewPitch,
                inventory=(string[])slots.Clone(),present=Array.ConvertAll(items,p=>p.gameObject.activeSelf),
                target=Array.ConvertAll(doors,d=>d.TargetOpen),amount=Array.ConvertAll(doors,d=>d.Openness),
                slideTarget=Array.ConvertAll(sliders,d=>d.TargetClosed),slideAmount=Array.ConvertAll(sliders,d=>d.ClosedFraction)
            };
        }

        public void Retry()
        {
            if(saved==null) return;
            var c=saved;
            Distance=c.distance; Speed=c.speed; Phase=c.phase; Aboard=c.aboard;
            beforeStation=c.first; beforeBridge=c.second; sounded=c.sounded;
            StationSeconds=c.dwell; ColdSeconds=c.cold; wreck=false; wreckAge=flightAge=0; boardingAge=-1;
            for(int i=0;i<rollingStock.Length;i++) rollingStock[i].SetLocalPositionAndRotation(restPosition[i],restRotation[i]);
            SyncTrain();
            for(int i=0;i<doors.Length;i++) doors[i].Restore(c.amount[i],c.target[i]);
            for(int i=0;i<sliders.Length;i++) sliders[i].RestorePose(c.slideAmount[i],c.slideTarget[i]);
            for(int i=0;i<items.Length;i++) items[i].gameObject.SetActive(c.present[i]);
            Array.Copy(c.inventory,slots,8);
            if(canopy) canopy.SetActive(false);
            walker.Place(Aboard ? consist : null,c.position,c.rotation,c.pitch);
            walker.externalMotion=false; SetInventory(false);
            Physics.SyncTransforms(); Tell("已回到检查点");
        }

        public void Tell(string text){ notice=text; noticeUntil=Time.unscaledTime+2.4f; }
        void Sound(AudioClip clip){ if(signal && clip) signal.PlayOneShot(clip,.53f); }

        static bool Key(char key)
        {
#if ENABLE_INPUT_SYSTEM
            var k=Keyboard.current;
            if(k==null) return false;
            switch(key){
                case 'B':return k.bKey.wasPressedThisFrame;
                case 'F':return k.fKey.wasPressedThisFrame;
                case 'L':return k.lKey.wasPressedThisFrame;
                case 'R':return k.rKey.wasPressedThisFrame;
                default:return k.spaceKey.wasPressedThisFrame;
            }
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(key==' ' ? KeyCode.Space : (KeyCode)((int)KeyCode.A+key-'A'));
#else
            return false;
#endif
        }
    }
}
