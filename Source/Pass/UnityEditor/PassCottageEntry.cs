using UnityEditor;
using UnityEngine;
using Winterbourne.Pass;
using static Winterbourne.Editor.StationFabrication;

namespace Winterbourne.Editor
{
    public static partial class PassPortalBuild
    {
        const float FacadeX=4.12f, DoorNearEdge=-2.83f, DoorFarEdge=-1.51f, DoorHeight=2.31f;
        const float DoorZ=(DoorNearEdge+DoorFarEdge)*.5f;

        static void FrontWallSegment(Transform hut,StationFabrication room,Palette m,float from,float to)
        {
            float z=(from+to)*.5f;
            float width=to-from;
            room.Box(m.cream,new Vector3(FacadeX,1.64f,z),new Vector3(.23f,3.28f,width));
            room.Box(m.green,new Vector3(FacadeX,.57f,z),new Vector3(.234f,1.14f,width));
            Solid(hut,"Front wall beside entrance",new Vector3(FacadeX,1.64f,z),new Vector3(.25f,3.28f,width));
        }

        static PassDoor MakeCottageEntry(Transform hut,Palette m)
        {
            var entrance=Child(hut,"Cottage entrance",new Vector3(FacadeX,0,DoorZ)).transform;
            var frame=new StationFabrication();
            const float jamb=.074f, frameWidth=DoorFarEdge-DoorNearEdge;
            float insideWidth=frameWidth-2*jamb;
            foreach (int side in new[]{-1,1})
            {
                var post=new Vector3(.007f,(DoorHeight-jamb)/2,side*(frameWidth-jamb)/2);
                var size=new Vector3(.30f,DoorHeight-jamb,jamb);
                frame.Box(m.oak,post,size);
                Solid(entrance,"Door lining",post,size);
                frame.Box(m.green,new Vector3(.138f,1.16f,side*(frameWidth/2+.035f)),new Vector3(.052f,2.32f,.121f));
            }
            var head=new Vector3(.01f,DoorHeight-jamb/2,0);
            frame.Box(m.oak,head,new Vector3(.30f,jamb,frameWidth));
            Solid(entrance,"Door frame head",head,new Vector3(.30f,jamb,frameWidth));
            // A short lean-to and triangular brackets put the entrance in silhouette beside the sash.
            var pitch=Quaternion.Euler(0,0,-10);
            frame.Box(m.oak,new Vector3(.55f,2.86f,0),new Vector3(1.24f,.12f,1.96f),pitch);
            frame.Box(m.slate,new Vector3(.55f,2.938f,0),new Vector3(1.32f,.052f,2.05f),pitch);
            frame.Box(m.snow,new Vector3(.55f,2.977f,0),new Vector3(1.33f,.037f,2.07f),pitch);
            for(int side=-1;side<=1;side+=2)
            {
                float z=side*.83f;
                frame.Beam(m.oak,new Vector3(.14f,2.36f,z),new Vector3(.93f,2.72f,z),.072f,.075f);
                frame.Box(m.oak,new Vector3(.14f,2.52f,z),new Vector3(.087f,.62f,.086f));
            }
            frame.Box(m.copper,new Vector3(.18f,1.84f,.96f),new Vector3(.081f,.29f,.17f));
            frame.Beam(m.copper,new Vector3(.15f,1.98f,.96f),new Vector3(.44f,2.07f,.96f),.041f,.05f);
            var lantern=new Vector3(.44f,1.87f,.96f);
            frame.Box(m.amber,lantern,new Vector3(.19f,.25f,.19f));
            foreach (int end in new[]{-1,1})
            {
                frame.Box(m.copper,lantern+Vector3.up*(end*.14f),new Vector3(.23f,.041f,.23f));
                foreach (int side in new[]{-1,1})
                    frame.Box(m.copper,lantern+new Vector3(side*.095f,0,end*.095f),new Vector3(.016f,.26f,.016f));
            }
            Lamp(entrance,"Porch lantern",lantern+Vector3.right*.12f,new Color(1,.77f,.48f),1.15f,5.1f);
            BuildPorch(hut,entrance,frame,m);
            frame.Finish(entrance,"Cottage porch canopy and steps");

            float leafWidth=insideWidth-.021f, leafHeight=DoorHeight-jamb-.023f;
            var leaf=Child(entrance,"Green oak front door",new Vector3(.006f,.013f,-leafWidth/2)).transform;
            var timber=new StationFabrication();
            for(int i=0;i<7;i++)
                timber.Box(m.green,new Vector3(0,leafHeight/2,(i+.5f)*leafWidth/7),new Vector3(.064f,leafHeight,leafWidth/7-.004f));
            foreach (int face in new[]{-1,1})
            {
                float x=face*.044f;
                foreach (float z in new[]{.066f,leafWidth-.066f})
                    timber.Box(m.oak,new Vector3(x,leafHeight/2,z),new Vector3(.025f,leafHeight,.108f));
                foreach (float y in new[]{.13f,.91f,leafHeight-.115f})
                    timber.Box(m.oak,new Vector3(x,y,leafWidth/2),new Vector3(.026f,.135f,leafWidth-.17f));
                for(int i=0;i<2;i++)
                {
                    float z=leafWidth*(.32f+.36f*i);
                    timber.Box(m.oak,new Vector3(x,1.52f,z),new Vector3(.022f,.94f,.322f));
                    timber.Box(m.green,new Vector3(x+face*.013f,1.52f,z),new Vector3(.012f,.85f,.254f));
                }
                var handle=new Vector3(face*.077f,1.05f,leafWidth-.168f);
                timber.Box(m.copper,handle,new Vector3(.019f,.205f,.074f));
                timber.Ring(m.gilt,handle,handle+Vector3.right*(face*.079f),.032f,.04f,10);
                timber.Beam(m.gilt,handle+Vector3.right*(face*.076f),handle+new Vector3(face*.078f,0,-.11f),.031f,.029f);
            }
            timber.Finish(leaf,"Cottage oak leaf and brass handles");
            var slab=leaf.gameObject.AddComponent<BoxCollider>();
            slab.size=new Vector3(.094f,leafHeight,leafWidth);
            slab.center=new Vector3(0,leafHeight*.5f,leafWidth*.5f);
            var jambCentre=Child(entrance,"Front door threshold",new Vector3(0,1.1f,0)).transform;
            var door=leaf.gameObject.AddComponent<PassDoor>();
            door.Configure(PassDoorKind.Cottage,hut,jambCentre,Quaternion.identity,Quaternion.Euler(0,-101,0),slab);
            // A small fixed pull on each face remains reachable once the leaf is parked.
            for(int side=-1;side<=1;side+=2)
            {
                var target=Child(entrance,"Jamb catch",new Vector3(side*.17f,1.1f,insideWidth/2+.034f));
                var grip=target.AddComponent<BoxCollider>(); grip.size=new Vector3(.127f,.36f,.17f);grip.isTrigger=true;
                target.layer=2;target.AddComponent<PassDoorTarget>().hinge=door;
                var detail=new StationFabrication();
                detail.Box(m.copper,Vector3.zero,new Vector3(.047f,.19f,.074f));
                detail.Ring(m.gilt,Vector3.zero,Vector3.right*(side*.057f),.026f,.033f,12);
                detail.Finish(target.transform,"Front door thumb catch "+(side<0?"inside":"outside"));
            }
            var enamel=Paint("Cottage entrance enamel",Color.white,.13f,.35f);
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Package+"/Station/CottageEnamel.png");
            if(!texture)throw new System.InvalidOperationException("Cottage entrance enamel artwork is missing");
            enamel.SetTexture("_BaseMap",texture);EditorUtility.SetDirty(enamel);
            CottageNotice(entrance,enamel,m,new Vector3(.148f,2.56f,0),Vector3.right,0);
            CottageNotice(hut,enamel,m,new Vector3(-4.27f,1.87f,-2.36f),Vector3.left,1);
            CottageNotice(hut,enamel,m,new Vector3(3.10f,1.84f,3.47f),Vector3.forward,-1);
            CottageNotice(hut,enamel,m,new Vector3(3.10f,1.84f,-3.47f),Vector3.back,1);
            return door;
        }

