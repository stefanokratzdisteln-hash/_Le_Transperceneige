using UnityEngine;

namespace Winterbourne.Editor
{
    public static partial class PassPortalBuild
    {
        static float Between(System.Random rng,float a,float b) => Mathf.Lerp(a,b,(float)rng.NextDouble());

        static void BookGroup(StationFabrication g,Bindery materials,float left,float right,float shelf,
                              float z,int face,float clearance,int row,int seed)
        {
            var rng=new System.Random(seed);
            float extent=right-left;
            int design=row%6;
            // Deliberately compose small collections rather than filling every inch.
            // The low register and the run of tall atlases have visibly different rhythms.
            switch(design)
            {
                case 0:
                    Stack(g,materials,rng,left+.055f,shelf,z,face,extent*.39f,4,true);
                    Uprights(g,materials,rng,right-.018f,shelf,z,face,extent*.49f,clearance,-1,12,.83f,8);
                    break;
                case 1:
                    Uprights(g,materials,rng,left+.019f,shelf,z,face,extent*.53f,clearance,1,-12,.72f,9);
                    Stack(g,materials,rng,right-extent*.33f-.038f,shelf,z,face,extent*.33f,3,false);
                    break;
                case 2:
                    Uprights(g,materials,rng,left+.015f,shelf,z,face,extent*.38f,clearance,1,-2,.93f,5);
                    Uprights(g,materials,rng,right-.026f,shelf,z,face,extent*.36f,clearance,-1,17,.76f,5);
                    break;
                case 3:
                    Stack(g,materials,rng,left+extent*.12f,shelf,z,face,extent*.38f,3,true);
                    Uprights(g,materials,rng,right-.015f,shelf,z,face,extent*.31f,clearance,-1,9,.88f,4);
                    break;
                case 4:
                    Uprights(g,materials,rng,left+.012f,shelf,z,face,extent*.57f,clearance,1,-13,.85f,9);
                    Stack(g,materials,rng,right-extent*.28f-.046f,shelf,z,face,extent*.28f,2,false);
                    break;
                default:
                    Uprights(g,materials,rng,left+.026f,shelf,z,face,extent*.30f,clearance,1,-9,.80f,5);
                    Uprights(g,materials,rng,right-.012f,shelf,z,face,extent*.45f,clearance,-1,4,.96f,7);
                    break;
            }
        }

        static void Uprights(StationFabrication g,Bindery m,System.Random rng,float jamb,float y,float z,
            int face,float span,float clearance,int growth,float lean,float relativeHeight,int maximum)
        {
            float tilt=lean*Mathf.Deg2Rad;
            float typical=clearance*relativeHeight;
            // The top of the first leaning book rests against the cabinet side;
            // each following book sits edge to edge with that neighbour.
            float used=Mathf.Abs(Mathf.Sin(tilt))*typical;
            int librarySeries=rng.Next(32);
            for(int i=0;i<maximum;i++)
            {
                float w=Between(rng,.048f,.103f),h=typical*Between(rng,.86f,1.07f);
                if(used+w>span)break;
                float angle=tilt+Between(rng,-.012f,.012f);
                Vector3 up=V(Mathf.Sin(angle),Mathf.Cos(angle),0);
                Vector3 across=V(Mathf.Cos(angle),-Mathf.Sin(angle),0);
                float deep=Between(rng,.146f,.217f);
                Vector3 front=Vector3.forward*face;
                var spine=V(jamb+growth*(used+w*.5f),y+Mathf.Abs(across.y)*w*.5f,z+face*Between(rng,.127f,.185f));
                int stamp=(librarySeries+(i<3?0:rng.Next(23)))%32;
                if(i<3 && maximum>6)stamp=(librarySeries+i%2)%32;
                Book(g,m,spine,across,up,front,w,h,deep,stamp);
                used+=w/Mathf.Max(.92f,Mathf.Cos(angle))+.007f;
            }
        }

        static void Stack(StationFabrication g,Bindery m,System.Random rng,float left,float y,float z,
            int face,float span,int count,bool frontEdges)
        {
            float depth=Between(rng,.169f,.217f);
            float centre=left+span*.50f;
            for(int i=0;i<count;i++)
            {
                float length=Mathf.Max(.12f,span*Between(rng,.78f,1.0f))*(1f-.022f*i);
                float thickness=Between(rng,.027f,.053f),deep=depth*Between(rng,.89f,1f);
                var turn=Quaternion.AngleAxis(Between(rng,-5,5),Vector3.up);
                var front=turn*(Vector3.forward*face);
                var along=turn*Vector3.right;
                var spine=V(centre+Between(rng,-.019f,.019f),y+thickness/2,z+face*.181f)-along*length*.5f;
                // Some folios face inward, leaving a bank of cream deckles and
                // contrasting boards facing the reader exactly as a hand-laid stack does.
                if((i%3!=1)==frontEdges){spine-=front*deep;front=-front;}
                Book(g,m,spine,Vector3.up,along,front,thickness,length,deep,rng.Next(32));
                y+=thickness+.002f;
            }
        }

