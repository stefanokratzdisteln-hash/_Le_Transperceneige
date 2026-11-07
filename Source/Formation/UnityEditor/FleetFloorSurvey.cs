using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using Winterbourne.Pass;

namespace Winterbourne.Editor
{
    /// Reachable interior deck area in square metres. Use the same sleeper passenger
    /// capsule and 10 cm raster for both formations. Flood from the car's gangway.
    public static class FleetFloorSurvey
    {
        const float Cell=.10f;
        // Number of reachable 0.1 m cells in the passenger areas on 720baba.
        static readonly int[] Original={398,594,3174,2386,2731,2178,2291,2246,2991};
        public static void Measure()
        {
            var scene=EditorSceneManager.OpenScene(FleetImport.Argument("-surveyScene")??
                "Assets/Transperceneige/Pass/WinterbournePass.unity");
            bool measuringOriginal=Environment.GetCommandLineArgs().Contains("-baselineFloor");
            var capsule=UnityEngine.Object.FindAnyObjectByType<CharacterController>();
            foreach(var door in UnityEngine.Object.FindObjectsByType<PassDoor>(FindObjectsSortMode.None))
                if (door.kind==PassDoorKind.Gangway || door.kind==PassDoorKind.Lavatory || door.kind==PassDoorKind.Galley)
                    door.SetOpen(true,true);
            capsule.enabled=false;
            Physics.SyncTransforms();
            for (int n=1;n<=9;n++)
            {
                var car=scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<Transform>())
                    .Single(t=>UnityEditor.PrefabUtility.GetNearestPrefabInstanceRoot(t.gameObject)==t.gameObject &&
                        UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject)==FleetImport.PrefabPath(n));
                var all=car.GetComponentsInChildren<Transform>();
                float first=car.InverseTransformPoint(all.Single(t=>t.name=="Socket_A").position).z;
                float last=car.InverseTransformPoint(all.Single(t=>t.name=="Socket_B").position).z;
                var valid=new HashSet<Vector2Int>();
                var seed=Vector2Int.zero;
                float nearest=float.PositiveInfinity;
                for(int z=Mathf.CeilToInt(first/Cell);z<last/Cell;z++)
                    for(int x=-27;x<=27;x++)
                    {
                        var p=car.TransformPoint(new Vector3(x*Cell,1.38f,z*Cell));
                        if(!Physics.Raycast(p+Vector3.up*.18f,Vector3.down,out var floor,.26f,~0,QueryTriggerInteraction.Ignore) ||
                            !floor.transform.IsChildOf(car) || floor.normal.y<.85f || Mathf.Abs(floor.point.y-p.y)>.059f) continue;
                        // Only the interior floor supports count, never side boarding steps or rooftop.
                        string deck=floor.collider.name;
                        if(!(deck=="Coach floor" || deck=="Tender_deck" || deck=="Cab_footplate" || deck.StartsWith("Extension floor",StringComparison.Ordinal)))continue;
                        var lower=floor.point+Vector3.up*(capsule.radius+.023f);
                        var upper=floor.point+Vector3.up*(capsule.height-capsule.radius+.023f);
                        if(Physics.CheckCapsule(lower,upper,capsule.radius,~0,QueryTriggerInteraction.Ignore))continue;
                        var grid=new Vector2Int(x,z);
                        valid.Add(grid);
                        float entry=n==1?last:first;
                        float d=(grid.y*Cell-entry)*(grid.y*Cell-entry)+grid.x*grid.x*Cell*Cell;
                        if(d<nearest){nearest=d;seed=grid;}
                    }
                var front=new Queue<Vector2Int>();
                int count=0;
                if(valid.Remove(seed))front.Enqueue(seed);
                while(front.Count>0)
                {
                    var p=front.Dequeue();count++;
                    foreach(var axis in new[]{Vector2Int.right,Vector2Int.left,Vector2Int.up,Vector2Int.down})
                        if(valid.Remove(p+axis)) front.Enqueue(p+axis);
                }
                FleetImport.Require(count>0,"No connected floor on "+car.name);
                FleetImport.Require(measuringOriginal || count>=Original[n-1]*2,
                    car.name+" requires "+(Original[n-1]*2)+" connected floor cells, measured "+count);
                Debug.Log("FLEET FLOOR "+car.name+" "+
                    (count*Cell*Cell).ToString("F2",CultureInfo.InvariantCulture)+" m2; sample "+seed+"; unreachable cells "+valid.Count);
            }
        }
    }
}
