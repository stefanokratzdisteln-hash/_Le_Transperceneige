using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Winterbourne.Pass;
using Winterbourne.Demo;
using static Winterbourne.Editor.StationFabrication;

namespace Winterbourne.Editor
{
    public static partial class PassFittings
    {
        const float DoorZ=8.04f;
        const float ThresholdY=2.42f;
        // An open side leaf parks along the coach flank, clear of a runner coming
        // alongside it. The approach remains blocked while the leaf sweeps through it.
        const float SideOpeningAngle=166f;
        public const float PlatformFirst=920f,PlatformLast=1110f,PlatformTop=1.375f;

        sealed class Finishes
        {
            public readonly Material masonry=Paint("Fellbridge masonry",new Color(.41f,.426f,.438f));
            public readonly Material stone=Paint("Flagstone",new Color(.51f,.519f,.516f));
            public readonly Material ash=Paint("Ashlar joints",new Color(.295f,.31f,.327f));
            public readonly Material pine=Paint("Painted iron",new Color(.112f,.205f,.181f),.24f,.42f);
            public readonly Material soot=Paint("Slate canopy",new Color(.17f,.209f,.23f),.26f,.35f);
            public readonly Material wood=Paint("Waiting room oak",new Color(.388f,.259f,.17f),.08f);
            public readonly Material cream=Paint("Booking office cream",new Color(.804f,.773f,.667f));
            public readonly Material snow=Paint("Platform snow",new Color(.84f,.885f,.91f),0,.16f);
            public readonly Material bronze=Paint("Ticket brass",new Color(.71f,.50f,.26f),.63f,.59f);
            public readonly Material lamp=Paint("Lantern glass",new Color(1f,.72f,.35f),0,.33f,2.8f);
            public readonly Material canvas=Paint("Olive canvas",new Color(.36f,.38f,.276f),0,.22f);
            public readonly Material khaki=Paint("Webbing",new Color(.578f,.515f,.364f),0,.21f);
            public readonly Material oxblood=Paint("Ripcord markings",new Color(.50f,.18f,.123f));
            public readonly Material silk=Paint("Unbleached silk",new Color(.84f,.78f,.603f),0,.23f,0,true);
            public readonly Material screen;

            public Finishes()
            {
                screen=Paint("Station enamel",Color.white,.10f,.45f);
                string path=StationFabrication.Folder+"/FellbridgeEnamel.png";
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                if(!texture)throw new InvalidOperationException("Station lettering missing: "+path);
                screen.SetTexture("_BaseMap",texture);
                EditorUtility.SetDirty(screen);
            }
        }

        static Transform Find(Transform parent,string name)
        {
            var all=parent.GetComponentsInChildren<Transform>(true).Where(t=>t.name==name).ToArray();
            if(all.Length!=1)throw new InvalidOperationException($"Expected one {name} under {parent.name}, found {all.Length}");
            return all[0];
        }

        static BoxCollider Box(Transform parent,string name,Vector3 centre,Vector3 size)
        {
            return AddBox(Child(parent,name,centre).transform,size);
        }

        static BoxCollider AddBox(Transform t,Vector3 size)
        {
            var box=t.gameObject.AddComponent<BoxCollider>();
            box.size=size;
            return box;
        }

        static void Recorded(UnityEngine.Object obj)
        {
            if(PrefabUtility.IsPartOfPrefabInstance(obj)) PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
        }

