using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Winterbourne.Pass;
using static Winterbourne.Editor.StationFabrication;

namespace Winterbourne.Editor
{
    public static partial class PassFittings
    {
        const float DoorZ=8.04f;
        const float ThresholdY=2.42f;
        const float RailSide=-1.475f;
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
            foreach(int n in new[]{4,5,6,7,8,9})
            {
                var car=cars[n-1].transform;
                var structure=Child(car,"Pass entrance fittings",Vector3.zero).transform;
                int[] ends=(n==6||n==9)?new[]{1}:new[]{-1,1};
                CutSideWall(car,structure,ends);
                foreach(int sign in ends)
                {
                    if(n!=9)
                    {
                        // Originally parked on the platform side, the end leaf obstructs
                        // the turn across the vestibule. Its open pose fits the opposite wing.
                        var parked=Find(car,sign<0?"EndDoor_A":"EndDoor_B");
                        var local=car.InverseTransformPoint(parked.position);
                        local.x=Mathf.Abs(local.x);
                        parked.position=car.TransformPoint(local);
                        Recorded(parked);
                    }
                    SideDoor(car,structure,sign,n==9,mat);
                }
                if(n==9) Rescue(car,structure,mat);
            }
            AssetDatabase.SaveAssets();
        }

        static void CutSideWall(Transform car,Transform parent,int[] signs)
        {
            var old=Find(car,"Side Starboard").GetComponent<BoxCollider>();
            if(!old) throw new InvalidOperationException("Starboard wall collider missing on "+car.name);
            old.enabled=false;
            Recorded(old);
            float last=-9;
            foreach(int side in signs)
            {
                float z=side*DoorZ;
                Cut(last,z-.502f);
                // The floor remains continuous. Only the wood above the lintel is solid here.
                Box(parent,"Door header "+side,new Vector3(-1.473f,3.569f,z),new Vector3(.082f,.035f,1.004f));
                last=z+.502f;
            }
            Cut(last,9);
            void Cut(float a,float b)
            {
                if(b<=a)return;
                Box(parent,"Starboard wall section",new Vector3(-1.473f,2.48f,(a+b)/2),new Vector3(.082f,2.21f,b-a));
            }
        }

        static void SideDoor(Transform car,Transform hardware,int sign,bool chase,Finishes mat)
        {
            string suffix=sign<0?"A":"B";
            var leaf=Find(car,"Door_Starboard_"+suffix);
            float z=sign*DoorZ;
            var jamb=Child(hardware,"Station threshold "+suffix,new Vector3(RailSide,ThresholdY,z)).transform;
            var solid=Box(leaf,"Passenger door panel",Vector3.zero,new Vector3(.074f,2.16f,.975f));
            solid.transform.SetPositionAndRotation(car.TransformPoint(new Vector3(RailSide,2.468f,z)),car.rotation);
            Quaternion openWorld=Quaternion.AngleAxis(-sign*88,car.up)*leaf.rotation;
            var door=leaf.gameObject.AddComponent<PassDoor>();
            door.Configure(chase?PassDoorKind.Chase:PassDoorKind.Passenger,car,jamb,leaf.localRotation,
                           Quaternion.Inverse(leaf.parent.rotation)*openWorld,solid);

            // This narrow running board closes the gap from the wooden floor to a
            // high platform. It travels with the stock and is level in both directions.
            var landing=Child(hardware,"Level boarding plate "+suffix,new Vector3(-1.607f,1.355f,z)).transform;
            var step=new StationFabrication();
            step.Box(mat.soot,Vector3.zero,new Vector3(.31f,.045f,.964f));
            for(int i=-4;i<=4;i++)
                step.Box(mat.bronze,new Vector3(0,.024f,.094f*i),new Vector3(.278f,.004f,.009f));
            step.Finish(landing,"Brass boarding threshold");
            AddBox(landing,new Vector3(.31f,.045f,.964f));
            var signAnchor=Child(hardware,"Entrance notice "+suffix,new Vector3(-1.411f,3.57f,z)).transform;
            var plate=new StationFabrication();
            plate.Box(mat.bronze,Vector3.zero,new Vector3(.013f,.102f,.610f));
            plate.Sign(mat.screen,new Vector3(.009f,0,0),.595f,.089f,chase?5:4);
            plate.Finish(signAnchor,chase?"Guards boarding notice":"Way out notice");
        }

        static void Rescue(Transform car,Transform hardware,Finishes mat)
        {
            var leaf=Find(car,"EndDoor_B");
            var solid=Find(leaf,"Open end leaf B").GetComponent<BoxCollider>();
            if(!solid)throw new InvalidOperationException("Missing aft gangway door collision.");
            // Builder supplied the tall rear leaf open along -Z. Turning it -90°
            // about the true vertical hinge closes the portal from -X toward +X.
            var open=leaf.localRotation;
            var closed=Quaternion.Inverse(leaf.parent.rotation)*Quaternion.AngleAxis(-90,car.up)*leaf.rotation;
            var jamb=Child(hardware,"Tail escape sill",new Vector3(-.1f,2.425f,8.91f)).transform;
            jamb.localRotation=Quaternion.Euler(0,90,0);
            var exit=leaf.gameObject.AddComponent<PassDoor>();
            exit.Configure(PassDoorKind.Escape,car,jamb,closed,open,solid);
            Recorded(leaf);
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
