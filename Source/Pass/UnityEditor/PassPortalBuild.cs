using System;
using UnityEditor;
using UnityEngine;
using Winterbourne.Pass;
using static Winterbourne.Editor.StationFabrication;
using Object = UnityEngine.Object;

namespace Winterbourne.Editor
{
    public static partial class PassPortalBuild
    {
        const string Package = "Assets/Transperceneige/Pass";
        const string GateAsset = Package + "/Waygate.prefab";
        sealed class Palette
        {
            public readonly Material oak=Paint("Keeper's oiled oak",new Color(.28f,.143f,.072f),.08f,.39f);
            public readonly Material cream=Paint("Keeper's limewash",new Color(.37f,.39f,.405f),0,.21f);
            public readonly Material green=Paint("Keeper's painted matchboard",new Color(.132f,.163f,.166f),.02f,.32f);
            public readonly Material slate=Paint("Keeper's slate",new Color(.135f,.165f,.18f),.18f,.36f);
            public readonly Material stone=Paint("Keeper's rubble stone",new Color(.36f,.364f,.345f));
            public readonly Material copper=Paint("Waygate bronze",new Color(.58f,.31f,.12f),.61f,.53f);
            public readonly Material gilt=Paint("Waygate edge brass",new Color(.88f,.65f,.30f),.59f,.58f);
            public readonly Material teal=Paint("Waygate turquoise",new Color(.31f,.69f,.67f),.12f,.62f,1.25f);
            public readonly Material amber=Paint("Keeper's lamp glass",new Color(.82f,.64f,.39f),.05f,.42f,.66f);
            public readonly Material snow=AssetDatabase.LoadAssetAtPath<Material>(Package+"/Scenery/Snow.mat");
            public readonly Material glass=AssetDatabase.LoadAssetAtPath<Material>("Assets/Transperceneige/Car07/Car07_CabinGlass.mat");
            public Palette(){ApplyTimber(oak,.84f);}
        }

        public static void Build(PassJourney route)
        {
            var colours=new Palette();
            var prefab=MakeGate(colours);
            var sleeper=Child(route.consist,"07 bedside arrival",route.walker.transform.localPosition).transform;
            sleeper.localRotation=route.walker.transform.localRotation;
            route.sleeperSpawn=sleeper;

            var hut=new GameObject("Greywater viaduct keeper's room").transform;
            // Between stone piers, on the floor of the gorge. The western window frames the old aqueduct.
            float highest=float.NegativeInfinity;
            for(int ix=-1;ix<=1;ix++)
            for(int iz=-1;iz<=1;iz++)
            {
                float x=ix*4.3f,s=1820-iz*3.5f;
                float survey=PassAtmosphere.GroundHeight(x,s);
                var top=new Vector3(x,survey+4,-s);
                float y=Physics.Raycast(top,Vector3.down,out var ground,10,~0,QueryTriggerInteraction.Ignore) ? ground.point.y : survey;
                highest=Mathf.Max(highest,y);
            }
            hut.position=new Vector3(0,highest+.17f,-1820f);
            MakeRoom(hut,colours);
            route.hubSpawn=Child(hut,"Keeper's room arrival",new Vector3(.71f,.055f,-1.38f)).transform;
            var hubArch=Child(hut,"Coach portal location",new Vector3(.05f,.015f,2.04f)).transform;
            route.hubSpawn.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(hubArch.position-Vector3.right*.67f-route.hubSpawn.position,Vector3.up));
            route.hubGate=Install(route,prefab,hubArch,PassPortal.Kind.Board);
            var station=GameObject.Find("Fellbridge portal arch anchor");
            var stationArrival=GameObject.Find("Fellbridge portal arrival");
            if(!station || !stationArrival) throw new InvalidOperationException("Missing Fellbridge building portal markers.");
            route.stationSpawn=stationArrival.transform;
            route.stationGate=Install(route,prefab,station.transform,PassPortal.Kind.Station);
            var riverMarker=new GameObject("Touchdown return gate").transform;
            route.returnGate=Install(route,prefab,riverMarker,PassPortal.Kind.Return);
            route.returnGate.gameObject.SetActive(false);
            route.walker.transform.SetParent(null,true);
            route.walker.transform.SetPositionAndRotation(route.hubSpawn.position,route.hubSpawn.rotation);
            Physics.SyncTransforms();
        }