        public static void Build(Transform consist,GameObject[] cars)
        {
            if(!consist||cars==null||cars.Length!=9)throw new ArgumentException("Need a nine-unit formation.");
            var mat=new Finishes();
            MakeStation(mat);
            MakeGangways(cars, mat);
            for(int n=3;n<=9;n++)
            {
                var car=cars[n-1].transform;
                var hardware=Child(car,"Pass entrance fittings",Vector3.zero).transform;
                bool loading=n==3 || n==9;
                for(int side=-1;side<=1;side+=2)
                {
                    CutSideWall(car,hardware,side,loading);
                    for(int end=-1;end<=1;end+=2)
                    {
                        var kind=loading || (n==6 && end<0) ? PassDoorKind.Service : PassDoorKind.Passenger;
                        if(n==9 && side==-1 && end==1) kind=PassDoorKind.Chase;
                        SideDoor(car,hardware,side,end,kind,mat);
                        if(loading) CargoDoor(car,hardware,side,end,mat);
                    }
                }
                for(int end=-1;end<=1;end+=2)
                    EndDoor(car,hardware,end,n==9 && end==1,mat);
                foreach(var slider in car.GetComponentsInChildren<Car07SlidingDoor>())
                {
                    Vector3 travel=car.InverseTransformVector(slider.transform.parent.TransformVector(slider.closedOffset));
                    var centre=car.InverseTransformPoint(slider.transform.position)+travel;
                    centre.x=n==7?.31f:.368f;
                    centre.y=2.44f;
                    centre.z-=Mathf.Sign(travel.z)*.47f;
                    Grip(car,hardware,centre,Quaternion.identity,null,slider,mat);
                }
                if(n==6) GalleyDoor(car,hardware,mat);
                if(n==7) LavatoryDoor(car,hardware,mat);
                if(n==9) Rescue(car,hardware,mat);
            }
            AssetDatabase.SaveAssets();
        }

        static string Flank(int side) => side<0?"Starboard":"Port";

        static void CutSideWall(Transform car,Transform parent,int side,bool loading)
        {
            string flank=Flank(side);
            var old=Find(car,"Side "+flank).GetComponent<BoxCollider>();
            old.enabled=false;
            Recorded(old);
            float x=side*1.473f;
            void Segment(float a,float b) => Box(parent,flank+" wall",new Vector3(x,2.48f,(a+b)/2),
                new Vector3(.082f,2.21f,b-a));
            Segment(-9f,-DoorZ-.502f);
            if(loading) {
                Segment(-DoorZ+.502f,-1.47f);
                Segment(1.47f,DoorZ-.502f);
                Box(parent,"Loading lintel "+flank,new Vector3(x,3.55f,0),new Vector3(.12f,.072f,2.94f));
            } else Segment(-DoorZ+.502f,DoorZ-.502f);
            Segment(DoorZ+.502f,9f);
            for(int end=-1;end<=1;end+=2)
                Box(parent,"Door header "+flank+end,new Vector3(x,3.569f,end*DoorZ),new Vector3(.082f,.035f,1.004f));
        }

        static BoxCollider Panel(Transform car,Transform leaf,string name,Vector3 centre,Vector3 size)
        {
            var panel=Box(leaf,name,Vector3.zero,size);
            panel.transform.SetPositionAndRotation(car.TransformPoint(centre),car.rotation);
            return panel;
        }

        static Quaternion Turn(Transform car,Transform leaf,float degrees)
            => Quaternion.Inverse(leaf.parent.rotation)*Quaternion.AngleAxis(degrees,car.up)*leaf.rotation;

        static void Grip(Transform car,Transform hardware,Vector3 centre,Quaternion angle,PassDoor hinge,Car07SlidingDoor slider,Finishes mat)
        {
            var grip=Child(hardware,"Jamb pull "+(hinge?hinge.name:slider.name),centre);
            grip.transform.localRotation=angle;
            var fitting=new StationFabrication();
            for(int side=-1;side<=1;side+=2)
            {
                fitting.Box(mat.bronze,new Vector3(side*.111f,0,0),new Vector3(.014f,.18f,.065f));
                fitting.Ring(mat.bronze,new Vector3(side*.138f,-.049f,0),new Vector3(side*.138f,.049f,0),.010f,.010f);
                for(int end=-1;end<=1;end+=2)
                    fitting.Box(mat.bronze,new Vector3(side*.123f,end*.047f,0),new Vector3(.032f,.018f,.020f));
            }
            fitting.Finish(grip.transform,"Vestibule brass pull");
            var volume=AddBox(grip.transform,new Vector3(.33f,.53f,.20f));
            volume.isTrigger=true;
            grip.layer=2;
            var target=grip.AddComponent<PassDoorTarget>();
            target.hinge=hinge;
            target.slider=slider;
        }

