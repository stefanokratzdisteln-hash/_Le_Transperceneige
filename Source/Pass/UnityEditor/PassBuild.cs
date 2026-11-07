// Build in a Unity 6 URP project containing the nine vehicle prefabs and Pass runtime scripts.
// Put Source/Pass/UnityEditor and Source/Formation/UnityEditor in an Assets/Editor directory.
// -batchmode -quit -executeMethod Winterbourne.Editor.PassBuild.Build -formationRoot "<checkout>"
// -executeMethod Winterbourne.Editor.PassBuild.Export writes the verified Unity package separately.
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using Winterbourne.Demo;
using Winterbourne.Pass;
using Object = UnityEngine.Object;

namespace Winterbourne.Editor
{
    public static class PassBuild
    {
        public const string Root = "Assets/Transperceneige/Pass";
        public const string ScenePath = Root + "/WinterbournePass.unity";
        public const float NoseOffset = 110.67f;
        public static void Check(bool value, string message) {
            if (!value) throw new InvalidOperationException(message);
        }

        static Transform Anchor(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).Single(t => t.name == name);
        }

        static UIDocument SetHud(PassJourney journey)
        {
            const string folder = Root + "/HUD";
            const string themePath = folder + "/PassTheme.tss";
            const string settingsPath = folder + "/PassPanel.asset";
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(folder + "/PassHUD.uxml");
            Check(tree, "Missing Pass HUD UXML");
            if (!File.Exists(themePath)) File.WriteAllText(themePath, "@import url('unity-theme://default');\n");
            AssetDatabase.ImportAsset(themePath);
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(themePath);
            Check(theme, "Missing runtime theme");
            var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(settingsPath);
            if (!settings) {
                settings = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(settings, settingsPath);
            }
            settings.themeStyleSheet = theme;
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1600, 900);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = .5f;
            EditorUtility.SetDirty(settings);
            var host = new GameObject("Passenger interface");
            var doc = host.AddComponent<UIDocument>();
            doc.panelSettings = settings;
            doc.visualTreeAsset = tree;
            host.AddComponent<PassHUD>().journey = journey;
            return doc;
        }

        public static void Build()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var scene = EditorSceneManager.OpenScene(FleetImport.ScenePath, OpenSceneMode.Single);
            var roots = scene.GetRootGameObjects();
            var player = roots.SingleOrDefault(g => g.GetComponent<Car07Walker>());
            Check(player, "Walkthrough player missing in "+scene.path+"; roots: "+string.Join(", ",roots.Select(g=>g.name)));
            var walker = player.GetComponent<Car07Walker>();
            var originalDecoration = roots.Single(g => g.name == "Winter valley and permanent way");
            Object.DestroyImmediate(originalDecoration);

            var cars = Enumerable.Range(1,9).Select(n => roots.Single(g => g &&
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g) == FleetImport.PrefabPath(n))).ToArray();
            var consist = new GameObject("Consist").transform;
            foreach (var car in cars) car.transform.SetParent(consist, true);
            player.transform.SetParent(consist, true);
            player.transform.localRotation = Quaternion.Euler(0, -101f, 0);
            walker.eyeCamera = player.GetComponentInChildren<Camera>();
            walker.eyeCamera.transform.localRotation = Quaternion.identity;
            walker.eyeCamera.farClipPlane = 1850f;
            walker.eyeCamera.nearClipPlane = .025f;
            walker.eyeCamera.clearFlags = CameraClearFlags.Skybox;
            walker.eyeCamera.GetUniversalAdditionalCameraData();
            consist.position = Vector3.forward * NoseOffset;

            PassLandscape.Build();
            PassFittings.Build(consist, cars);
            var route = new GameObject("Winterbourne Pass journey").AddComponent<PassJourney>();
            route.consist = consist;
            route.platformFloor = GameObject.Find("Fellbridge Station/Platform structure/Platform floor").GetComponent<BoxCollider>();
            route.walker = walker;
            route.chaseGrip = Anchor(cars[8].transform, "ChaseGrip");
            route.chaseArrival = Anchor(cars[8].transform, "ChaseArrival");
            route.escapeLaunch = Anchor(cars[8].transform, "EscapeLaunch");
            route.canopy = PassFittings.MakeCanopy(player.transform);
            route.canopy.SetActive(false);
            PassPortalBuild.Build(route);
            SetHud(route);
            AssetDatabase.SaveAssets();
            Check(EditorSceneManager.SaveScene(scene, ScenePath, true), "Could not save WinterbournePass scene");
            // The edits belong to the saved route copy. Leave the source walkthrough on disk untouched.
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            CheckSavedScene();
        }

        public static void CheckSavedScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var route = Object.FindFirstObjectByType<PassJourney>();
            Check(route && route.walker && route.consist && route.canopy && route.chaseGrip && route.chaseArrival &&
                route.escapeLaunch, "Missing a route or passenger reference in the saved scene");
            Check(route.hubGate && route.stationGate && route.returnGate && route.hubSpawn && route.stationSpawn &&
                route.sleeperSpawn && route.sleeperSpawn.IsChildOf(route.consist), "Missing portal or destination");
            Check(!route.walker.transform.IsChildOf(route.consist) && !route.canopy.activeSelf &&
                Vector3.Distance(route.walker.transform.position,route.hubSpawn.position)<.06f,
                "Passenger must start in the bridge keeper's room");
            Check(route.stationGate.transform.position.y < 2.5f && route.stationSpawn.position.y < 2.5f,
                "Fellbridge gate and arrival must be on the ground floor");
            var document = Object.FindFirstObjectByType<UIDocument>();
            Check(document && document.visualTreeAsset && document.panelSettings &&
                document.GetComponent<PassHUD>().journey == route, "HUD is not linked to the journey");
            var doors = route.consist.GetComponentsInChildren<PassDoor>(true);
            Check(doors.Count(d => d.kind == PassDoorKind.Passenger) == 18 &&
                doors.Count(d => d.kind == PassDoorKind.Service) == 9 &&
                doors.Count(d => d.kind == PassDoorKind.Chase) == 1 &&
                doors.Count(d => d.kind == PassDoorKind.Escape) == 1 &&
                doors.Count(d => d.kind == PassDoorKind.Gangway) == 13 &&
                doors.Count(d => d.kind == PassDoorKind.Lavatory) == 1 &&
                doors.Count(d => d.kind == PassDoorKind.Galley) == 1 &&
                doors.Count(d => d.kind == PassDoorKind.Cargo) == 8,
                "Missing a vestibule, loading, gangway or lavatory door");
            var pulls=route.consist.GetComponentsInChildren<PassDoorTarget>(true);
            var sliders=route.consist.GetComponentsInChildren<Car07SlidingDoor>(true);
            Check(sliders.Length==13 && pulls.Length==doors.Length+sliders.Length &&
                doors.All(d=>pulls.Count(p=>p.hinge==d)==1) && sliders.All(d=>pulls.Count(p=>p.slider==d)==1),
                "Every door must have a registered fixed pull");
            Check(route.consist.GetComponentsInChildren<PassPickup>(true).Any(p => p.itemId == "parachute"),
                "Missing movable parachute bag");
            var cars=Enumerable.Range(1,9).Select(n => route.consist.Cast<Transform>().Single(t =>
                PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject) == FleetImport.PrefabPath(n))).ToArray();
            for (int i=0; i<8; i++) {
                var rear=Anchor(cars[i],"Socket_B");
                var front=Anchor(cars[i+1],"Socket_A");
                Check(Vector3.Distance(rear.position,front.position)<.014f, "Gap after platform car "+(i+1));
            }
            Check(Mathf.Abs(Anchor(cars[0],"Socket_A").position.z) < .015f, "Route origin differs from locomotive nose");
            foreach (var root in scene.GetRootGameObjects()) {
                foreach (var tr in root.GetComponentsInChildren<Transform>(true))
                    Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(tr.gameObject)==0, "Unresolved script on "+tr.name);
            }
            Physics.SyncTransforms();
            Check(Physics.Raycast(new Vector3(-3.3f,4,-1000),Vector3.down,out var platform,4) &&
                Mathf.Abs(platform.point.y-1.375f)<.13f,"Platform walking surface must meet carriage floors");
            Check(!Physics.Raycast(new Vector3(0,2,-2135),Vector3.down,5f),"Visually broken bridge still has a deck collider");
            Debug.Log("PASS SAVED SCENE VERIFIED: nine vehicles, eight gangways, 65 independently operable leaves, parcel rack and HUD.");
        }

        public static void Export()
        {
            CheckSavedScene();
            var folder=Path.Combine(FleetImport.Checkout,"Releases");
            Directory.CreateDirectory(folder);
            var name=Path.Combine(folder,"Winterbourne.unitypackage");
            var tmp=Path.Combine(folder,".winterbourne-pass-"+Guid.NewGuid().ToString("N")+".unitypackage");
            try {
                AssetDatabase.ExportPackage("Assets/Transperceneige",tmp,ExportPackageOptions.Recurse);
                Check(File.Exists(tmp) && new FileInfo(tmp).Length>100000, "Did not export the train and pass");
                if (File.Exists(name)) File.Replace(tmp,name,null); else File.Move(tmp,name);
                Debug.Log("PASS RELEASE "+new FileInfo(name).Length+" bytes");
            }
            finally { if (File.Exists(tmp)) File.Delete(tmp); }
        }
    }
}
