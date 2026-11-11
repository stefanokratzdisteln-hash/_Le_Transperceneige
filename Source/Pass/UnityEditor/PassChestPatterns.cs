using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using static Winterbourne.Editor.StationFabrication;
using Winterbourne.Pass;

namespace Winterbourne.Editor
{
    public sealed partial class PassLootFixtures
    {
        const string CabinetArt = "Assets/Transperceneige/Pass/Cases/";

        // The four authored axis markers keep hinge coordinates in coach metres,
        // independent of the FBX importer's axis and unit conversions.
        static Transform LoadChestArt(Transform owner, string name)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(CabinetArt + name + ".fbx");
            PassBuild.Check(asset, "Missing chest artwork " + name);
            var handed = Child(owner, name + " handedness", Vector3.zero).transform;
            var basis = Child(handed, name + " basis", Vector3.zero).transform;
            var model = Object.Instantiate(asset, basis, false).transform;
            var nodes = model.GetComponentsInChildren<Transform>(true);
            Vector3 Axis(string marker) => basis.InverseTransformPoint(nodes.Single(t => t.name == name+"_"+marker).position);
            Vector3 origin = Axis("Origin"), back = Axis("Back")-origin, up = Axis("Up")-origin;
            Vector3 right = Axis("Right")-origin;
            PassBuild.Check(back.sqrMagnitude>1e-7f && up.sqrMagnitude>1e-7f, "Invalid authored chest basis");
            basis.localRotation = Quaternion.Inverse(Quaternion.LookRotation(back,up));
            basis.localScale = Vector3.one * (.1f/back.magnitude);
            basis.localPosition = -(basis.localRotation * origin) * basis.localScale.x;
            if ((basis.localRotation*right).x < 0) handed.localScale = new Vector3(-1,1,1);
            foreach (var marker in nodes.Where(t => t.name.StartsWith(name+"_", StringComparison.Ordinal)))
                Object.DestroyImmediate(marker.gameObject);
            return model;
        }

        static Transform ArtHinge(Transform owner, Transform model, string name, Vector3 pivot)
        {
            var part = model.GetComponentsInChildren<MeshRenderer>(true).Single(m => m.transform.name == name).transform;
            var hinge = Child(owner, name+" pivot", pivot).transform;
            part.SetParent(hinge, true);
            return hinge;
        }

        // Aim a narrow part of the existing room luminaire's spill onto luggage
        // in its shadow. Its emitter stays at the physical carriage or booking
        // office light, so neither a prop light nor a camera lamp is required.
        static void IlluminateParcel(Transform room,Transform cabinet)
        {
            var face=cabinet.TransformPoint(V(0,.29f,-.17f));
            var near=room.GetComponentsInChildren<Light>(true).Where(l=>l.enabled &&
                !l.GetComponentInParent<PassLootContainer>() && l.type!=LightType.Directional &&
                l.transform.position.y>face.y+.40f && l.intensity>0)
                .OrderBy(l=>(l.transform.position-face).sqrMagnitude).FirstOrDefault();
            if(!near)return;
            Vector3 mount=cabinet.InverseTransformPoint(near.transform.position);
            float distance=(near.transform.position-face).magnitude;
            var fixture=Child(cabinet,"Parcel illumination from "+near.name,mount).transform;
            fixture.rotation=Quaternion.LookRotation((face-near.transform.position).normalized,Vector3.up);
            var beam=fixture.gameObject.AddComponent<Light>();
            beam.type=LightType.Spot;beam.color=near.color;beam.spotAngle=55;beam.innerSpotAngle=36;
            beam.intensity=.29f*distance*distance;beam.range=distance+1.6f;
            beam.shadows=LightShadows.None;beam.renderMode=LightRenderMode.ForcePixel;
        }

        static void InspectionLamp(Transform parent,string name,Vector3 mount,Vector3 aim,
            float angle,float intensity,float range)
        {
            var fixture=Child(parent,name,mount).transform;
            fixture.localRotation=Quaternion.LookRotation((aim-mount).normalized,Vector3.up);
            var light=fixture.gameObject.AddComponent<Light>();
            light.type=LightType.Spot;light.spotAngle=angle;light.innerSpotAngle=angle*.72f;
            light.intensity=intensity;light.range=range;light.color=new Color(1,.81f,.58f);
            light.shadows=LightShadows.None;light.renderMode=LightRenderMode.ForcePixel;
        }