        static void BoardingPlate(Transform parent,float x,float z,float length,Finishes mat)
        {
            var landing=Child(parent,"Level boarding plate",new Vector3(x,1.355f,z)).transform;
            var step=new StationFabrication();
            step.Box(mat.soot,Vector3.zero,new Vector3(.43f,.045f,length));
            for(int i=-4;i<=4;i++)
                step.Box(mat.bronze,new Vector3(0,.024f,length*.098f*i),new Vector3(.40f,.004f,.009f));
            step.Finish(landing,length>1?"Loading threshold":"Brass boarding threshold");
            AddBox(landing,new Vector3(.43f,.045f,length));
        }

        static void SideDoor(Transform car,Transform hardware,int side,int end,PassDoorKind kind,Finishes mat)
        {
            string name=Flank(side)+(end<0?"_A":"_B");
            var leaf=Find(car,"Door_"+name);
            float x=side*1.475f,z=end*DoorZ;
            var jamb=Child(hardware,"Station threshold "+name,new Vector3(x,ThresholdY,z)).transform;
            var solid=Panel(car,leaf,"Passenger door panel",new Vector3(x,2.468f,z),new Vector3(.074f,2.16f,.975f));
            var door=leaf.gameObject.AddComponent<PassDoor>();
            door.Configure(kind,car,jamb,leaf.localRotation,Turn(car,leaf,side*end*SideOpeningAngle),solid,side);
            Grip(car,hardware,new Vector3(x,2.43f,z+end*.535f),Quaternion.identity,door,null,mat);
            BoardingPlate(hardware,side*1.62f,z,.964f,mat);
            var signAnchor=Child(hardware,"Entrance notice "+name,new Vector3(side*1.411f,3.57f,z)).transform;
            if(side>0)signAnchor.localRotation=Quaternion.Euler(0,180,0);
            var plate=new StationFabrication();
            plate.Box(mat.bronze,Vector3.zero,new Vector3(.013f,.102f,.610f));
            bool service=kind!=PassDoorKind.Passenger;
            plate.Sign(mat.screen,new Vector3(.009f,0,0),.595f,.089f,service?5:4);
            plate.Finish(signAnchor,service?"Guards boarding notice":"Way out notice");
        }

        static void CargoDoor(Transform car,Transform hardware,int side,int end,Finishes mat)
        {
            string name=Flank(side)+(end<0?"_A":"_B");
            var leaf=Find(car,"LoadingDoor_"+name);
            var centre=new Vector3(side*1.555f,2.454f,end*.725f);
            var jamb=Child(hardware,"Loading threshold "+name,centre).transform;
            var solid=Panel(car,leaf,"Loading door panel",centre,new Vector3(.14f,2.125f,1.45f));
            var door=leaf.gameObject.AddComponent<PassDoor>();
            door.Configure(PassDoorKind.Cargo,car,jamb,leaf.localRotation,Turn(car,leaf,-side*end*SideOpeningAngle),solid,side);
            Grip(car,hardware,new Vector3(side*1.52f,2.44f,end*1.49f),Quaternion.identity,door,null,mat);
            if(end==1) BoardingPlate(hardware,side*1.62f,0,2.92f,mat);
        }

        static void EndDoor(Transform car,Transform hardware,int end,bool escape,Finishes mat)
        {
            string tag=end<0?"A":"B";
            var leaf=Find(car,"EndDoor_"+tag);
            var solid=Find(leaf,"Open end leaf "+tag).GetComponent<BoxCollider>();
            // Each leaf parks against its own side of the bellows. This frees both vestibule exits.
            var hinge=car.InverseTransformPoint(leaf.position);
            hinge.x=-end*.68f;
            leaf.position=car.TransformPoint(hinge);
            var open=Turn(car,leaf,180);
            var closed=Turn(car,leaf,-90);
            var jamb=Child(hardware,"End threshold "+tag,new Vector3(-end*.10f,2.425f,end*8.91f)).transform;
            jamb.localRotation=Quaternion.Euler(0,90,0);
            var door=leaf.gameObject.AddComponent<PassDoor>();
            door.Configure(escape?PassDoorKind.Escape:PassDoorKind.Gangway,car,jamb,closed,open,solid);
            door.SetOpen(!escape,true);
            Recorded(leaf);
            Grip(car,hardware,new Vector3(end*.524f,2.43f,end*8.91f),jamb.localRotation,door,null,mat);
            if(escape) return;
            var stile=Child(hardware,"End strike "+tag,new Vector3(end*.577f,2.435f,end*8.913f)).transform;
            var trim=new StationFabrication();
            trim.Box(mat.wood,Vector3.zero,new Vector3(.214f,2.06f,.083f));
            trim.Finish(stile,"Gangway strike stile");
            AddBox(stile,new Vector3(.214f,2.06f,.083f));
        }

