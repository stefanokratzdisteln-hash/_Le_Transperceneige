using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Winterbourne.Pass;
using Object=UnityEngine.Object;
using static Winterbourne.Editor.StationFabrication;

namespace Winterbourne.Editor
{
    // All positions refer to the supporting furniture or the lining of a case.
    // A drawer carries its contents; a hinged lid leaves them in the bottom half.
    public sealed class PassLootFixtures : IDisposable
    {
        const string Art="Assets/Transperceneige/Pass/Valuables/";
        readonly GameObject workshop=new GameObject("Unpacked case patterns");
        readonly Dictionary<string,GameObject> patterns=new Dictionary<string,GameObject>();
        readonly Material walnut,leather,cloth,brass,enamel,iron;

        public PassLootFixtures()
        {
            Material Finish(string path) => AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+path+".mat");
            walnut=Finish("Reading room walnut"); leather=Finish("Heritage/Seat hide");
            cloth=Finish("Heritage/Leather"); brass=Finish("Ticket brass"); iron=Finish("Heritage/Graphite");
            enamel=AssetDatabase.LoadAssetAtPath<Material>(Art+"Cobalt.mat");
            PassBuild.Check(walnut && leather && cloth && brass && iron && enamel,"Missing a fitted case finish");
        }

        public void Dispose()=>Object.DestroyImmediate(workshop);
        static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);

        static void Collider(Transform parent,Vector3 center,Vector3 size)
        {
            var solid=parent.gameObject.AddComponent<BoxCollider>();
            solid.center=center; solid.size=size;
        }

        static void Rim(StationFabrication mesh,Material mat,float w,float d,float y,float depth=.007f)
        {
            for(int side=-1;side<=1;side+=2)
            {
                mesh.Box(mat,V(side*(w-depth)/2,y,0),V(depth,depth,d));
                mesh.Box(mat,V(0,y,side*(d-depth)/2),V(w-depth*2,depth,depth));
            }
        }

        public Transform Case(Transform parent,string name,Vector3 local,float yaw,string style,bool locked=false)
        {
            if(!patterns.TryGetValue(style,out var template))
            {
                template=BuildPattern(style);
                patterns.Add(style,template);
            }
            var go=Object.Instantiate(template,parent,false);
            go.name=name;
            go.transform.SetLocalPositionAndRotation(local,Quaternion.Euler(0,yaw,0));
            var interaction=go.GetComponent<PassLootContainer>();
            if(interaction) interaction.locked=locked;
            return go.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Contents");
        }

        GameObject BuildPattern(string style)
        {
            var root=Child(workshop.transform,"Case "+style,Vector3.zero);
            var fixedMesh=new StationFabrication();
            var movingMesh=new StationFabrication();
            var leaf=Child(root.transform,"Opening leaf",Vector3.zero).transform;
            Transform contentsParent=root.transform;
            Vector3 lining;
            PassLootContainer action=null;
            if(style!="watch")
            {
                action=root.AddComponent<PassLootContainer>();
                action.movingPart=leaf;
            }
            if(style=="cabinet")
            {
                const float w=.47f,d=.30f,h=.39f;
                fixedMesh.Box(walnut,V(0,.014f,0),V(w,.028f,d));
                fixedMesh.Box(cloth,V(0,.029f,0),V(w-.026f,.005f,d-.018f));
                fixedMesh.Box(walnut,V(0,h,d*.46f),V(w,.018f,d*.095f));
                fixedMesh.Box(walnut,V(0,h/2,d*.47f),V(w,h,.018f));
                fixedMesh.Box(walnut,V(0,h,0),V(w+.015f,.023f,d+.012f));
                foreach(int side in new[]{-1,1})
                    fixedMesh.Box(walnut,V(side*(w-.017f)/2,h/2,0),V(.017f,h,d));
                Rim(fixedMesh,brass,w,d,.035f);
                leaf.localPosition=V(-w/2+.011f,.031f,-d/2);
                var centre=V((w-.024f)/2,(h-.043f)/2,0);
                movingMesh.Box(walnut,centre,V(w-.024f,h-.044f,.018f));
                movingMesh.Box(enamel,centre+Vector3.back*.011f,V(w-.060f,h-.085f,.006f));
                for(int i=0;i<5;i++)
                    movingMesh.Box(brass,V(.071f+i*.067f,.169f,-.017f),V(.004f,.225f,.004f),Quaternion.Euler(0,0,20));
                movingMesh.Box(brass,V(w-.074f,.164f,-.025f),V(.019f,.082f,.025f));
                Collider(root.transform,V(0,h/2,d/2-.010f),V(w,h,.023f));
                Collider(leaf,centre,V(w-.024f,h-.044f,.022f));
                action.openEuler=V(0,104,0);action.labelKey="search.cabinet";
                lining=V(0,.032f,0);
            }
            else if(style=="drawer")
            {
                const float w=.435f,h=.142f,d=.272f;
                fixedMesh.Box(walnut,V(0,h,0),V(w+.008f,.018f,d+.012f));
                fixedMesh.Box(walnut,V(0,.010f,0),V(w,.020f,d));
                foreach(int side in new[]{-1,1}) fixedMesh.Box(walnut,V(side*w*.48f,h/2,0),V(.018f,h,d));
                fixedMesh.Box(walnut,V(0,h/2,d*.48f),V(w,h,.013f));
                movingMesh.Box(cloth,V(0,.026f,0),V(w-.041f,.012f,d-.022f));
                foreach(int side in new[]{-1,1}) movingMesh.Box(leather,V(side*.193f,.063f,0),V(.011f,.077f,d-.023f));
                movingMesh.Box(walnut,V(0,.076f,-d/2),V(w-.022f,.111f,.019f));
                movingMesh.Box(brass,V(0,.080f,-d/2-.012f),V(.087f,.037f,.005f));
                movingMesh.Beam(brass,V(-.026f,.078f,-.157f),V(.026f,.078f,-.157f),.007f,.007f);
                Collider(root.transform,V(0,h,0),V(w,.018f,d));
                Collider(leaf,V(0,.076f,-d/2),V(w-.022f,.111f,.021f));
                action.openOffset=V(0,0,-.210f);action.labelKey="search.drawer";
                contentsParent=leaf;lining=V(0,.033f,-.02f);
            }
            else if(style=="trunk" || style=="watch")
            {
                bool small=style=="watch";
                float w=small?.187f:.478f,d=small?.138f:.308f,h=small?.027f:.24f;
                float t=small?.006f:.014f;
                fixedMesh.Box(walnut,V(0,t*.5f,0),V(w,t,d));
                fixedMesh.Box(small?cloth:leather,V(0,t,0),V(w-t*2,.004f,d-t*2));
                foreach(int side in new[]{-1,1})
                {
                    fixedMesh.Box(leather,V(side*(w-t)/2,h/2,0),V(t,h,d));
                    fixedMesh.Box(leather,V(0,h/2,side*(d-t)/2),V(w-2*t,h,t));
                    // Brass corners, edge welting, leather strap and clasp.
                    for(int end=-1;end<=1;end+=2)
                        fixedMesh.Box(brass,V(side*(w-.022f)/2,.012f,end*(d-t)/2),V(.022f,.022f,t*1.2f));
                    if(!small)
                    {
                        fixedMesh.Box(walnut,V(side*w*.32f,h/2,-d*.5f),V(.027f,h,.010f));
                        fixedMesh.Box(brass,V(side*w*.32f,h-.037f,-d*.52f),V(.038f,.041f,.012f));
                    }
                }
                Rim(fixedMesh,brass,w,d,h,small?.0025f:.005f);
                leaf.localPosition=V(0,h,d/2);
                var lid=V(0,t*.6f,-d*.5f);
                movingMesh.Box(leather,lid,V(w,t*2,d));
                movingMesh.Box(cloth,lid+Vector3.down*t*1.2f,V(w-3*t,.003f,d-3*t));
                for(int side=-1;side<=1;side+=2)
                {
                    movingMesh.Box(brass,V(side*(w-t)/2,0,-d/2),V(t,t,d));
                    if(!small) movingMesh.Box(walnut,V(side*w*.32f,t*1.8f,-d/2),V(.027f,t*.42f,d));
                }
                Collider(root.transform,V(0,h*.49f,-d/2+t/2),V(w,h,t));
                Collider(leaf,lid,V(w,t*2,d));
                if(small) leaf.localRotation=Quaternion.Euler(109f,0,0);
                else {action.openEuler=V(108f,0,0);action.labelKey="search.case";}
                lining=V(0,t+.0026f,small?-.022f:0);
            }
            else throw new ArgumentException("Unknown case style "+style);
            fixedMesh.Finish(root.transform,"Antique "+style+" carcass");
            movingMesh.Finish(leaf,"Antique "+style+" leaf");
            Child(contentsParent,"Contents",lining);
            return root;
        }

        public static PassPickup Item(Transform parent,string itemId,string siteId,string group,Vector3 local,float yaw=0)
        {
            var model=AssetDatabase.LoadAssetAtPath<GameObject>(Art+itemId+".fbx");
            PassBuild.Check(model && PassLootCatalogue.Contains(itemId),"Unknown or unmodelled antique: "+itemId);
            var root=Child(parent,siteId,local);
            root.transform.localRotation=Quaternion.Euler(0,yaw,0);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(model,root.transform);
            var renderers=instance.GetComponentsInChildren<MeshRenderer>();
            PassBuild.Check(renderers.Length>0,"No visible jewellery for "+itemId);
            // Compute the interaction volume from the imported mesh in the site's own
            // coordinates. World AABBs would expand a long pen each time its case turns.
            bool first=true;
            var bounds=new Bounds();
            foreach(var renderer in renderers)
            {
                var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
                var childToRoot=root.transform.worldToLocalMatrix*renderer.transform.localToWorldMatrix;
                foreach(int sx in new[]{-1,1})
                    foreach(int sy in new[]{-1,1})
                        foreach(int sz in new[]{-1,1})
                        {
                            var corner=mesh.bounds.center+Vector3.Scale(mesh.bounds.extents,V(sx,sy,sz));
                            var p=childToRoot.MultiplyPoint3x4(corner);
                            if(first){bounds=new Bounds(p,Vector3.zero);first=false;}else bounds.Encapsulate(p);
                        }
            }
            var aim=root.AddComponent<BoxCollider>();
            aim.isTrigger=true;aim.center=bounds.center;
            aim.size=Vector3.Max(bounds.size+Vector3.one*.010f,V(.085f,.036f,.085f));
            var pickup=root.AddComponent<PassPickup>();
            pickup.itemId=itemId;pickup.siteId=siteId;pickup.rollGroup=group;pickup.label="item."+itemId;
            return pickup;
        }
    }
}
