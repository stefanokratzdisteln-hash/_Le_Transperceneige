// Install this file in Assets/Editor of the Unity 6 URP project containing the train assets.
// -executeMethod Winterbourne.Editor.FleetImport.Build -formationRoot "<checkout>"
// -formationCars 05,06 builds and validates only the selected prefabs.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Winterbourne.Demo;
using Object = UnityEngine.Object;

namespace Winterbourne.Editor
{
    public static class FleetImport
    {
        public const string Base = "Assets/Transperceneige/Formation";
        public const string Original = "Assets/Transperceneige/Car07";
        public const string ScenePath = Base + "/Demo/WinterbourneExpress.unity";
        public static readonly Vector3 Bedside = new Vector3(-.13f, 1.405f, -5.21f);
        public const float Radius = .22f, Height = 1.72f;

        [Serializable] public class Box { public string name, parent; public float[] center, size; }
        [Serializable] public class Lamp { public string name; public float[] center, color; public float range, intensity; }
        [Serializable] public class Door { public string name; public float[] closedOffset; }
        [Serializable] public class Route { public string name; public Vector3[] points; }
        [Serializable] public class View { public float[] position, target; public float lens; }
        [Serializable] public class Layout
        {
            public int number, meshes, triangles;
            public string title;
            public float socketA, socketB;
            public Box[] boxes;
            public Lamp[] lights;
            public Door[] doors;
            public Route[] routes;
            public View view;
        }