        // Joint centres match the phalanges in railway_chests.py. Each digit
        // pivots at its knuckle, middle joint and distal joint instead of
        // moving a rigid mitten. Coordinates here are authored wrist metres.
        static Transform[] ArtFingers(Transform palm,Transform art,int side)
        {
            var joints=new Transform[15];
            string[] names={"Index","Middle","Ring","Little","Thumb"};
            float[] xs={-.030f,-.0097f,.0111f,.0289f}, starts={.090f,.095f,.092f,.085f},
                    lengths={.074f,.080f,.073f,.055f};
            for(int digit=0;digit<5;digit++)
            {
                Vector3[] pivots;
                if(digit==4)
                    pivots=new[]{V(-.027f*side,-.003f,.042f),V(-.0515f*side,-.012f,.060f),V(-.050f*side,-.030f,.083f)};
                else
                {
                    float x=xs[digit], y=starts[digit], len=lengths[digit], fan=digit==0?-.002f:.002f;
                    pivots=new[]{V(x*side,.005f,y),V((x+fan)*side,.007f,y+len*.43f),V((x+fan*1.45f)*side,-.002f,y+len*.72f)};
                }
                string digitName=(side<0?"Left":"Right")+names[digit];
                var digitBones=new Transform[4];
                digitBones[0]=palm;
                for(int joint=0;joint<3;joint++)
                {
                    var offset=joint==0?pivots[0]:pivots[joint]-pivots[joint-1];
                    var next=Child(digitBones[joint],digitName+" joint "+joint,offset).transform;
                    digitBones[joint+1]=next;
                    joints[digit*3+joint]=next;
                }
                SkinDigit(palm,art,digitName+"Skin",digitBones,pivots);
            }
            return joints;
        }

        // Build the smooth deformation once at edit time. Each hand shares its
        // ten small imported skin surfaces across every instance of both cases.
        static void SkinDigit(Transform wrist,Transform art,string name,Transform[] bones,Vector3[] joints)
        {
            var solid=art.GetComponentsInChildren<MeshRenderer>(true).Single(r=>r.name==name);
            var source=solid.GetComponent<MeshFilter>().sharedMesh;
            string path=StationFabrication.Folder+"/Passenger glove "+name+".asset";
            // Imported/compressed FBX meshes may keep packed vertex streams.
            // Write to a clean mesh explicitly so old import remaps cannot mix
            // former vertex indices with the current finger topology on rebake.
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool create=mesh==null;
            if(create)mesh=new Mesh{name="Passenger glove "+name};
            else { mesh.Clear(false);mesh.ClearBlendShapes(); }
            var intoWrist=wrist.worldToLocalMatrix*solid.localToWorldMatrix;
            var vertices=source.vertices;
            var normals=source.normals;
            var normalMatrix=intoWrist.inverse.transpose;
            var weights=new BoneWeight[vertices.Length];
            Vector3 axis=(joints[2]-joints[0]).normalized;
            float first=Vector3.Dot(joints[1]-joints[0],axis);
            float second=Vector3.Dot(joints[2]-joints[0],axis);
            for(int i=0;i<vertices.Length;i++)
            {
                vertices[i]=intoWrist.MultiplyPoint3x4(vertices[i]);
                normals[i]=normalMatrix.MultiplyVector(normals[i]).normalized;
                float station=Vector3.Dot(vertices[i]-joints[0],axis);
                int pair=station<first*.5f?0:station<(first+second)*.5f?1:2;
                float centre=pair==0?0:pair==1?first:second;
                float flex=Mathf.SmoothStep(0,1,Mathf.InverseLerp(centre-.0075f,centre+.0075f,station));
                weights[i]=new BoneWeight{boneIndex0=pair,boneIndex1=pair+1,weight0=1-flex,weight1=flex};
            }
            mesh.vertices=vertices;
            mesh.normals=normals;
            mesh.uv=source.uv;
            mesh.subMeshCount=source.subMeshCount;
            for(int sub=0;sub<source.subMeshCount;sub++)
            {
                var indices=source.GetTriangles(sub,true);
                if(intoWrist.determinant<0)
                    for(int i=0;i<indices.Length;i+=3){int temp=indices[i+1];indices[i+1]=indices[i+2];indices[i+2]=temp;}
                mesh.SetTriangles(indices,sub,false);
            }
            mesh.boneWeights=weights;
            mesh.bindposes=bones.Select(b=>b.worldToLocalMatrix*wrist.localToWorldMatrix).ToArray();
            mesh.RecalculateBounds();mesh.RecalculateTangents();
            var hand=Child(wrist,name,Vector3.zero).AddComponent<SkinnedMeshRenderer>();
            hand.sharedMaterials=solid.sharedMaterials;
            hand.bones=bones;hand.rootBone=wrist;hand.quality=SkinQuality.Bone4;
            if(create)AssetDatabase.CreateAsset(mesh,path);
            else EditorUtility.SetDirty(mesh);
            hand.sharedMesh=mesh;
            hand.localBounds=new Bounds(V(0,0,.068f),V(.20f,.20f,.24f));
            Object.DestroyImmediate(solid.gameObject);
        }