        static void CottageNotice(Transform parent,Material enamel,Palette m,Vector3 at,Vector3 outward,int arrow)
        {
            var mount=Child(parent,arrow==0?"Keeper's entrance plaque":"Entrance direction",at).transform;
            mount.rotation=Quaternion.LookRotation(-outward,Vector3.up);
            var b=new StationFabrication();
            float width=arrow==0?1.30f:1.04f, height=arrow==0?.325f:.26f;
            b.Box(m.copper,Vector3.zero,new Vector3(width+.032f,height+.032f,.034f));
            float offset=arrow==0?0:-arrow*.112f;
            float half=(width-(arrow==0?0:.211f))/2;
            var a=new Vector3(offset-half,-height/2,-.021f);
            b.AtlasQuad(enamel,a,a+Vector3.right*(half*2),a+new Vector3(half*2,height,0),a+Vector3.up*height,
                Vector3.back,new Vector2(0,arrow==0?.5f:0),new Vector2(1,arrow==0?1:.5f));
            if(arrow!=0)
            {
                float x=arrow*(width/2-.095f);
                b.Box(m.green,new Vector3(x,0,-.019f),new Vector3(.21f,height,.008f));
                b.Box(m.cream,new Vector3(x-arrow*.031f,0,-.03f),new Vector3(.089f,.026f,.012f));
                b.Triangle(m.cream,new Vector3(x-arrow*.003f,.052f,-.035f),new Vector3(x+arrow*.072f,0,-.035f),
                    new Vector3(x-arrow*.003f,-.052f,-.035f),Vector3.back);
            }
            b.Finish(mount,arrow==0?"Door nameplate":"Door direction "+(arrow<0?"left":"right"));
        }

        static void BuildPorch(Transform hut,Transform entrance,StationFabrication frame,Palette m)
        {
            float SnowAt(float fromDoor)
            {
                var p=entrance.TransformPoint(new Vector3(fromDoor,0,0));
                float survey=PassAtmosphere.GroundHeight(p.x,-p.z);
                return Physics.Raycast(new Vector3(p.x,survey+3,p.z),Vector3.down,out var ground,9,
                    Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore) ? ground.point.y-hut.position.y : survey-hut.position.y;
            }
            void Step(string name,float begin,float end,float top,float width)
            {
                float baseY=Mathf.Min(top-.15f,SnowAt(end)-.15f);
                var p=new Vector3((begin+end)/2,(top+baseY)/2,0);
                var size=new Vector3(end-begin,top-baseY,width);
                frame.Box(m.stone,p,size);
                frame.Box(m.slate,new Vector3(p.x,top-.028f,0),new Vector3(size.x+.013f,.057f,width+.035f));
                Solid(entrance,name,p,size);
            }
            Step("Entrance landing",-.16f,1.43f,-.013f,1.91f);
            for(int i=1;i<=20;i++)
            {
                float begin=1.43f+(i-1)*.38f, end=begin+.38f, top=-.013f-i*.13f;
                if(SnowAt(begin)>=top+.06f && SnowAt(end)>=top-.03f) break;
                Step("Snow approach tread "+i,begin,end,top,1.87f);
            }
        }
    }
}