        public static string Argument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index+1 < args.Length ? args[index+1] : null;
        }
        public static string Checkout => Path.GetFullPath(Argument("-formationRoot") ?? Directory.GetCurrentDirectory());
        public static string Id(int n) => $"Car{n:D2}";
        public static string PrefabPath(int n) => n == 7 ? Original + "/Car07.prefab" : $"{Base}/Prefabs/{Id(n)}.prefab";
        public static void Require(bool valid, string message)
        {
            if (!valid) throw new InvalidOperationException(message);
        }
        public static Vector3 Vector(float[] a)
        {
            Require(a != null && a.Length == 3 && a.All(v => !float.IsNaN(v) && !float.IsInfinity(v)), "Invalid layout vector");
            return new Vector3(a[0],a[1],a[2]);
        }
        public static Layout Read(int n)
        {
            string path = Path.Combine(Checkout, "Source", "Formation", "Layouts", Id(n)+".json");
            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText(path));
            Require(layout.number == n && layout.socketB > layout.socketA && layout.boxes?.Length > 0 &&
                    layout.lights != null && layout.routes?.Length > 0 && layout.view?.lens > 0, "Invalid layout " + path);
            return layout;
        }
        public static float[] Offsets(Layout[] cars)
        {
            var z = new float[cars.Length];
            for (int i=5;i>=0;i--) z[i]=z[i+1]+cars[i+1].socketA-cars[i].socketB;
            for (int i=7;i<cars.Length;i++) z[i]=z[i-1]+cars[i-1].socketB-cars[i].socketA;
            return z;
        }
        static GameObject Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }
        static T Load<T>(string path) where T:Object
        {
            var asset=AssetDatabase.LoadAssetAtPath<T>(path);
            Require(asset, "Missing " + path);
            return asset;
        }
        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent+"/"+name)) AssetDatabase.CreateFolder(parent,name);
        }
        static GameObject ImportModel(int number)
        {
            string path=number==7 ? Original+"/Car07.fbx" : $"{Base}/Models/{Id(number)}.fbx";
            var importer=AssetImporter.GetAtPath(path) as ModelImporter;
            Require(importer, "Missing model import " + path);
            importer.bakeAxisConversion=true;
            importer.preserveHierarchy=true;
            importer.globalScale=1;
            importer.useFileScale=true;
            importer.importCameras=false;
            importer.importLights=false;
            importer.importAnimation=false;
            importer.animationType=ModelImporterAnimationType.None;
            importer.addCollider=false;
            importer.importNormals=ModelImporterNormals.Import;
            importer.meshCompression=ModelImporterMeshCompression.Off;
            importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation=ModelImporterMaterialLocation.InPrefab;
            importer.SaveAndReimport();
            var materials=new Dictionary<string,Material> {
                {"Car07_Livery",Load<Material>(Original+"/Car07_Surface.mat")},
                {"Car07_CabinGlass",Load<Material>(Original+"/Car07_CabinGlass.mat")}
            };
            var identifiers=new HashSet<AssetImporter.SourceAssetIdentifier>(
                importer.GetExternalObjectMap().Keys.Where(id=>id.type==typeof(Material)));
            foreach (var mat in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
                identifiers.Add(new AssetImporter.SourceAssetIdentifier(mat));
            foreach (var id in identifiers)
            {
                Require(materials.ContainsKey(id.name), "Unrecognized material "+id.name+" on "+path);
                importer.AddRemap(id,materials[id.name]);
            }
            importer.SaveAndReimport();
            var source=Load<GameObject>(path);
            var renderers=source.GetComponentsInChildren<MeshRenderer>(true);
            Require(renderers.Length>0 && renderers.All(r=>r.sharedMaterials.Length>0 &&
                r.sharedMaterials.All(m=>m && materials.ContainsValue(m))), "FBX material remap failed on "+path);
            return source;
        }
        static Transform FindModelNode(GameObject visual, string name)
        {
            var matches=visual.GetComponentsInChildren<Transform>(true).Where(t=>t.name==name).ToArray();
            Require(matches.Length==1, "Expected one model node "+name+" on "+visual.name);
            return matches[0];
        }
        static void BuildCar(Layout spec)
        {
            var source=ImportModel(spec.number);
            var root=new GameObject(Id(spec.number));
            try
            {
                var visual=(GameObject)PrefabUtility.InstantiatePrefab(source,root.transform);
                if(spec.number==7) visual.name="RollingStock";
                var boxes=Child(root.transform,"Interior collision");
                foreach (var def in spec.boxes)
                {
                    var parent=string.IsNullOrEmpty(def.parent)?boxes.transform:FindModelNode(visual,def.parent);
                    var go=Child(parent,def.name);
                    go.transform.position=Vector(def.center);
                    go.transform.rotation=Quaternion.identity;
                    Require(Vector3.Distance(go.transform.lossyScale,Vector3.one)<.008f,"Scaled collision parent "+def.name);
                    var box=go.AddComponent<BoxCollider>();
                    box.size=Vector(def.size);
                    Require(Mathf.Min(box.size.x,box.size.y,box.size.z)>0, "Invalid collision box "+def.name);
                }
                var lighting=Child(root.transform,"Interior lighting");
                foreach (var def in spec.lights)
                {
                    var light=Child(lighting.transform,def.name).AddComponent<Light>();
                    light.transform.localPosition=Vector(def.center);
                    var rgb=Vector(def.color);
                    light.color=new Color(rgb.x,rgb.y,rgb.z);
                    light.range=def.range;
                    bool overhead=def.center[1]>=3.11f;
                    light.type=overhead?LightType.Spot:LightType.Point;
                    light.intensity=def.intensity*(overhead?.80f:.44f);
                    light.shadows=LightShadows.None;
                    if (overhead)
                    {
                        light.transform.localRotation=Quaternion.Euler(90,0,0);
                        light.spotAngle=142;
                        light.innerSpotAngle=86;
                    }
                }
                foreach (var def in spec.doors??Array.Empty<Door>())
                {
                    var t=FindModelNode(visual,def.name);
                    var door=t.gameObject.AddComponent<Car07SlidingDoor>();
                    door.closedOffset=t.parent.InverseTransformVector(Vector(def.closedOffset));
                    Require(door.GetComponentInChildren<BoxCollider>(),"Missing sliding leaf collision "+def.name);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(door);
                }
                foreach(var pair in new[]{("Socket_A",spec.socketA),("Socket_B",spec.socketB)})
                {
                    var found=visual.GetComponentsInChildren<Transform>(true).SingleOrDefault(t=>t.name==pair.Item1);
                    if (!found)
                    {
                        found=Child(root.transform,pair.Item1).transform;
                        found.localPosition=new Vector3(0,.97f,pair.Item2);
                    }
                    Require(Mathf.Abs(found.position.z-pair.Item2)<.012f,"Socket does not match layout on "+root.name);
                }
                PrefabUtility.SaveAsPrefabAsset(root,PrefabPath(spec.number),out bool saved);
                Require(saved,"Could not save "+root.name);
            }
            finally {Object.DestroyImmediate(root);}
        }
        static void Standing(Vector3 foot, string label)
        {
            bool ground=Physics.Raycast(foot+Vector3.up*.32f,Vector3.down,out var hit,.48f,~0,QueryTriggerInteraction.Ignore);
            Require(ground && Mathf.Abs(hit.point.y-foot.y)<.13f,"Missing walk surface at "+label+" "+foot.ToString("F3"));
            var lower=foot+Vector3.up*(Radius+.039f);
            var upper=foot+Vector3.up*(Height-Radius+.039f);
            var hits=Physics.OverlapCapsule(lower,upper,Radius,~0,QueryTriggerInteraction.Ignore);
            Require(hits.Length==0,"Blocked "+label+" at "+foot.ToString("F3")+" by "+string.Join(", ",hits.Select(c=>c.name)));
        }
        public static void Passage(Vector3 from,Vector3 to,string label)
        {
            int steps=Math.Max(1,Mathf.CeilToInt(Vector3.Distance(from,to)/.085f));
            for(int i=0;i<=steps;i++) Standing(Vector3.Lerp(from,to,i/(float)steps),label);
        }
        public static void VerifyCar(Layout spec,GameObject car)
        {
            Require(!car.GetComponent<Collider>(),"Solid root collider "+car.name);
            var filters=car.GetComponentsInChildren<MeshFilter>(true);
            var doors=car.GetComponentsInChildren<Car07SlidingDoor>(true);
            Require(doors.Length==(spec.doors?.Length??0),"Missing moving doors in "+car.name);
            if(spec.number==5)
            {
                var names=new HashSet<string>(filters.Select(f=>f.name));
                Require(Enumerable.Range(1,8).All(i=>names.Contains($"Saloon_Row_{i:D2}")) &&
                    car.GetComponentsInChildren<BoxCollider>(true).Count(c=>c.name.StartsWith("Saloon_Row",StringComparison.Ordinal))==16,
                    "The empty saloon must retain eight rows of paired benches and their colliders");
                Require(!car.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Travellers" ||
                    t.name.StartsWith("Traveller_",StringComparison.Ordinal)), "Seated figures still present in the saloon");
            }
            if(spec.number==4)
            {
                var panes=filters.Where(f=>f.name.StartsWith("DoorPane_",StringComparison.Ordinal)).ToArray();
                var glass=Load<Material>(Original+"/Car07_CabinGlass.mat");
                Require(panes.Length==4 && panes.All(p=>p.GetComponentInParent<Car07SlidingDoor>() &&
                    p.GetComponent<MeshRenderer>().sharedMaterials.Contains(glass)), "Missing attached compartment door glass");
            }
            if(spec.number==8)
            {
                var names=new HashSet<string>(filters.Where(f=>f.gameObject.activeInHierarchy).Select(f=>f.name));
                Require(Enumerable.Range(1,16).All(i=>names.Contains($"BerthCurtain_{i:D2}")), "Expected sixteen individual privacy curtains");
            }
            long tris=0;
            foreach(var filter in filters)
            {
                var mesh=filter.sharedMesh;
                Require(mesh && mesh.vertexCount>0 && mesh.HasVertexAttribute(VertexAttribute.TexCoord0),"Missing geometry/UV on "+filter.name);
                for(int i=0;i<mesh.subMeshCount;i++) tris+=mesh.GetIndexCount(i)/3;
            }
            Require((spec.meshes==0 || filters.Length==spec.meshes) && (spec.triangles==0 || tris==spec.triangles), "Geometry count mismatch in "+car.name);
            Require(car.GetComponentsInChildren<Light>().Length==spec.lights.Length,"Lighting count mismatch "+car.name);
            Require(car.GetComponentsInChildren<BoxCollider>().Length==spec.boxes.Length,"Collision count mismatch "+car.name);
            Physics.SyncTransforms();
            var solids=car.GetComponentsInChildren<BoxCollider>();
            foreach(var def in spec.boxes)
            {
                var shape=solids.Single(c=>c.name==def.name);
                Require(!shape.isTrigger && shape.enabled &&
                    Vector3.Distance(shape.size,Vector(def.size))<.005f &&
                    Vector3.Distance(car.transform.InverseTransformPoint(shape.bounds.center),Vector(def.center))<.009f,
                    "Invalid physical shape "+def.name+" on "+car.name);
                if(!string.IsNullOrEmpty(def.parent)) Require(shape.transform.parent.name==def.parent,"Incorrect moving leaf parent");
            }
            foreach(var route in spec.routes)
            {
                Require(route.points?.Length>0,"Empty route "+route.name);
                for(int i=0;i<route.points.Length;i++)
                    Passage(car.transform.TransformPoint(route.points[Math.Max(0,i-1)]),car.transform.TransformPoint(route.points[i]),car.name+" / "+route.name);
                Debug.Log($"FLEET ROUTE {Id(spec.number)} {route.name}: {route.points.Length} checkpoints");
            }
            Debug.Log($"FLEET CAR {spec.number:D2}: {filters.Length} meshes, {tris} triangles, {spec.boxes.Length} collision boxes");
        }

        public static void Build()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EnsureFolder(Base,"Prefabs");
            EnsureFolder(Base,"Demo");
            string selection=Argument("-formationCars");
            int[] numbers=selection==null?Enumerable.Range(1,9).ToArray():
                selection.Split(',').Select(int.Parse).ToArray();
            foreach(int n in numbers)
            {
                Require(n>=1 && n<=9,"Unknown formation vehicle");
                var layout=Read(n);
                BuildCar(layout);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
                var car=(GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(PrefabPath(n)));
                VerifyCar(layout,car);
            }
            if(selection!=null) return;
            BuildScene();
            Export();
        }

        public static void BuildScene()
        {
            var layouts=Enumerable.Range(1,9).Select(Read).ToArray();
            var offsets=Offsets(layouts);
            // The proven sleeping-car scene already contains the passenger controller,
            // listener, moonlight and original nested prefab.
            var scene=EditorSceneManager.OpenScene(Original+"/Demo/Car07_Walkthrough.unity",OpenSceneMode.Single);
            var sleeper=scene.GetRootGameObjects().Single(g=>PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g)==PrefabPath(7));
            Require(Vector3.Distance(sleeper.transform.position,Vector3.zero)<.01f,"Sleeping car must be the scene datum");
            for(int i=0;i<layouts.Length;i++)
            {
                if(i==6) continue;
                var car=(GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(PrefabPath(i+1)),scene);
                car.transform.localPosition=Vector3.forward*offsets[i];
            }
            var player=scene.GetRootGameObjects().Single(g=>g.GetComponent<Car07Walker>());
            player.transform.SetPositionAndRotation(Bedside,Quaternion.Euler(0,180,0));
            var camera=player.GetComponentInChildren<Camera>();
            camera.farClipPlane=270;
            camera.backgroundColor=new Color(.25f,.34f,.43f);
            RenderSettings.fog=true;
            RenderSettings.fogMode=FogMode.ExponentialSquared;
            RenderSettings.fogColor=camera.backgroundColor;
            RenderSettings.fogDensity=.004f;
            FleetLandscape.Build(Mathf.Max(Mathf.Abs(offsets[0]), Mathf.Abs(offsets[8]))+80);
            Require(EditorSceneManager.SaveScene(scene,ScenePath),"Could not save the formation");
            AssetDatabase.SaveAssets();
            VerifyFormation();
        }

        static void Export() => WritePackage("Assets/Transperceneige","Winterbourne");

        public static void ExportSleeper() => WritePackage(Original,"Car07");

        static void WritePackage(string assetFolder,string packageName)
        {
            string releaseFolder=Path.Combine(Checkout,"Releases");
            Directory.CreateDirectory(releaseFolder);
            var target=Path.Combine(releaseFolder,packageName+".unitypackage");
            var staging=Path.Combine(releaseFolder,packageName+"."+Guid.NewGuid().ToString("N")+".unitypackage");
            try
            {
                AssetDatabase.ExportPackage(assetFolder,staging,ExportPackageOptions.Recurse);
                Require(File.Exists(staging) && new FileInfo(staging).Length>0,"Unity did not generate an archive");
                if(File.Exists(target)) File.Replace(staging,target,null); else File.Move(staging,target);
            }
            finally {if(File.Exists(staging)) File.Delete(staging);}
            Debug.Log("FLEET PACKAGE WRITTEN "+target);
        }

        public static void VerifyFormation()
        {
            var layouts=Enumerable.Range(1,9).Select(Read).ToArray();
            var z=Offsets(layouts);
            var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            var roots=scene.GetRootGameObjects();
            var passenger=roots.Single(g=>g.GetComponent<Car07Walker>());
            var capsule=passenger.GetComponent<CharacterController>();
            bool enabled=capsule.enabled;
            capsule.enabled=false;
            try
            {
                Physics.SyncTransforms();
                Standing(Bedside,"passenger spawn");
                for(int i=0;i<9;i++)
                {
                    var car=roots.Single(g=>PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g)==PrefabPath(i+1));
                    Require(Mathf.Abs(car.transform.position.z-z[i])<.002f,"Formation offset "+car.name);
                    VerifyCar(layouts[i],car);
                    if(i<8)
                    {
                        float joint=z[i]+layouts[i].socketB;
                        Require(Mathf.Abs(joint-z[i+1]-layouts[i+1].socketA)<.002f,"Coupling joint "+i);
                        Passage(new Vector3(0,1.395f,joint-.40f),new Vector3(0,1.395f,joint+.40f),"coupled gangway "+(i+1));
                    }
                    foreach(var t in car.GetComponentsInChildren<Transform>(true))
                        Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,"Missing script on "+t.name);
                }
                foreach(float doorZ in new[]{-5.21f,-3.67f,3.67f,5.21f})
                    Passage(new Vector3(.90f,1.395f,doorZ),new Vector3(-.13f,1.395f,doorZ),"original cabin");
                Require(Physics.CheckSphere(new Vector3(-1.01f,1.64f,-6.18f),.10f),"Original compartment berth lost its collision");
                Vector3[] bedsideExit={Bedside,new Vector3(.9f,1.395f,-5.21f),new Vector3(.9f,1.395f,-7.55f),
                    new Vector3(0,1.395f,-7.55f),new Vector3(0,1.395f,layouts[6].socketA)};
                for(int i=1;i<bedsideExit.Length;i++) Passage(bedsideExit[i-1],bedsideExit[i],"bedside to kitchen");
            }
            finally {capsule.enabled=enabled;}
            Debug.Log("FLEET FORMATION VERIFIED: nine linked vehicles, eight continuous gangways, nine source prefabs");
        }
    }
}