        static void GalleyDoor(Transform car,Transform hardware,Finishes mat)
        {
            // The pantry entrance opens from the front vestibule. The separate passenger
            // passage runs along the other side of the kitchen's longitudinal partition.
            const float z=-7.16f, pivotX=.34f, width=.98f;
            var frame=new StationFabrication();
            var jamb=Child(hardware,"Galley pantry threshold",new Vector3(pivotX-width/2,2.43f,z)).transform;
            jamb.localRotation=Quaternion.Euler(0,90,0);
            Vector3 fixedCentre=new Vector3(-1.035f,2.465f,z);
            Vector3 fixedSize=new Vector3(.79f,2.17f,.076f);
            frame.Box(mat.wood,fixedCentre,fixedSize);
            Box(hardware,"Pantry partition",fixedCentre,fixedSize);
            frame.Box(mat.cream,fixedCentre+Vector3.up*.24f,new Vector3(.69f,1.23f,.091f));
            frame.Box(mat.wood,new Vector3(-.15f,3.48f,z),new Vector3(width,.17f,.09f));
            Box(hardware,"Pantry lintel",new Vector3(-.15f,3.48f,z),new Vector3(width,.17f,.09f));
            frame.Finish(hardware,"Pantry partition and transom");
            var leaf=Child(hardware,"Galley pantry door",new Vector3(pivotX,1.39f,z)).transform;
            var panel=new StationFabrication();
            panel.Box(mat.wood,new Vector3(-width/2,.58f,0),new Vector3(width,1.16f,.054f));
            panel.Box(mat.cream,new Vector3(-width/2,.56f,0),new Vector3(width-.14f,.87f,.060f));
            panel.Box(mat.wood,new Vector3(-width/2,1.93f,0),new Vector3(width,.15f,.054f));
            foreach (float x in new[]{-.055f,-width+.055f})
                panel.Box(mat.wood,new Vector3(x,1.51f,0),new Vector3(.11f,.71f,.054f));
            var glass=AssetDatabase.LoadAssetAtPath<Material>("Assets/Transperceneige/Car07/Car07_CabinGlass.mat");
            panel.Box(glass,new Vector3(-width/2,1.51f,0),new Vector3(width-.22f,.71f,.012f));
            for(int side=-1;side<=1;side+=2)
                panel.Box(mat.bronze,new Vector3(-.83f,1.04f,side*.055f),new Vector3(.15f,.025f,.064f));
            panel.Finish(leaf,"Glazed pantry door");
            var solid=Box(leaf,"Pantry leaf panel",new Vector3(-width/2,1,0),new Vector3(width,2,.056f));
            var door=leaf.gameObject.AddComponent<PassDoor>();
            door.Configure(PassDoorKind.Galley,car,jamb,Quaternion.identity,Quaternion.Euler(0,90,0),solid);
            door.SetOpen(true,true);
            Grip(car,hardware,new Vector3(-.654f,2.43f,z),jamb.localRotation,door,null,mat);
        }

        static void LavatoryDoor(Transform car,Transform hardware,Finishes mat)
        {
            var leaf=Find(car,"LavatoryDoor");
            var parked=Find(car,"Lavatory open leaf").GetComponent<BoxCollider>();
            parked.enabled=false;
            Recorded(parked);
            var solid=Panel(car,leaf,"Lavatory door panel",new Vector3(-.147f,2.39f,1.55f),new Vector3(.898f,2.01f,.055f));
            var jamb=Child(hardware,"Lavatory threshold",new Vector3(.302f,2.39f,1.10f)).transform;
            var door=leaf.gameObject.AddComponent<PassDoor>();
            door.Configure(PassDoorKind.Lavatory,car,jamb,Turn(car,leaf,-90),leaf.localRotation,solid);
            door.SetOpen(true,true);
            Recorded(leaf);
            Grip(car,hardware,new Vector3(.31f,2.43f,.605f),Quaternion.identity,door,null,mat);
        }

