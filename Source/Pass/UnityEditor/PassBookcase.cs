using UnityEditor;
using UnityEngine;
using Winterbourne.Pass;
using static Winterbourne.Editor.StationFabrication;

namespace Winterbourne.Editor
{
    public static partial class PassPortalBuild
    {
        sealed class Bindery
        {
            public readonly Material walnut=Paint("Reading room walnut",Color.white,0,.32f);
            public readonly Material edge=Paint("Beeswax walnut moulding",Color.white,0,.39f);
            public readonly Material recess=Paint("Bookcase shadow",Color.white,0,.18f);
            public readonly Material pages=Paint("Deckled paper edges",new Color(.64f,.54f,.38f),0,.20f);
            public readonly Material leather=Paint("Ox blood binding",new Color(.255f,.13f,.12f),.03f,.27f);
            public readonly Material carpet=Paint("Reading room wool",new Color(.235f,.175f,.166f),0,.12f);
            public readonly Material spines=Paint("Lettered cloth and leather",Color.white,.035f,.29f);
            public Bindery()
            {
                ApplyTimber(walnut);
                ApplyTimber(edge,1.13f);
                ApplyTimber(recess,.52f);
                var atlas=AssetDatabase.LoadAssetAtPath<Texture2D>(Package+"/Station/LibraryBindings.png");
                if(!atlas)throw new System.InvalidOperationException("Missing railway library bindings.");
                spines.mainTexture=atlas;
                EditorUtility.SetDirty(spines);
            }
        }

        static Vector3 V(float x,float y,float z) => new Vector3(x,y,z);

        // The photographed timber's grain runs along V. Map the long axis of
        // each visible board continuously through the sawn edge and the mouldings.
        static void Board(StationFabrication g,Material wood,Vector3 centre,Vector3 size)
        {
            void Face(Vector3 n,Vector3 u,Vector3 v)
            {
                if(u.sqrMagnitude>v.sqrMagnitude){var swap=u;u=v;v=swap;}
                var p=centre+n*.5f;
                Vector2 phase=new Vector2(Mathf.Abs(centre.z*.317f+centre.x*.67f)%1,Mathf.Abs(centre.y*.31f+centre.z*.27f)%1);
                var texSize=new Vector2(Mathf.Max(.02f,u.magnitude/.63f),Mathf.Max(.02f,v.magnitude/1.89f));
                g.AtlasQuad(wood,p-(u+v)*.5f,p+(u-v)*.5f,p+(u+v)*.5f,p+(-u+v)*.5f,n,phase,phase+texSize);
            }
            var x=Vector3.right*size.x;var y=Vector3.up*size.y;var z=Vector3.forward*size.z;
            Face(x,y,z);Face(-x,y,z);Face(y,x,z);Face(-y,x,z);Face(z,x,y);Face(-z,x,y);
        }

        // Face profile measured out from the cabinet; an ogee between the two
        // flats catches a much finer highlight than a stack of rectangular strips.
        static void Moulding(StationFabrication g,Material wood,float left,float right,float y,float z,
                             int side,Vector2[] profile)
        {
            float run=right-left;
            for(int i=1;i<profile.Length;i++)
            {
                var a=profile[i-1];var b=profile[i];
                Vector3 p=V(left,y+a.y,z+side*a.x),q=V(left,y+b.y,z+side*b.x);
                var cross=V(0,-side*(q.z-p.z),side*(q.y-p.y));
                var dx=Vector3.right*run;
                g.AtlasQuad(wood,p,q,q+dx,p+dx,cross,Vector2.zero,
                    new Vector2((a-b).magnitude/.63f,run/1.89f));
                g.Triangle(wood,V(left,y,z),q,p,Vector3.left);
                g.Triangle(wood,V(right,y,z),p+dx,q+dx,Vector3.right);
            }
        }