        GameObject TapChest(string style)
        {
            bool gold = style == "treasuryChest";
            var root = Child(workshop.transform, style, Vector3.zero);
            Transform t = root.transform;
            var caseArt = LoadChestArt(t, gold ? "TreasuryChest" : "ParcelChest");
            var action = root.AddComponent<PassLootContainer>();
            action.tapsRequired = gold ? 8 : 3;
            action.locked = true;
            action.labelKey = gold ? "search.goldChest" : "search.woodChest";
            float w=gold?1.12f:.68f;
            float h=gold?.73f:.47f;
            float d=gold?.49f:.41f;
            Collider(t,V(0,.036f,0),V(w,.072f,d));
            Collider(t,V(0,h*.5f,d*.5f-.008f),V(w,h,.018f));
            foreach(int sign in new[]{-1,1})
                Collider(t,V(sign*(w*.5f-.022f),h*.46f,.015f),V(.035f,h*.9f,d-.03f));
            if(gold)
            {
                InspectionLamp(t,"Treasury picture light",V(0,.615f,-.311f),V(0,.29f,-.263f),110,.009f,1.3f);
                InspectionLamp(t,"Lined tray lamp",V(.377f,.616f,-.111f),V(.375f,.095f,.020f),104,.076f,.70f);
                InspectionLamp(t,"Left recess lamp",V(-.377f,.616f,-.111f),V(-.375f,.095f,.020f),104,.047f,.70f);
                Collider(t,V(0,.684f,0),V(w,.089f,.505f));
                Collider(t,V(0,.354f,-.238f),V(.460f,.569f,.060f));
                action.movingPart=ArtHinge(t,caseArt,"TreasuryLeft",V(-.524f,.35f,-.206f));
                action.otherDoor=ArtHinge(t,caseArt,"TreasuryRight",V(.524f,.35f,-.206f));
                action.openEuler=V(0,102,0);
                Collider(action.movingPart,V(.145f,0,.003f),V(.282f,.573f,.036f));
                Collider(action.otherDoor,V(-.145f,0,.003f),V(.282f,.573f,.036f));
                action.lockWork=ArtHinge(t,caseArt,"TreasuryLock",V(0,.345f,-.288f));
                action.gripLocal=V(0,.345f,-.30f);
                action.braceLocal=V(-.115f,.440f,-.323f);
                action.knobInLock=V(.113f,-.017f,-.054f);
            }
            else
            {
                Collider(t,V(0,.159f,-.194f),V(w,.315f,.028f));
                action.movingPart=ArtHinge(t,caseArt,"ParcelLid",V(0,.308f,.197f));
                action.openEuler=V(104,0,0);
                Collider(action.movingPart,V(0,.062f,-.197f),V(.670f,.124f,.405f));
                action.lockWork=ArtHinge(t,caseArt,"ParcelLatch",V(.184f,.342f,-.208f));
                action.leftHasp=ArtHinge(t,caseArt,"ParcelLeftLatch",V(-.184f,.342f,-.208f));
                // The buckles travel with the lid once both tongues release.
                action.leftHasp.SetParent(action.movingPart,true);
                action.lockWork.SetParent(action.movingPart,true);
                action.gripLocal=V(0,.312f,-.222f);
                action.braceLocal=V(-.184f,.314f,-.224f);
                action.knobInLock=V(0,-.029f,-.019f);
            }
            var rig = Child(t, "Passenger's hands", Vector3.zero).transform;
            var gloveArt = LoadChestArt(rig, "CabinetGloves");
            action.leftGlove=ArtHinge(rig,gloveArt,"LeftGlove",Vector3.zero);
            action.rightGlove=ArtHinge(rig,gloveArt,"RightGlove",Vector3.zero);
            action.leftSleeve=ArtHinge(rig,gloveArt,"LeftSleeve",Vector3.zero);
            action.rightSleeve=ArtHinge(rig,gloveArt,"RightSleeve",Vector3.zero);
            action.leftUpper=ArtHinge(rig,gloveArt,"LeftUpper",Vector3.zero);
            action.rightUpper=ArtHinge(rig,gloveArt,"RightUpper",Vector3.zero);
            action.leftDigits=ArtFingers(action.leftGlove,gloveArt,-1);
            action.rightDigits=ArtFingers(action.rightGlove,gloveArt,1);
            action.handsRoot=rig.gameObject;
            rig.gameObject.SetActive(false);
            var voice=root.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=1;
            voice.minDistance=.65f;voice.maxDistance=5f;voice.dopplerLevel=0;
            Child(t,"Contents",gold?V(.375f,.080f,0):V(0,.075f,0));
            return root;
        }
    }
}