        static PassPortal Install(PassJourney route,GameObject prefab,Transform anchor,PassPortal.Kind kind)
        {
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,anchor);
            instance.transform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);
            var gate=instance.GetComponent<PassPortal>();
            gate.destination=kind;
            gate.journey=route;
            PrefabUtility.RecordPrefabInstancePropertyModifications(gate);
            return gate;
        }

        static void Solid(Transform root,string name,Vector3 centre,Vector3 size)
        {
            var block=Child(root,name,centre).AddComponent<BoxCollider>();
            block.size=size;
        }

        static Light Lamp(Transform root,string name,Vector3 position,Color tint,float power,float range)
        {
            var light=Child(root,name,position).AddComponent<Light>();
            light.type=LightType.Point;light.color=tint;light.intensity=power;
            light.range=range;light.shadows=LightShadows.None;
            return light;
        }

        static GameObject MakeGate(Palette m)
        {
            var asset=new GameObject("Winterbourne Waygate");
            var t=asset.transform;
            var frame=new StationFabrication();
            BookcaseGate(t,frame,m);
            frame.Finish(t,"Waygate framework");
            var veil=Child(t,"Passage glass",new Vector3(0,0,.12f));
            var shader=Shader.Find("Winterbourne/Portal glass");
            if(!shader) throw new InvalidOperationException("Portal glass shader missing");
            string materialPath=Package+"/Waygate glass.mat";
            var glass=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(!glass) {glass=new Material(shader);AssetDatabase.CreateAsset(glass,materialPath);}
            glass.shader=shader;
            glass.SetColor("_Tint",new Color(.39f,.73f,1f));
            EditorUtility.SetDirty(glass);
            var sheet=new StationFabrication();
            sheet.Quad(glass,new Vector3(-.745f,.016f,0),new Vector3(.745f,.016f,0),
                new Vector3(.745f,2.495f,0),new Vector3(-.745f,2.495f,0),Vector3.back);
            sheet.Finish(veil.transform,"Waygate mist panel");
            var gate=asset.AddComponent<PassPortal>();
            gate.cover=asset.GetComponent<PassBookcaseCover>();
            gate.threshold=asset.AddComponent<BoxCollider>();gate.threshold.isTrigger=true;
            gate.threshold.center=new Vector3(0,1.12f,0);gate.threshold.size=new Vector3(1.43f,2.24f,.45f);
            var sign=Child(t,"Enamel destination lettering",new Vector3(0,2.56f,-.191f)).AddComponent<TextMesh>();
            var font=AssetDatabase.LoadAssetAtPath<Font>(Package+"/HUD/WinterbourneSans.ttf");
            sign.font=font;sign.fontSize=70;sign.characterSize=.0115f;sign.anchor=TextAnchor.MiddleCenter;
            sign.alignment=TextAlignment.Center;sign.color=new Color(1,.917f,.69f);
            sign.GetComponent<MeshRenderer>().sharedMaterial=font.material;
            sign.text="SLEEPER 07";gate.plaque=sign;
            Lamp(t,"Light from the hidden passage",new Vector3(.14f,1.52f,-.18f),new Color(.49f,.62f,.92f),.77f,2.8f);
            var result=PrefabUtility.SaveAsPrefabAsset(asset,GateAsset);
            Object.DestroyImmediate(asset);
            return result;
        }

        static void MakeRoom(Transform hut,Palette m)
        {
            var b=new StationFabrication();
            var ceiling=new Vector3(8.35f,.16f,6.75f);
            b.Box(m.stone,new Vector3(0,-.37f,0),new Vector3(8.44f,.74f,6.82f));
            Solid(hut,"Room floor",new Vector3(0,-.14f,0),new Vector3(8.3f,.28f,6.7f));
            for(int i=0;i<31;i++)
                b.Box(m.oak,new Vector3(-3.99f+i*.266f,-.012f,0),new Vector3(.260f,.038f,6.53f));
            for(int side=-1;side<=1;side+=2)
            {
                float z=side*3.31f;
                b.Box(m.stone,new Vector3(0,1.61f,z),new Vector3(8.36f,3.22f,.24f));
                b.Box(m.cream,new Vector3(0,2.04f,z-side*.131f),new Vector3(8.17f,2.37f,.046f));
                b.Box(m.green,new Vector3(0,.53f,z-side*.138f),new Vector3(8.13f,1.05f,.064f));
                b.Box(m.oak,new Vector3(0,1.075f,z-side*.175f),new Vector3(8.19f,.096f,.108f));
                for(int i=-6;i<=6;i++) b.Box(m.oak,new Vector3(i*.654f,.55f,z-side*.178f),new Vector3(.037f,1.02f,.047f));
                Solid(hut,"Room wall",new Vector3(0,1.65f,z),new Vector3(8.35f,3.3f,.25f));

                float x=side*4.12f;
                // Leaded sash looking out across the gorge, surrounded by a real opening.
                foreach (var wall in new[]{new Vector2(-2.25f,2.08f),new Vector2(2.25f,2.08f)})
                    b.Box(m.cream,new Vector3(x,1.64f,wall.x),new Vector3(.23f,3.28f,wall.y));
                b.Box(m.green,new Vector3(x,.57f,0),new Vector3(.23f,1.14f,6.61f));
                b.Box(m.stone,new Vector3(x,2.98f,0),new Vector3(.24f,.68f,6.61f));
                b.Box(m.oak,new Vector3(x-side*.031f,1.145f,0),new Vector3(.38f,.071f,2.62f));
                b.Box(m.glass,new Vector3(x,1.906f,0),new Vector3(.021f,1.45f,2.44f));
                foreach(float zbar in new[]{-1.23f,-.40f,.40f,1.23f})
                    b.Box(m.green,new Vector3(x,1.91f,zbar),new Vector3(.12f,1.53f,.054f));
                b.Box(m.green,new Vector3(x,2.01f,0),new Vector3(.10f,.047f,2.46f));
                Solid(hut,"Sash and window wall",new Vector3(x,1.64f,0),new Vector3(.25f,3.28f,6.6f));
            }
            b.Box(m.cream,new Vector3(0,3.26f,0),ceiling);
            Solid(hut,"Room ceiling",new Vector3(0,3.27f,0),ceiling);
            for(int i=-2;i<=2;i++)
                b.Box(m.oak,new Vector3(i*1.70f,3.16f,0),new Vector3(.124f,.19f,6.58f));
            for(int side=-1;side<=1;side+=2)
            {
                var q=Quaternion.Euler(0,0,-side*17);
                b.Box(m.slate,new Vector3(side*2.10f,3.94f,0),new Vector3(4.52f,.15f,7.02f),q);
                b.Box(m.snow,new Vector3(side*2.1f,4.035f,0),new Vector3(4.54f,.08f,7.00f),q);
            }
            // Bridge keeper's writing table, blotter, dispatch book, ink and shaded lamp.
            var table=new Vector3(-2.83f,.91f,.65f);
            b.Box(m.oak,table,new Vector3(1.22f,.092f,2.05f));
            for(int i=-1;i<=1;i+=2) for(int j=-1;j<=1;j+=2)
                b.Box(m.green,table+new Vector3(.49f*i,-.46f,.89f*j),new Vector3(.105f,.87f,.105f));
            Solid(hut,"Keeper's writing desk",table+Vector3.down*.43f,new Vector3(1.2f,.92f,2.04f));
            b.Box(m.green,table+new Vector3(.07f,.055f,.08f),new Vector3(.82f,.014f,1.19f));
            b.Box(m.cream,table+new Vector3(.09f,.073f,.12f),new Vector3(.49f,.021f,.33f),Quaternion.Euler(0,11,0));
            for(int i=0;i<6;i++) b.Box(m.oak,table+new Vector3(.01f,.085f,.02f+i*.027f),new Vector3(.27f,.002f,.004f));
            var lamp=table+new Vector3(-.26f,.06f,.69f);
            b.Ring(m.copper,lamp,lamp+Vector3.up*.05f,.142f,.12f);
            b.Ring(m.gilt,lamp+Vector3.up*.05f,lamp+Vector3.up*.39f,.023f,.021f);
            b.Ring(m.amber,lamp+Vector3.up*.37f,lamp+Vector3.up*.53f,.183f,.082f);
            b.Box(m.slate,table+new Vector3(-.21f,.091f,-.58f),new Vector3(.31f,.075f,.40f));
            Lamp(hut,"Desk light",lamp+new Vector3(.15f,.40f,-.01f),new Color(1,.79f,.61f),.88f,3.4f);
            // A cast-iron stove faces the desk from the other side of the arch.
            var stove=new Vector3(2.72f,.055f,2.34f);
            b.Box(m.slate,stove,new Vector3(1.08f,.105f,1.12f));
            for(int side=-1;side<=1;side+=2)
            for(int end=-1;end<=1;end+=2)
            {
                var foot=stove+new Vector3(.218f*side,.065f,.21f*end);
                b.Beam(m.slate,foot,foot+new Vector3(-.047f*side,.192f,-.034f*end),.074f,.071f);
            }
            b.Ring(m.slate,stove+Vector3.up*.235f,stove+Vector3.up*.54f,.312f,.362f,24);
            b.Ring(m.slate,stove+Vector3.up*.54f,stove+Vector3.up*.91f,.362f,.273f,24);
            b.Ring(m.copper,stove+Vector3.up*.519f,stove+Vector3.up*.546f,.366f,.368f,24);
            b.Ring(m.slate,stove+Vector3.up*.91f,stove+Vector3.up*.95f,.315f,.31f,24);
            var fireDoor=stove+new Vector3(0,.55f,-.316f);
            b.Box(m.copper,fireDoor,new Vector3(.395f,.385f,.06f));
            b.Box(m.slate,fireDoor+Vector3.back*.034f,new Vector3(.356f,.35f,.023f));
            for(int grate=-2;grate<=2;grate++)
                b.Box(m.amber,fireDoor+new Vector3(grate*.054f,.023f,-.047f),new Vector3(.024f,.145f,.005f));
            b.Box(m.gilt,fireDoor+new Vector3(.158f,.006f,-.068f),new Vector3(.085f,.023f,.041f));
            var pipe=stove+new Vector3(0,.95f,.099f);
            b.Ring(m.slate,pipe,pipe+Vector3.up*3.26f,.087f,.083f,18);
            for(int band=0;band<5;band++)
                b.Ring(m.copper,pipe+Vector3.up*(band*.69f),pipe+Vector3.up*(band*.69f+.024f),.091f,.091f,18);
            Solid(hut,"Stove hearth",stove+Vector3.up*.48f,new Vector3(.77f,.98f,.74f));
            Lamp(hut,"Firebox glow",fireDoor+Vector3.back*.10f,new Color(1,.49f,.23f),.49f,2.2f);
            // Railway regulator. The dial, hour strokes and hands are actual raised fittings.
            var clock=new Vector3(-1.26f,2.10f,3.125f);
            b.Ring(m.copper,clock,clock+Vector3.back*.060f,.296f,.296f,40);
            b.Ring(m.cream,clock+Vector3.back*.061f,clock+Vector3.back*.071f,.264f,.264f,40);
            for(int i=0;i<12;i++)
            {
                var turn=Quaternion.AngleAxis(i*30f,Vector3.forward);
                b.Box(m.slate,clock+turn*(Vector3.up*.22f)+Vector3.back*.079f,new Vector3(.015f,.048f,.008f),turn);
            }
            b.Beam(m.slate,clock+Vector3.back*.087f,clock+new Vector3(-.117f,.122f,-.087f),.019f,.011f);
            b.Beam(m.slate,clock+Vector3.back*.092f,clock+new Vector3(.128f,.034f,-.092f),.027f,.013f);
            for(int i=0;i<2;i++)
            {
                var lampPos=new Vector3(i==0?1.86f:-1.91f,2.95f,i==0?-.94f:1.44f);
                b.Ring(m.copper,lampPos+Vector3.up*.25f,lampPos,.028f,.038f);
                b.Ring(m.amber,lampPos-Vector3.up*.155f,lampPos,.168f,.087f);
                var downlight=Lamp(hut,"Shaded reading light",lampPos+Vector3.down*.18f,new Color(1,.86f,.72f),1.15f,5.4f);
                downlight.type=LightType.Spot;
                downlight.transform.rotation=Quaternion.Euler(90,0,0);
                downlight.spotAngle=117; downlight.innerSpotAngle=61;
                downlight.shadows=LightShadows.Soft;
            }
            FitReadingRoom(hut,b,m);
            b.Finish(hut,"Viaduct keeper's lamp room");
            var shelter=Child(hut,"Keeper's room weather bounds",new Vector3(0,1.62f,0));
            shelter.layer=2;
            var box=shelter.AddComponent<BoxCollider>();
            box.isTrigger=true;
            box.size=new Vector3(8.18f,3.25f,6.59f);
            shelter.AddComponent<PassShelterVolume>();
        }
    }
}
