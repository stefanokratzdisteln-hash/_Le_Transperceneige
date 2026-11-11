using System;
using System.Linq;
using UnityEngine;
using UnityEditor.SceneManagement;
using Winterbourne.Pass;
using Object=UnityEngine.Object;

namespace Winterbourne.Editor
{
    // Verify that a passenger can reach the lock face, even when a randomized
    // grand prize belongs to a closed cabinet and starts disabled in the scene.
    internal static class PassChestAccessReview
    {
        internal static void Check(PassJourney route,Transform house,PassPickup[] articles)
        {
            var stores = articles.Select(p=>p.GetComponentInParent<PassLootContainer>(true))
                .Where(c=>c && c.TapChest).Distinct().OrderBy(c=>c.name).ToArray();
            var prizes = articles.Where(p=>PassLootCatalogue.GrandTreasures.Contains(p.itemId)).ToArray();
            PassBuild.Check(stores.Length==6 && prizes.Length==15,"Expected six working cases and fifteen rare deposits");
            foreach(var treasury in stores.Where(c=>c.otherDoor)) {
                var inside=treasury.GetComponentsInChildren<PassPickup>(true);
                PassBuild.Check(PassLootCatalogue.GrandTreasures.All(id=>inside.Count(p=>p.itemId==id)==1),
                    "A rare deposit is missing from "+treasury.name);
            }
            var body=route.walker.GetComponent<CharacterController>();
            var passenger=body.transform;
            var original=passenger.position;
            bool enabled=body.enabled;body.enabled=false;
            try {
                foreach(var chest in stores)
                {
                    Transform room=house;
                    bool onboard=chest.transform.IsChildOf(route.consist);
                    if(onboard){room=chest.transform;while(room.parent!=route.consist)room=room.parent;}
                    float floor=onboard?1.405f:.025f;
                    var entrance=onboard?new Vector3(0,floor,-13.65f):new Vector3(-.25f,floor,7.5f);
                    passenger.position=room.TransformPoint(entrance);Physics.SyncTransforms();
                    var grid=onboard?new PassWalkGrid(room,body,-2.55f,2.55f,-14.7f,14.7f,floor,true):
                        new PassWalkGrid(room,body,-9.15f,6.4f,-12.0f,14.0f,floor,false);
                    var grip=chest.transform.TransformPoint(chest.gripLocal);
                    bool Sees(Vector3 eye,bool working){
                        var ray=grip-eye;
                        float range=working?Mathf.Min(route.walker.interactionDistance,ray.magnitude+.12f):route.walker.interactionDistance;
                        return Physics.Raycast(eye,ray.normalized,out var hit,range,
                            Physics.DefaultRaycastLayers,QueryTriggerInteraction.Collide) &&
                            hit.collider.GetComponentInParent<PassLootContainer>()==chest;
                    }
                    bool AtLock(Vector3 foot) {
                        var local=chest.transform.InverseTransformPoint(foot);
                        float depth=chest.otherDoor?-.735f:-.68f;
                        if(Mathf.Abs(local.x)>.21f || Mathf.Abs(local.z-depth)>.13f)return false;
                        // The same place must permit the initial standing interaction
                        // as well as the crouched working position at the physical lock.
                        return Sees(foot+Vector3.up*route.walker.eyeCamera.transform.localPosition.y,false) &&
                            Sees(foot+Vector3.up*.86f,true);
                    }
                    var path=grid.Path(AtLock);
                    PassBuild.Check(path!=null && path.Count>0,"No connected standing approach and crouched grip for "+chest.name);
                    Debug.Log("PASS CHEST ACCESS: "+chest.name+" reached at "+room.TransformPoint(path.Last()).ToString("F2"));
                }
            }
            finally{passenger.position=original;body.enabled=enabled;Physics.SyncTransforms();}
        }
    }
}