        static readonly Vector2[] Crown={
            new Vector2(.215f,0),new Vector2(.224f,.016f),new Vector2(.23f,.017f),
            new Vector2(.230f,.033f),new Vector2(.242f,.045f),new Vector2(.251f,.050f),
            new Vector2(.267f,.054f),new Vector2(.277f,.063f),new Vector2(.284f,.074f),
            new Vector2(.285f,.088f),new Vector2(.291f,.093f),new Vector2(.291f,.107f),
            new Vector2(.273f,.113f)};
        static readonly Vector2[] BaseRail={
            new Vector2(.257f,0),new Vector2(.265f,.009f),new Vector2(.265f,.043f),
            new Vector2(.253f,.048f),new Vector2(.25f,.060f),new Vector2(.238f,.070f),
            new Vector2(.228f,.073f)};
        static readonly Vector2[] ShelfNose={
            new Vector2(.213f,-.027f),new Vector2(.228f,-.024f),new Vector2(.239f,-.016f),
            new Vector2(.241f,-.004f),new Vector2(.236f,.002f),new Vector2(.217f,.005f)};

        static void BookcaseGate(Transform gate,StationFabrication frame,Palette fittings)
        {
            var m=new Bindery();
            // A recessed fixed threshold and a single separate cabinet in front.
            // The cabinet rolls along X; the right-hand side contains only its jamb.
            const float clear=.745f;
            for(int side=-1;side<=1;side+=2)
            {
                float x=side*(clear+.050f);
                Board(frame,m.recess,V(x,1.251f,.052f),V(.100f,2.502f,.394f));
                for(int face=-1;face<=1;face+=2)
                {
                    float z=face<0?-.154f:.244f;
                    Board(frame,m.walnut,V(x,1.252f,z),V(.121f,2.50f,.038f));
                    Board(frame,m.edge,V(side*(clear+.014f),1.251f,z+face*.021f),V(.024f,2.50f,.014f));
                }
                Solid(gate,"Portal rebate",V(x,1.252f,.048f),V(.10f,2.504f,.395f));
            }
            Board(frame,m.walnut,V(0,2.561f,.047f),V(1.707f,.142f,.40f));
            Solid(gate,"Portal lintel",V(0,2.56f,.045f),V(1.70f,.140f,.4f));
            Board(frame,m.edge,V(0,2.640f,.047f),V(1.71f,.043f,.419f));
            for(int face=-1;face<=1;face+=2)
            {
                float z=face<0?-.173f:.259f;
                Board(frame,m.recess,V(0,2.564f,z),V(.94f,.114f,.021f));
                for(int side=-1;side<=1;side+=2)
                {
                    var screw=V(side*.439f,2.565f,z+face*.014f);
                    frame.Ring(fittings.copper,screw,screw+Vector3.forward*face*.004f,.007f,.007f,8);
                }
            }

            // The narrow iron guide and brass-flanged rollers explain why the open
            // cover stands forward of the doorway. The tread is flush at foot level.
            Board(frame,m.recess,V(-.772f,2.631f,-.177f),V(3.19f,.045f,.080f));
            frame.Box(fittings.slate,V(-.772f,2.625f,-.236f),V(3.18f,.027f,.042f));
            frame.Box(fittings.slate,V(-.772f,.011f,-.210f),V(3.18f,.018f,.039f));
            frame.Box(fittings.copper,V(-.772f,.020f,-.210f),V(3.13f,.004f,.013f));

            var leaf=Child(gate,"Sliding library bookcase",V(-1.57f,0,-.22f)).transform;
            var furniture=Child(leaf,"Railway library joinery",Vector3.zero).transform;
            furniture.localScale=V(1,.84f,.80f);
            var cabinet=new StationFabrication();
            Case(cabinet,m,-.775f,.775f,0,0,false,0,false,783);
            // Four raised panels finish the back, visible from the far side when open.
            for(int bay=0;bay<2;bay++)
            {
                float x=(bay-.5f)*.692f;
                for(int row=0;row<2;row++)
                {
                    float y=.81f+row*1.405f;
                    Board(cabinet,m.walnut,V(x,y,.214f),V(.626f,1.32f,.044f));
                    Board(cabinet,m.edge,V(x,y,.242f),V(.545f,1.20f,.019f));
                }
            }
            Board(cabinet,m.walnut,V(0,3.020f,0),V(1.559f,.101f,.46f));
            for(int side=-1;side<=1;side+=2)
                Moulding(cabinet,m.edge,-.783f,.783f,2.971f,0,side,Crown);
            // A worn recessed handle on the movable stile, plus fixed return pulls
            // accessible at the right-hand jamb on either face of a closed cover.
            cabinet.Box(m.recess,V(.732f,1.39f,-.269f),V(.077f,.24f,.013f));
            cabinet.Ring(fittings.copper,V(.731f,1.313f,-.283f),V(.731f,1.467f,-.283f),.013f,.013f,8);
            cabinet.Finish(furniture,"Sliding library cabinet and volumes");
            var rollers=new StationFabrication();
            for(int side=-1;side<=1;side+=2)
            {
                float x=side*.626f;
                rollers.Ring(fittings.slate,V(x,.065f,-.085f),V(x,.065f,-.135f),.055f,.055f,14);
                rollers.Ring(fittings.copper,V(x,.065f,-.137f),V(x,.065f,-.143f),.017f,.017f,12);
                rollers.Box(fittings.slate,V(x,2.589f,.012f),V(.055f,.064f,.105f));
            }
            rollers.Finish(leaf,"Bookcase runner shoes");
            var panel=leaf.gameObject.AddComponent<BoxCollider>();
            panel.center=V(0,1.295f,0);panel.size=V(1.55f,2.59f,.392f);
            var cover=gate.gameObject.AddComponent<PassBookcaseCover>();
            cover.leaf=leaf;cover.panel=panel;cover.closedOffset=Vector3.right*1.57f;
            BoxCollider Pull(int face)
            {
                // The front grip projects slightly past the closed cabinet's edge.
                float z=face<0?-.216f:.277f;
                var target=Child(gate,face<0?"Platform side pull":"Reverse side pull",V(.837f,1.18f,z));
                target.layer=2;
                var hit=target.AddComponent<BoxCollider>();hit.size=V(.19f,.36f,.12f);hit.isTrigger=true;
                frame.Box(m.recess,V(.817f,1.18f,z-face*.013f),V(.101f,.242f,.033f));
                frame.Ring(fittings.copper,V(.817f,1.106f,z+face*.017f),V(.817f,1.254f,z+face*.017f),.016f,.016f,9);
                return hit;
            }
            cover.frontPull=Pull(-1);cover.rearPull=Pull(1);
        }

