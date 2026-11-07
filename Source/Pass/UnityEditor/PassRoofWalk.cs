using System;
using UnityEngine;
using Winterbourne.Pass;
using static Winterbourne.Editor.StationFabrication;

namespace Winterbourne.Editor
{
    public static partial class PassFittings
    {
        const float BoardTop=4.20f, BoardInner=.71f, BoardOuter=1.38f;
        const float WellLeft=.648f, WellRight=1.416f;

        static void BuildRoofWalk(Transform car,int number,Finishes mat)
        {
            var fittings=Child(car,"Roof maintenance access",Vector3.zero).transform;
            bool hasLadder=number==3 || number==9;
            float partition=number==3 ? 2.52f : 3.10f;
            var well=Rect.MinMaxRect(WellLeft,partition+.014f,WellRight,partition+1.003f);

            // Exact camber, raised clerestory and rounded ends from the artist's model.
            // Keep the physical hole in the same triangles as the visible opening.
            int shellCount=0, cuts=0;
            foreach(var filter in car.GetComponentsInChildren<MeshFilter>(true))
            {
                string name=filter.name;
                bool shell=name=="Roof_Structure" || name=="Roof_Clerestory";
                if(hasLadder && (name.StartsWith("Roof_",StringComparison.Ordinal) ||
                    name.StartsWith("Coved_headlining_",StringComparison.Ordinal)))
                    if(PassHatchCut.Cut(filter,car,well,number)) cuts++;
                if(!shell)continue;
                filter.gameObject.AddComponent<MeshCollider>().sharedMesh=filter.sharedMesh;
                shellCount++;
            }
            PassBuild.Check(shellCount==2,$"Car {number:D2} lacks its cambered roof or raised clerestory");
            var deck=new StationFabrication();
            if(hasLadder)
            {
                PassBuild.Check(cuts>=2,$"Car {number:D2} hatch must pierce the outer roof and headlining");
                var old=Find(car,"Coach ceiling").GetComponent<BoxCollider>();
                old.enabled=false;
                Recorded(old);
                void Ceiling(float x1,float x2,float z1,float z2) =>
                    Box(fittings,"Ceiling beside roof hatch",new Vector3((x1+x2)/2,3.686f,(z1+z2)/2),
                        new Vector3(x2-x1,.10f,z2-z1));
                Ceiling(-1.455f,1.455f,-9f,well.yMin);
                Ceiling(-1.455f,1.455f,well.yMax,9f);
                Ceiling(-1.455f,well.xMin,well.yMin,well.yMax);
                Ceiling(well.xMax,1.455f,well.yMin,well.yMax);
                Board(fittings,deck,mat,-8.95f,well.yMin-.032f);
                Board(fittings,deck,mat,well.yMax+.032f,8.95f);
                Ladder(fittings,deck,mat,well,partition);
            }
            else Board(fittings,deck,mat,-8.95f,8.95f);
            deck.Finish(fittings,hasLadder ? $"Car {number:D2} roof hatch and ladder" : "Coach roof running board");
        }

        static void Board(Transform parent,StationFabrication deck,Finishes mat,float first,float last)
        {
            float mid=(first+last)/2, width=BoardOuter-BoardInner, x=(BoardOuter+BoardInner)/2;
            // Narrow, longitudinal drainage slits separate three hardwood tread planks.
            int lengths=Mathf.CeilToInt((last-first)/2.2f);
            for(int i=0;i<lengths;i++)
            {
                float span=(last-first)/lengths, z=first+(i+.5f)*span;
                for(int p=0;p<3;p++)
                    deck.Box(mat.wood,new Vector3(BoardInner+(p+.5f)*width/3,BoardTop-.024f,z),
                        new Vector3(width/3-.009f,.048f,span-.009f));
                foreach(float railX in new[]{BoardInner+.065f,BoardOuter-.065f})
                {
                    float camberDrop=railX>1f ? .20f : .095f;
                    deck.Box(mat.soot,new Vector3(railX,BoardTop-.052f-camberDrop/2,z),
                        new Vector3(.048f,camberDrop,.074f));
                    deck.Ring(mat.soot,new Vector3(railX,BoardTop-.002f,z),
                        new Vector3(railX,BoardTop+.003f,z),.018f,.015f,6);
                }
                deck.Box(mat.soot,new Vector3(x,BoardTop-.056f,z),new Vector3(width-.025f,.026f,.073f));
            }
            Box(parent,"Roof running board",new Vector3(x,BoardTop-.025f,mid),new Vector3(width,.05f,last-first));
        }