        static void Rescue(Transform car,Transform hardware,Finishes mat)
        {
            Child(hardware,"EscapeLaunch",new Vector3(0,1.40f,9.53f));
            var grip=Child(hardware,"ChaseGrip",new Vector3(-1.90f,2.13f,8.04f)).transform;
            Child(hardware,"ChaseArrival",new Vector3(-.98f,1.40f,8.04f));
            var kit=new StationFabrication();
            // The historic open leaf is 1.15 m across, while the bellows clear
            // opening is wider. A fixed strike stile finishes the closed escape door.
            kit.Box(mat.wood,new Vector3(.577f,2.435f,8.913f),new Vector3(.214f,2.06f,.083f));
            kit.Box(mat.bronze,new Vector3(.474f,2.365f,8.908f),new Vector3(.024f,.256f,.085f));
            Box(hardware,"Escape door strike stile",new Vector3(.577f,2.435f,8.913f),
                new Vector3(.214f,2.06f,.083f));
            for(int s=-1;s<=1;s+=2)
                kit.Ring(mat.bronze,new Vector3(-1.56f,2.15f+s*.225f,8.58f),
                    new Vector3(-1.86f,2.15f+s*.225f,8.58f),.021f,.021f);
            kit.Ring(mat.bronze,new Vector3(-1.86f,1.926f,8.58f),new Vector3(-1.86f,2.375f,8.58f),.026f,.026f);
            kit.Box(mat.pine,new Vector3(-1.582f,2.91f,8.72f),new Vector3(.108f,.210f,.160f));
            kit.Ring(mat.lamp,new Vector3(-1.638f,2.91f,8.72f),new Vector3(-1.680f,2.91f,8.72f),.079f,.061f);
            // Enamel evacuation board on the right flank, facing inwards.
            kit.Box(mat.bronze,new Vector3(1.397f,3.073f,8.54f),new Vector3(.021f,.164f,.951f));
            kit.Sign(mat.screen,new Vector3(1.383f,3.073f,8.54f),.936f,.15f,6,false);
            kit.Finish(hardware,"Brake van emergency fittings");
            var glow=Child(grip,"ChaseLamp",new Vector3(.11f,.78f,.68f)).AddComponent<Light>();
            glow.type=LightType.Point;glow.color=new Color(1f,.65f,.19f);
            glow.intensity=1.6f;glow.range=3;glow.shadows=LightShadows.None;glow.enabled=false;
            MakePack(hardware,mat);
        }