        static void Book(StationFabrication g,Bindery m,Vector3 baseSpine,Vector3 across,Vector3 head,
            Vector3 outward,float thickness,float height,float depth,int id)
        {
            float half=thickness*.5f;
            Vector3 P(float x,float h,float inward) => baseSpine+across*x+head*h-outward*inward;
            const float pad=3f/3072;
            var spineTile=new Rect((id%16)/16f,1f-(id/16+1)/4f,1f/16f,.25f);
            var coverTile=new Rect((id%8)*.09375f,.5f-(id/8+1)*.125f,.09375f,.125f);
            var edgeTile=new Rect(.75f+(id%2)*.125f,.5f-((id%8)/2+1)*.125f,.125f,.125f);
            void Plate(Rect tile,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 n,float from=0,float to=1)
            {
                g.AtlasQuad(m.spines,a,b,c,d,n,
                    new Vector2(Mathf.Lerp(tile.xMin+pad,tile.xMax-pad,from),tile.yMin+pad),
                    new Vector2(Mathf.Lerp(tile.xMin+pad,tile.xMax-pad,to),tile.yMax-pad));
            }
            // Both boards overhang the text block. Full-cover cloth, morocco or
            // marbling remains visible when a folio is leaned, stacked or turned.
            foreach(int side in new[]{-1,1})
            {
                float outer=side*half,inner=side*(half-.0055f);
                Plate(coverTile,P(outer,0,0),P(outer,0,depth),P(outer,height,depth),P(outer,height,0),across*side);
                Plate(coverTile,P(inner,0,0),P(inner,0,depth),P(inner,height,depth),P(inner,height,0),-across*side);
                Plate(spineTile,P(inner,0,depth),P(outer,0,depth),P(outer,height,depth),P(inner,height,depth),-outward,.025f,.045f);
                Plate(spineTile,P(inner,height,depth),P(outer,height,depth),P(outer,height,0),P(inner,height,0),head,.04f,.06f);
                Plate(spineTile,P(inner,0,depth),P(outer,0,depth),P(outer,0,0),P(inner,0,0),-head,.04f,.06f);
            }
            float page=half-.0055f,edge=depth-.006f;
            // Atlas V spans the thickness, so cut-paper lines always run along the pages.
            Plate(edgeTile,P(-page,.004f,edge),P(-page,height-.006f,edge),P(page,height-.006f,edge),P(page,.004f,edge),-outward);
            Plate(edgeTile,P(-page,height-.006f,.009f),P(-page,height-.006f,edge),P(page,height-.006f,edge),P(page,height-.006f,.009f),head);
            Plate(edgeTile,P(-page,.004f,.009f),P(-page,.004f,edge),P(page,.004f,edge),P(page,.004f,.009f),-head);
            // Three facets put a narrow line of light on the rolled shoulder of the binding.
            var left=P(-half,0,0);var rLeft=P(-half*.72f,0,-.006f);
            var rRight=P(half*.72f,0,-.006f);var right=P(half,0,0);
            void Spine(Vector3 a,Vector3 b,float u,float v)
                =>Plate(spineTile,a,b,b+head*height,a+head*height,outward,u,v);
            if(Vector3.Dot(Vector3.Cross(across,head),outward)<0)
            {
                Spine(left,rLeft,0,.13f);Spine(rLeft,rRight,.13f,.87f);Spine(rRight,right,.87f,1);
            }
            else
            {
                Spine(right,rRight,0,.13f);Spine(rRight,rLeft,.13f,.87f);Spine(rLeft,left,.87f,1);
            }
        }

        static void OpenAtlas(StationFabrication g,Bindery m,Vector3 origin)
        {
            var turn=Quaternion.Euler(0,-12,0);
            Vector3 P(float x,float h,float z) => origin+turn*V(x,h,z);
            g.Box(m.leather,origin,V(.62f,.024f,.40f),turn);
            for(int side=-1;side<=1;side+=2)
            {
                float prev=0,h=.025f;
                for(int section=1;section<=5;section++)
                {
                    float nx=section*.059f;
                    float nh=.027f+.029f*Mathf.Sin(Mathf.PI*section/6f);
                    var a=P(prev*side,h,-.186f);var b=P(nx*side,nh,-.186f);
                    var c=P(nx*side,nh,.184f);var d=P(prev*side,h,.184f);
                    g.Quad(m.pages,a,b,c,d,Vector3.up);
                    g.Quad(m.pages,P(prev*side,.013f,-.186f),P(nx*side,.013f,-.186f),b,a,Vector3.back);
                    prev=nx;h=nh;
                }
                for(int row=0;row<9;row++)
                {
                    float z=-.131f+row*.030f;
                    float length=row==0?.174f:row==8?.118f:.188f;
                    g.Beam(m.recess,P(side*.055f,.046f,z),P(side*(.055f+length),.057f,z),.002f,.0018f);
                }
            }
            // Sewn-in cloth marker drops between the open leaves.
            g.Quad(m.carpet,P(.009f,.03f,-.184f),P(.026f,.032f,-.184f),
                P(.011f,.015f,-.244f),P(-.004f,.016f,-.238f),Vector3.up);
        }
    }
}