        static void Ladder(Transform parent,StationFabrication metal,Finishes mat,Rect well,float partition)
        {
            float axis=1f, z=partition+.166f, middle=well.center.y;
            var ladder=Child(parent,"Iron roof ladder",Vector3.zero).AddComponent<PassRoofLadder>();
            Transform Mark(string label,float x,float y,float at) =>
                Child(ladder.transform,label,new Vector3(x,y,at)).transform;
            ladder.lowerLanding=Mark("Lower landing",.74f,1.40f,partition+.65f);
            ladder.lowerRung=Mark("Ascent line",axis,1.40f,partition+.48f);
            ladder.upperRung=Mark("Through hatch",axis,4.25f,partition+.48f);
            ladder.roofLanding=Mark("Roof landing",axis,BoardTop+.02f,partition-.55f);

            for(int hand=-1;hand<=1;hand+=2)
            {
                float x=axis+hand*.255f;
                metal.Ring(mat.soot,new Vector3(x,1.43f,z),new Vector3(x,4.27f,z),.023f,.023f);
                // Bronze handholds turn over the coaming towards the running board.
                metal.Ring(mat.bronze,new Vector3(x,4.27f,z),new Vector3(x,4.43f,z-.055f),.022f,.022f);
                metal.Ring(mat.bronze,new Vector3(x,4.43f,z-.055f),new Vector3(x,4.43f,z-.22f),.022f,.022f);
                metal.Ring(mat.soot,new Vector3(x,4.43f,z-.22f),new Vector3(x,4.17f,z-.26f),.023f,.023f);
                foreach(float y in new[]{1.58f,2.42f,3.34f})
                {
                    metal.Box(mat.soot,new Vector3(x,y,partition+.057f),new Vector3(.086f,.11f,.030f));
                    metal.Beam(mat.soot,new Vector3(x,y-.027f,partition+.065f),new Vector3(x,y+.014f,z),.033f,.035f);
                }
            }
            for(int i=0;i<=9;i++)
            {
                float y=1.61f+i*.271f;
                metal.Ring(mat.soot,new Vector3(axis-.267f,y,z),new Vector3(axis+.267f,y,z),.020f,.020f);
                metal.Ring(mat.bronze,new Vector3(axis-.155f,y,z),new Vector3(axis+.155f,y,z),.0215f,.0215f);
            }
            // Line the shaft between the moulded ceiling and the cambered weather skin.
            foreach(float x in new[]{well.xMin,well.xMax})
            {
                float sign=x==well.xMin ? -1 : 1;
                metal.Box(mat.soot,new Vector3(x+sign*.014f,3.875f,middle),new Vector3(.028f,.68f,well.height+.054f));
                metal.Box(mat.bronze,new Vector3(x+sign*.023f,3.531f,middle),new Vector3(.046f,.020f,well.height+.070f));
            }
            foreach(float end in new[]{well.yMin,well.yMax})
            {
                float sign=end==well.yMin ? -1 : 1;
                metal.Box(mat.soot,new Vector3(well.center.x,3.905f,end+sign*.015f),new Vector3(well.width,.62f,.030f));
                metal.Box(mat.bronze,new Vector3(well.center.x,3.585f,end+sign*.021f),new Vector3(well.width,.019f,.042f));
            }
            // Hinged cover folds flat against the adjoining deck; the clear opening
            // stays visible both from the compartment and from the maintenance walk.
            float lidZ=well.yMax+well.height/2+.042f;
            var lidCentre=new Vector3(well.center.x,BoardTop+.023f,lidZ);
            var lidSize=new Vector3(well.width,.045f,well.height);
            metal.Box(mat.pine,lidCentre,lidSize);
            Box(parent,"Folded hatch cover",lidCentre,lidSize);
            for(int sign=-1;sign<=1;sign+=2)
            {
                float x=well.center.x+sign*.22f;
                metal.Ring(mat.bronze,new Vector3(x-.068f,BoardTop+.039f,well.yMax+.026f),
                    new Vector3(x+.068f,BoardTop+.039f,well.yMax+.026f),.027f,.027f);
                metal.Box(mat.soot,new Vector3(x,BoardTop+.048f,lidZ),new Vector3(.036f,.008f,well.height-.08f));
            }
        }
    }
}