        static void Case(StationFabrication g,Bindery m,float left,float right,float baseY,float z,
                         bool lowerCupboard,int variant,bool doubleSided,int seed)
        {
            float width=right-left,cx=(left+right)*.5f;
            const float stile=.074f;
            float[] shelves=lowerCupboard?new[]{.688f,1.185f,1.73f,2.278f,2.793f}:
                variant==2?new[]{.157f,.638f,1.148f,1.647f,2.12f,2.61f,2.93f}:
                           new[]{.151f,.557f,1.004f,1.506f,1.961f,2.439f,2.932f};
            int boards=Mathf.Max(3,Mathf.RoundToInt(width/.17f));
            for(int i=0;i<boards;i++)
            {
                float x=left+(i+.5f)*width/boards;
                Board(g,m.recess,V(x,baseY+1.54f,z+(doubleSided?0:.185f)),
                    V(width/boards-.0026f,2.86f,.032f));
            }
            foreach(int hand in new[]{-1,1})
            {
                float outer=hand<0?left:right;
                float x=outer-hand*stile*.5f;
                Board(g,m.walnut,V(x,baseY+1.54f,z),V(stile,2.89f,.456f));
                foreach(int face in new[]{-1,1})
                {
                    if(!doubleSided&&face>0)continue;
                    Board(g,m.edge,V(x,baseY+1.538f,z+face*.224f),V(stile+.022f,2.865f,.047f));
                    Board(g,m.walnut,V(x,baseY+1.539f,z+face*.249f),V(stile-.008f,2.84f,.017f));
                    Board(g,m.recess,V(x-hand*.021f,baseY+1.54f,z+face*.259f),V(.009f,2.77f,.005f));
                }
                // Tapered bracket feet leave a shadow gap under the plinth.
                float footx=outer-hand*.064f;
                foreach(int face in new[]{-1,1})
                {
                    var top=V(footx,baseY+.091f,z+face*.177f);
                    var bottom=top+V(-hand*.011f,-.091f,-face*.006f);
                    for(int i=0;i<4;i++)
                    {
                        Vector3 corner(int j,float w) => V(j==0||j==3?-w:w,0,j<2?-w:w);
                        var a=bottom+corner(i,.032f);var b=bottom+corner((i+1)%4,.032f);
                        var c=top+corner((i+1)%4,.047f);var d=top+corner(i,.047f);
                        g.Quad(m.walnut,a,b,c,d,(a+b)*.5f-bottom);
                    }
                }
            }
            Board(g,m.walnut,V(cx,baseY+.109f,z),V(width,.107f,.45f));
            foreach(int face in new[]{-1,1})
            {
                if(!doubleSided&&face>0)continue;
                Moulding(g,m.edge,left-.014f,right+.014f,baseY+.065f,z,face,BaseRail);
                if(lowerCupboard)
                {
                    // Fielded door below the narrow register bay.
                    float inn=width-2*stile-.015f;
                    Board(g,m.walnut,V(cx,baseY+.409f,z+face*.198f),V(inn,.48f,.068f));
                    Board(g,m.recess,V(cx,baseY+.412f,z+face*.234f),V(inn-.116f,.349f,.010f));
                    Board(g,m.walnut,V(cx,baseY+.412f,z+face*.245f),V(inn-.142f,.320f,.019f));
                    var at=V(right-stile-.044f,baseY+.55f,z+face*.24f);
                    g.Ring(m.edge,at,at+Vector3.forward*face*.022f,.016f,.021f,10);
                }
            }
            if(lowerCupboard) Board(g,m.recess,V(cx,baseY+2.877f,z),V(width-2*stile,.196f,.428f));
            for(int row=0;row<shelves.Length;row++)
            {
                float y=baseY+shelves[row],innerLeft=left+stile,innerRight=right-stile;
                Board(g,m.walnut,V(cx,y-.016f,z),V(width-2*stile+.003f,.032f,.446f));
                foreach(int face in new[]{-1,1})
                {
                    if(!doubleSided&&face>0)continue;
                    Moulding(g,m.edge,innerLeft,innerRight,y,z,face,ShelfNose);
                    if(row==shelves.Length-1)continue;
                    BookGroup(g,m,innerLeft+.013f,innerRight-.013f,y+.002f,z,face,
                        shelves[row+1]-shelves[row]-.040f,row+variant,seed+97*row+782*face);
                }
            }
        }