        static void MakePack(Transform parent,Finishes mat)
        {
            // Between the existing parcels shelving and the guard's bulkhead is
            // an entire clear bay. The item's trigger projects beyond its own shelf.
            var shelf=Child(parent,"Reserve pack shelf",new Vector3(-1.17f,1.535f,2.55f)).transform;
            var frame=new StationFabrication();
            frame.Box(mat.wood,Vector3.zero,new Vector3(.50f,.049f,.79f));
            for(int s=-1;s<=1;s+=2)
                frame.Beam(mat.pine,new Vector3(-.20f,-.106f,s*.308f),new Vector3(.206f,-.025f,s*.308f),.045f,.032f);
            frame.Finish(shelf,"Emergency shelf");
            AddBox(shelf,new Vector3(.50f,.049f,.79f));
            var obj=Child(parent,"ParachutePack",new Vector3(-1.085f,1.571f,2.55f));
            var bag=new StationFabrication();
            // Rounded stuffed canvas with a cut-corner profile, a folded flap and web harness.
            var sections=new[]{(0f,.84f),(.027f,.95f),(.086f,1f),(.228f,1f),(.269f,.93f),(.292f,.74f)};
            var loop=new[]{new Vector2(-.156f,-.24f),new Vector2(-.121f,-.275f),new Vector2(.123f,-.275f),
                new Vector2(.17f,-.224f),new Vector2(.17f,.212f),new Vector2(.119f,.272f),
                new Vector2(-.112f,.272f),new Vector2(-.162f,.218f)};
            Vector3 V((float,float) station,Vector2 pt)=>new Vector3(pt.x*station.Item2,station.Item1,pt.y*station.Item2);
            for(int j=0;j<sections.Length-1;j++) for(int i=0;i<loop.Length;i++)
            {
                Vector2 a=loop[i],b=loop[(i+1)%loop.Length];
                bag.Quad(mat.canvas,V(sections[j],a),V(sections[j],b),V(sections[j+1],b),V(sections[j+1],a),
                         new Vector3(a.x+b.x,0,a.y+b.y));
            }
            for(int i=0;i<loop.Length;i++)
                bag.Triangle(mat.khaki,new Vector3(0,.293f,0),V(sections[5],loop[i]),V(sections[5],loop[(i+1)%loop.Length]),Vector3.up);
            for(int s=-1;s<=1;s+=2)
            {
                float z=s*.154f;
                bag.Box(mat.khaki,new Vector3(.172f,.138f,z),new Vector3(.015f,.236f,.041f));
                bag.Beam(mat.khaki,new Vector3(.174f,.248f,z),new Vector3(.098f,.296f,z),.038f,.014f);
                bag.Box(mat.khaki,new Vector3(-.012f,.295f,z),new Vector3(.219f,.015f,.040f));
                bag.Box(mat.bronze,new Vector3(.184f,.112f,z),new Vector3(.012f,.068f,.065f));
                bag.Box(mat.canvas,new Vector3(.192f,.112f,z),new Vector3(.008f,.042f,.040f));
            }
            bag.Ring(mat.oxblood,new Vector3(.171f,.228f,.152f),new Vector3(.195f,.25f,.062f),.016f,.016f,10);
            bag.Sign(mat.screen,new Vector3(.181f,.175f,0),.239f,.071f,7);
            bag.Finish(obj.transform,"Canvas parachute satchel");
            var trigger=obj.AddComponent<BoxCollider>();
            trigger.size=new Vector3(.395f,.33f,.565f);
            trigger.center=new Vector3(.018f,.153f,0);
            trigger.isTrigger=true;
            obj.AddComponent<PassPickup>();
        }

        public static GameObject MakeCanopy(Transform player)
        {
            var mat=new Finishes();
            var root=Child(player,"DeployedCanopy",Vector3.zero);
            var silk=new StationFabrication();
            const int gores=24,slices=8;
            Vector3 Dome(int i,int j)
            {
                float a=i*Mathf.PI*2/gores;
                float t=Mathf.Lerp(.065f,1.45f,j/(float)slices);
                float r=3.54f*Mathf.Sin(t);
                return new Vector3(r*Mathf.Cos(a),4.15f+2.44f*Mathf.Cos(t),r*Mathf.Sin(a));
            }
            for(int i=0;i<gores;i++)
            {
                for(int j=0;j<slices;j++)
                {
                    var a=Dome(i,j);var b=Dome(i+1,j);var c=Dome(i+1,j+1);var d=Dome(i,j+1);
                    silk.Quad((i%12==2||i%12==3)?mat.khaki:mat.silk,a,b,c,d,(a+b+c+d)/4-Vector3.up*4.15f);
                }
                var edge=Dome(i,slices);
                var junction=new Vector3(edge.x>0?.63f:-.63f,2.77f,edge.z>0?.27f:-.27f);
                silk.Ring(mat.khaki,edge,junction,.0034f,.0031f,5);
            }
            // Suspension lines become four risers, held well outside the passenger's
            // eye. Keep the terrain and locomotive visible when looking back under silk.
            for(int hand=-1;hand<=1;hand+=2) for(int fore=-1;fore<=1;fore+=2)
                silk.Ring(mat.wood,new Vector3(hand*.63f,2.77f,fore*.27f),
                          new Vector3(hand*.42f,1.24f,fore*.074f-.092f),.008f,.009f,6);
            silk.Finish(root.transform,"Silk reserve canopy");
            root.SetActive(false);
            return root;
        }
    }
}