        static void FitReadingRoom(Transform hut,StationFabrication g,Palette p)
        {
            var m=new Bindery();
            // A small return bookcase faces across the desk from the west wall. It
            // reads as separate furnishing, not a stationary wing beside the waygate.
            var shelf=Child(hut,"Bridge keeper's reference shelf",V(-3.83f,0,-2.31f)).transform;
            shelf.localRotation=Quaternion.Euler(0,-90f,0);
            shelf.localScale=V(1,.78f,.76f);
            var builtIn=new StationFabrication();
            Case(builtIn,m,-.76f,.76f,0,0,false,2,false,952);
            Board(builtIn,m.walnut,V(0,3.009f,0),V(1.54f,.072f,.51f));
            Moulding(builtIn,m.edge,-.773f,.773f,2.971f,0,-1,Crown);
            builtIn.Finish(shelf,"Keeper's reference shelf and books");
            Solid(shelf,"Library wall below the reference books",V(0,1.5f,0),V(1.53f,3.03f,.51f));
            // The wool runner has a woven border woven in narrow, broken stripes,
            // with alternating knots and a wax-worn dark wooden floor around it.
            g.Box(m.carpet,V(-.10f,.012f,.14f),V(1.52f,.014f,3.36f));
            for(int hand=-1;hand<=1;hand+=2)
            {
                g.Box(m.recess,V(-.1f+hand*.707f,.022f,.14f),V(.038f,.003f,3.27f));
                g.Box(m.pages,V(-.1f+hand*.676f,.023f,.14f),V(.009f,.003f,3.21f));
                g.Box(m.recess,V(-.1f,.022f,.14f+hand*1.613f),V(1.44f,.003f,.039f));
            }
            for(int line=-6;line<=6;line++)
                foreach(int hand in new[]{-1,1})
                    g.Box(m.leather,V(-.10f+hand*.642f,.022f,.14f+line*.237f+.018f*(line%2)),V(.029f,.002f,.046f));
            OpenAtlas(g,m,V(-2.70f,1.010f,.51f));
        }
    }
}
