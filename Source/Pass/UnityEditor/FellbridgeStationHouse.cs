using UnityEditor;
using UnityEngine;
using Winterbourne.Pass;
using static Winterbourne.Editor.StationFabrication;

namespace Winterbourne.Editor
{
    public static partial class PassFittings
    {
        static bool HouseFrontage(float chainage) => chainage>=995f && chainage<1015f;

        static void MakeStationHouse(Transform station,Finishes finishes)
        {
            var house=Child(station,"Fellbridge station house",PlatformPoint(-9f,0,1005f)).transform;
            new StationHouse(house,finishes).Build();
        }

        sealed class StationHouse
        {
            const float Upper=3.42f, Eaves=6.88f, Depth=9f, HalfLength=10f;
            readonly Transform house;
            readonly Finishes m;
            readonly StationFabrication shell=new StationFabrication(), interior=new StationFabrication();
            readonly Material brick, brickLight, brickDark, plaster, green, glass, signs, timetable;

            internal StationHouse(Transform root,Finishes finishes)
            {
                house=root; m=finishes;
                brick=Paint("Fired station brick",new Color(.47f,.219f,.145f));
                brickLight=Paint("Weathered red brick",new Color(.56f,.283f,.201f));
                brickDark=Paint("Kiln flashed brick",new Color(.37f,.165f,.124f));
                plaster=Paint("Station lime plaster",new Color(.824f,.764f,.640f));
                green=Paint("Station lead green",new Color(.127f,.269f,.215f),.08f,.41f);
                glass=AssetDatabase.LoadAssetAtPath<Material>("Assets/Transperceneige/Car07/Car07_CabinGlass.mat");
                signs=Graphic("House enamel signs","StationHouseEnamel");
                timetable=Graphic("Station timetable ink","FellbridgeTimetable");
            }

            static Material Graphic(string name,string texture)
            {
                var mat=Paint(name,Color.white);
                var image=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/"+texture+".png");
                if (!image) throw new System.InvalidOperationException("Missing station artwork "+texture);
                mat.mainTexture=image;
                EditorUtility.SetDirty(mat);
                return mat;
            }

            void Solid(StationFabrication mesh,Material material,string name,Vector3 p,Vector3 size)
            {
                mesh.Box(material,p,size);
                Box(house,name,p,size);
            }
            static Vector3 P(float x,float y,float z) => new Vector3(x,y,z);

            public void Build()
            {
                Solid(shell,m.masonry,"Ground floor",P(-4.43f,-.66f,0),P(9.56f,1.32f,20.3f));
                TileHall();
                foreach(var end in new[]{-1,1})
                {
                    float z=end*7.5f;
                    shell.Box(m.stone,P(.60f,-.025f,z),P(1.58f,.05f,2.49f));
                    Box(house,"Entrance threshold",P(.60f,-.06f,z),P(1.58f,.12f,2.49f));
                }
                UpperFloor(-6.72f,.03f,-HalfLength,HalfLength);
                UpperFloor(-Depth,-6.72f,-HalfLength,-3.0f);
                UpperFloor(-Depth,-6.72f,4.04f,HalfLength);
                foreach(int side in new[]{-1,1})
                {
                    var outward=Vector3.right*side;
                    var origin=P(side>0?0:-Depth,0,0);
                    for(int i=0;i<4;i++)
                    {
                        var bay=origin+Vector3.forward*(-7.5f+5*i);
                        WallBay(bay,outward,5,0,side>0&&(i==0||i==3),i,0);
                        WallBay(bay,outward,5,Upper,false,i,1);
                    }
                    outward=Vector3.forward*side;
                    origin=P(-Depth/2,0,side*HalfLength);
                    for(int i=0;i<2;i++)
                    {
                        var bay=origin+Vector3.right*(-Depth/4 + i*Depth/2);
                        WallBay(bay,outward,Depth/2,0,false,i,0);
                        WallBay(bay,outward,Depth/2,Upper,false,i,1);
                    }
                }
                Roof();
                Stair();
                Rooms();
                SignsAndLights();
                shell.Finish(house,"Fellbridge house masonry and joinery");
                interior.Finish(house,"Fellbridge house rooms and fittings");

                var volume=Child(house,"Inside station house",P(-4.5f,3.40f,0));
                volume.layer=2;
                var box=volume.AddComponent<BoxCollider>(); box.isTrigger=true; box.size=P(8.92f,6.85f,19.93f);
                volume.AddComponent<PassShelterVolume>();
                var portal=Child(house,"Fellbridge portal arch anchor",P(-3.4f,0,8.90f)).transform;
                portal.localRotation=Quaternion.identity;
                var arrival=Child(house,"Fellbridge portal arrival",P(-3.4f,.04f,6.05f)).transform;
                arrival.localRotation=Quaternion.LookRotation(P(1,0,.31f));
            }

            void TileHall()
            {
                // Encaustic tiles form a dark double border around an ochre field.
                // A single batch draws their top faces; the foundation carries the player.
                const float tile=.40f;
                Vector3 gap=P(.004f,0,.004f);
                for(int i=0;i<22;i++)
                    for(int j=0;j<50;j++)
                    {
                        int edge=Mathf.Min(Mathf.Min(i,21-i),Mathf.Min(j,49-j));
                        var tint=edge==1||edge==3?green:edge==2?plaster:
                            ((i+j)%2==0?m.khaki:m.cream);
                        Vector3 a=P(-8.9f+i*tile,.004f,-10+j*tile)+gap;
                        var b=a+Vector3.right*(tile-.008f);var c=b+Vector3.forward*(tile-.008f);
                        interior.Quad(tint,a,b,c,a+Vector3.forward*(tile-.008f),Vector3.up);
                        if(edge>3 && i%3==0 && j%3==0)
                        {
                            var centre=(a+c)/2+Vector3.up*.001f;
                            interior.Quad(m.oxblood,centre+Vector3.right*.105f,centre+Vector3.forward*.105f,
                                centre-Vector3.right*.105f,centre-Vector3.forward*.105f,Vector3.up);
                        }
                    }
            }

            void UpperFloor(float a,float b,float z0,float z1)
            {
                var centre=P((a+b)/2,Upper-.12f,(z0+z1)/2);
                Solid(shell,m.wood,"Upper floor and plaster ceiling",centre,P(b-a,.24f,z1-z0));
                shell.Box(plaster,centre+Vector3.down*.119f,P(b-a,.012f,z1-z0));
                for(float x=a+.22f;x<b;x+=.23f)
                    interior.Box(m.ash,P(x,Upper+.001f,(z0+z1)/2),P(.004f,.002f,z1-z0-.07f));
            }

            void WallBay(Vector3 c,Vector3 n,float width,float floor,bool doorway,int bay,int storey)
            {
                bool alongZ=n.x!=0;
                Vector3 axis=alongZ?Vector3.forward:Vector3.right;
                float baseY=floor, height=storey==0 ? Upper : Eaves-Upper;
                float span=doorway?1.68f:1.86f, sill=doorway?0:.86f, lintel=doorway?2.53f:2.61f;
                Vector3 At(float a,float y,float outwards=0) => c+axis*a+Vector3.up*(baseY+y)+n*outwards;
                Vector3 Size(float wide,float h,float deep) => alongZ ? P(deep,h,wide) : P(wide,h,deep);
                void Block(float a,float b,float lo,float hi)
                {
                    if(hi-lo<.005f || b-a<.005f)return;
                    float mid=(a+b)/2,level=(lo+hi)/2;
                    Solid(shell,brick,"Masonry pier",At(mid,level),Size(b-a,hi-lo,.22f));
                    interior.Box(plaster,At(mid,level,-.115f),Size(b-a,hi-lo,.011f));
                    if(lo<1.02f)
                    {
                        float top=Mathf.Min(hi,1.02f);
                        interior.Box(green,At(mid,(lo+top)/2,-.133f),Size(b-a,top-lo,.030f));
                        interior.Box(m.wood,At(mid,top-.02f,-.15f),Size(b-a,.047f,.033f));
                        for(float trim=a+.16f;trim<b-.03f;trim+=.34f)
                            interior.Box(green,At(trim,(lo+top)/2,-.151f),Size(.031f,top-lo,.028f));
                    }
                    // A running bond of individually cut faces, with different fired clay tones.
                    for(int row=0;row<22;row++)
                    {
                        float low=Mathf.Max(lo,.173f*row+.008f),high=Mathf.Min(hi,.173f*(row+1)-.008f);
                        if(high<=low)continue;
                        for(int col=-7;col<=7;col++)
                        {
                            float start=col*.42f+(row%2)*.21f;
                            float l=Mathf.Max(a,start+.009f),r=Mathf.Min(b,start+.411f);
                            if(r<=l)continue;
                            int variation=Mathf.Abs((col*37+row*13+bay*17+storey*7)%9);
                            var shade=variation<3?brickLight:variation<5?brickDark:brick;
                            shell.Quad(shade,At(l,low,.113f),At(r,low,.113f),At(r,high,.113f),At(l,high,.113f),n);
                        }
                    }
                }
                Block(-width/2,-span/2,0,height); Block(span/2,width/2,0,height);
                Block(-span/2,span/2,0,sill); Block(-span/2,span/2,lintel,height);
                foreach(int side in new[]{-1,1})
                    shell.Box(doorway?m.stone:plaster,At(side*(span/2+.046f),(sill+lintel)/2,.11f),
                        Size(.102f,lintel-sill,.14f));
                shell.Box(m.stone,At(0,lintel+.067f,.11f),Size(span+.33f,.145f,.20f));
                shell.Box(m.stone,At(0,height-.12f,.10f),Size(width,.106f,.20f));
                interior.Box(plaster,At(0,height-.29f,-.164f),Size(width,.098f,.105f));
                if(doorway)
                {
                    // Fixed-open double leaves park along the internal walls.
                    foreach(var hand in new[]{-1,1})
                    {
                        var leaf=At(hand*(span/2-.038f),1.21f,-.47f);
                        Solid(interior,green,"Open station entrance leaf",leaf,Size(.066f,2.40f,.74f));
                        interior.Box(m.bronze,leaf+(-n*.24f)+axis*(-hand*.065f),Size(.05f,.21f,.035f));
                    }
                    return;
                }
                var s=At(0,(sill+lintel)/2);
                shell.Box(glass,s,Size(span-.05f,lintel-sill-.035f,.018f));
                Box(house,"Sash glass",s,Size(span,lintel-sill,.052f));
                foreach(int side in new[]{-1,1})
                    shell.Box(green,At(side*(span/2-.035f),(sill+lintel)/2),Size(.08f,lintel-sill,.11f));
                for(int i=-1;i<=1;i++)
                    shell.Box(plaster,At(i*span/4,(sill+lintel)/2,.04f),Size(.033f,lintel-sill,.051f));
                foreach(float h in new[]{sill+.035f,sill+(lintel-sill)*.58f,lintel-.025f})
                    shell.Box(plaster,At(0,h,.034f),Size(span,.06f,.086f));
                shell.Box(m.stone,At(0,sill-.034f,.07f),Size(span+.23f,.104f,.37f));
            }

            void Roof()
            {
                foreach(int side in new[]{-1,1})
                {
                    float x=side<0 ? -9.52f:.52f;
                    var a=P(x,Eaves,-10.63f); var b=P(x,Eaves,10.63f);
                    var c=P(-4.5f,9.15f,10.63f); var d=P(-4.5f,9.15f,-10.63f);
                    shell.Quad(m.soot,a,b,c,d,Vector3.down);
                    shell.Quad(m.snow,a+Vector3.up*.085f,b+Vector3.up*.085f,c+Vector3.up*.085f,d+Vector3.up*.085f,Vector3.up);
                    shell.Box(m.soot,P(x,Eaves,0),P(.114f,.16f,21.38f));
                    shell.Box(plaster,P(x,Eaves-.09f,0),P(.083f,.095f,21.38f));
                    for(int end=-1;end<=1;end+=2)
                        shell.Beam(plaster,P(x,Eaves,end*10.63f),P(-4.5f,9.15f,end*10.63f),.13f,.20f);
                }
                for(int end=-1;end<=1;end+=2)
                {
                    float z=end*10;
                    shell.Triangle(brick,P(0,Eaves,z),P(-9,Eaves,z),P(-4.5f,9.15f,z),Vector3.forward*end);
                    shell.Triangle(plaster,P(0,Eaves,z-end*.1f),P(-9,Eaves,z-end*.1f),P(-4.5f,9.15f,z-end*.1f),Vector3.back*end);
                    for(int k=-2;k<=2;k++)
                    {
                        float h=1.93f-.47f*Mathf.Abs(k);shell.Box(green,P(-4.5f+k*.77f,Eaves+h/2,z+end*.045f),P(.09f,h,.11f));
                    }
                    shell.Ring(m.pine,P(.23f,.04f,end*9.66f),P(.23f,6.86f,end*9.66f),.066f,.060f);
                    shell.Box(brickDark,P(-5.60f,9.1f,end*6.7f),P(1.22f,2.31f,.69f));
                    shell.Box(m.stone,P(-5.60f,10.23f,end*6.7f),P(1.38f,.19f,.83f));
                    foreach(float dx in new[]{-.34f,.34f})
                    {
                        shell.Ring(brick,P(-5.6f+dx,10.30f,end*6.7f),P(-5.6f+dx,10.81f,end*6.7f),.153f,.175f);
                        shell.Ring(m.soot,P(-5.6f+dx,10.80f,end*6.7f),P(-5.6f+dx,10.815f,end*6.7f),.117f,.117f);
                    }
                }
                Solid(shell,plaster,"Station attic ceiling",P(-4.5f,Eaves+.005f,0),P(9,.12f,20));
                // A shallow iron-and-slate canopy marks the two street-level entrances.
                shell.Box(m.soot,P(.83f,3.10f,0),P(1.85f,.12f,18.63f),Quaternion.Euler(0,0,-4));
                shell.Box(m.snow,P(.83f,3.18f,0),P(1.86f,.055f,18.66f),Quaternion.Euler(0,0,-4));
                for(int i=-3;i<=3;i++)
                {
                    float z=i*2.79f;
                    shell.Beam(green,P(.13f,2.56f,z),P(1.49f,3.0f,z),.075f,.075f);
                    shell.Ring(m.pine,P(.18f,2.64f,z),P(1.13f,3.01f,z),.029f,.019f);
                }
            }

            void Stair()
            {
                const float start=-2.44f, width=1.80f;
                const int steps=19;
                float rise=Upper/steps, run=(4.04f-start)/steps;
                for(int i=0;i<steps;i++)
                {
                    float h=(i+1)*rise,z=start+(i+.5f)*run;
                    var size=P(width,h,run+.002f);
                    interior.Box(m.wood,P(-7.86f,h/2,z),size);
                    interior.Box(m.bronze,P(-7.86f,h+.001f,z-run/2+.018f),P(width,.003f,.032f));
                }
                float length=run*steps;
                float angle=-Mathf.Atan2(Upper,length)*Mathf.Rad2Deg;
                var slope=Box(house,"Oak stair walking surface",P(-7.86f,Upper/2+.04f,(4.04f+start)/2),
                    P(width,.16f,Mathf.Sqrt(length*length+Upper*Upper)));
                slope.transform.localRotation=Quaternion.Euler(angle,0,0);
                foreach(float x in new[]{-8.72f,-7.0f})
                {
                    for(int i=0;i<=steps;i+=2)
                    {
                        float z=start+i*run,h=i*rise;
                        interior.Ring(green,P(x,h+.06f,z),P(x,h+.90f,z),.038f,.028f,8);
                    }
                    interior.Beam(m.wood,P(x,.89f,start),P(x,Upper+.89f,4.04f),.088f,.080f);
                }
                var stairRail=Box(house,"Stair handrail",P(-6.94f,Upper/2+.50f,(4.04f+start)/2),P(.085f,.91f,Mathf.Sqrt(length*length+Upper*Upper)));
                stairRail.transform.localRotation=Quaternion.Euler(angle,0,0);
                void Guard(Vector3 a,Vector3 b)
                {
                    interior.Beam(m.wood,a+Vector3.up*.95f,b+Vector3.up*.95f,.10f,.09f);
                    int n=Mathf.CeilToInt((a-b).magnitude/.39f);
                    for(int i=0;i<=n;i++)
                    {
                        var p=Vector3.Lerp(a,b,i/(float)n);interior.Ring(green,p,p+Vector3.up*.94f,.031f,.023f,8);
                    }
                    var g=Box(house,"Upper landing balustrade",(a+b)/2+Vector3.up*.48f,P(.092f,.97f,(a-b).magnitude));
                    g.transform.localRotation=Quaternion.LookRotation((b-a).normalized);
                }
                Guard(P(-6.70f,Upper,-2.98f),P(-6.70f,Upper,3.91f));
                Guard(P(-8.91f,Upper,-3.02f),P(-6.72f,Upper,-3.02f));
            }

            void Rooms()
            {
                // A wide door connects the luggage room and booking hall at the platform end.
                Solid(interior,plaster,"Parcels partition",P(-5.55f,1.52f,-4.12f),P(6.72f,3.04f,.13f));
                Solid(interior,plaster,"Parcels partition return",P(-.35f,1.52f,-4.12f),P(.50f,3.04f,.13f));
                interior.Box(m.wood,P(-1.4f,2.87f,-4.12f),P(1.66f,.36f,.15f));
                // Parcels are tagged and lashed on a dedicated baggage rack.
                for(int i=0;i<4;i++)
                {
                    float z=-8.7f+i*.99f;
                    foreach(float x in new[]{-8.30f,-7.18f}) interior.Box(m.wood,P(x,.98f,z),P(.095f,1.96f,.09f));
                }
                for(int tier=0;tier<2;tier++)
                {
                    float h=.24f+tier*.92f;
                    Solid(interior,m.wood,"Parcel shelf",P(-7.75f,h,-7.20f),P(1.29f,.09f,3.38f));
                    for(int i=0;i<3;i++)
                    {
                        float z=-8.26f+i*.96f+.10f*tier;
                        var p=P(-7.74f,h+.26f,z); var dim=P(.78f,.42f,.69f);
                        interior.Box(i%2==0?m.canvas:m.oxblood,p,dim);
                        for(int side=-1;side<=1;side+=2)interior.Box(m.khaki,p+Vector3.forward*side*.21f,dim+P(.008f,.008f,-.64f));
                        interior.Box(plaster,p+P(.399f,.03f,.04f),P(.008f,.091f,.127f));
                    }
                }
                Box(house,"Baggage shelving",P(-7.75f,1.03f,-7.2f),P(1.33f,2.06f,3.45f));
                // Rail ticket counter with a brass grille and glazed clerk's screen.
                Solid(interior,green,"Booking counter",P(-6.57f,.57f,6.25f),P(.94f,1.14f,4.67f));
                interior.Box(m.wood,P(-6.57f,1.16f,6.25f),P(1.08f,.09f,4.82f));
                foreach(float z in new[]{4.11f,5.51f,6.99f,8.41f}) interior.Box(m.wood,P(-6.38f,1.92f,z),P(.091f,1.51f,.10f));
                interior.Box(m.wood,P(-6.38f,2.64f,6.25f),P(.129f,.14f,4.49f));
                for(int j=0;j<29;j++)
                {
                    float z=4.14f+j*.15f;float bottom=Mathf.Abs(z-6.25f)<.48f?1.67f:1.22f;
                    interior.Ring(m.bronze,P(-6.34f,bottom,z),P(-6.34f,2.57f,z),.012f,.012f,6);
                }
                interior.Box(m.bronze,P(-6.34f,1.96f,6.25f),P(.038f,.038f,4.19f));
                interior.Box(glass,P(-6.40f,2.22f,6.25f),P(.018f,.70f,4.19f));
                Box(house,"Booking grille",P(-6.38f,1.95f,6.25f),P(.09f,1.52f,4.51f));
                for(int i=0;i<12;i++)
                    for(int row=0;row<3;row++)
                    {
                        var p=P(-8.69f,1.0f+row*.29f,4.31f+i*.32f);
                        interior.Box(m.wood,p,P(.19f,.26f,.30f));
                        interior.Box(m.khaki,p+P(.097f,.018f,0),P(.007f,.094f,.182f));
                    }
                foreach(float z in new[]{-1.8f,1.25f}) BenchAt(-1.10f,0,z);
                // The duty room opens off the upper landing; a communication desk faces the line.
                Solid(interior,plaster,"Duty room inner wall",P(-5.23f,Upper+1.49f,-2.30f),P(3.05f,2.98f,.13f));
                Solid(interior,plaster,"Duty room arch return",P(-.87f,Upper+1.49f,-2.30f),P(1.47f,2.98f,.13f));
                interior.Box(m.wood,P(-2.64f,Upper+2.63f,-2.30f),P(2.12f,.33f,.15f));
                Desk(-2.05f,Upper,-6.10f);
                BenchAt(-1.08f,Upper,1.7f);
                for(int i=0;i<7;i++)
                {
                    var p=P(-6.74f,Upper+1.01f,-8.93f+i*.37f);
                    interior.Box(i%2==0?m.oxblood:green,p,P(.35f,.32f,.26f));
                    interior.Box(m.khaki,p+P(.19f,.02f,0),P(.018f,.081f,.16f));
                }
                Solid(interior,m.wood,"Register table",P(-6.8f,Upper+.44f,-7.83f),P(.74f,.88f,2.84f));
                // Keep the portal approach in the booking hall clear of furniture.
                ClockFace(-8.82f,2.75f,.75f);
                ClockFace(-8.82f,Upper+2.37f,-7.46f);
                ClockFace(-6.29f,2.74f,4.55f);
            }

            void BenchAt(float x,float y,float z)
            {
                foreach(var dz in new[]{-.62f,.62f})
                    foreach(var dx in new[]{-.19f,.19f}) interior.Box(green,P(x+dx,y+.23f,z+dz),P(.075f,.46f,.11f));
                Solid(interior,m.wood,"Waiting bench",P(x,y+.46f,z),P(.57f,.10f,1.75f));
                interior.Box(m.wood,P(x+.235f,y+.76f,z),P(.068f,.49f,1.75f));
                Box(house,"Bench back",P(x+.235f,y+.76f,z),P(.068f,.49f,1.75f));
            }
            void Desk(float x,float y,float z)
            {
                for(int side=-1;side<=1;side+=2)
                    Solid(interior,m.wood,"Desk pedestal",P(x,y+.41f,z+side*.73f),P(.78f,.82f,.52f));
                Solid(interior,m.wood,"Dispatch desk top",P(x,y+.85f,z),P(.97f,.088f,2.21f));
                interior.Box(green,P(x,y+.903f,z),P(.74f,.025f,1.67f));
                interior.Box(m.khaki,P(x,y+.926f,z+.38f),P(.42f,.026f,.56f),Quaternion.Euler(0,-12,0));
                for(int i=0;i<7;i++)interior.Box(m.soot,P(x+.14f-i*.045f,y+.943f,z+.38f),P(.009f,.004f,.32f));
                // Telegraph brass key and bell on an ebony plinth.
                var t=P(x,y+.97f,z-.69f);
                interior.Box(m.soot,t,P(.38f,.11f,.23f));
                interior.Beam(m.bronze,t+P(-.13f,.09f,0),t+P(.12f,.12f,0),.032f,.043f);
                interior.Ring(m.soot,t+P(.12f,.13f,0),t+P(.12f,.16f,0),.053f,.061f);
                interior.Ring(m.bronze,t+P(-.13f,.07f,.07f),t+P(-.13f,.16f,.07f),.067f,.037f);
                var lamp=P(x-.11f,y+.916f,z+.90f);
                interior.Ring(m.bronze,lamp,lamp+Vector3.up*.045f,.139f,.11f);
                interior.Ring(m.bronze,lamp+Vector3.up*.045f,lamp+Vector3.up*.34f,.028f,.023f);
                interior.Beam(m.bronze,lamp+P(0,.34f,0),lamp+P(-.08f,.40f,0),.045f,.035f);
                interior.Box(green,lamp+P(-.09f,.39f,0),P(.20f,.12f,.33f));
                interior.Box(m.lamp,lamp+P(-.09f,.329f,0),P(.17f,.009f,.30f));
                var warm=Child(house,"Ledger lamp",lamp+P(-.09f,.30f,0)).AddComponent<Light>();
                warm.type=LightType.Point;warm.color=new Color(1,.80f,.52f);warm.range=2.3f;warm.intensity=.32f;
                float cx=x-1;
                foreach(float side in new[]{-.21f,.21f})
                    foreach(float end in new[]{-.23f,.23f})
                        interior.Box(m.wood,P(cx+side,y+.23f,z+end),P(.075f,.46f,.074f));
                Solid(interior,m.oxblood,"Stationmaster chair",P(cx,y+.49f,z),P(.57f,.13f,.60f));
                foreach(float dz in new[]{-.235f,.235f})
                    interior.Box(m.wood,P(cx-.23f,y+.74f,z+dz),P(.065f,.63f,.071f));
                interior.Box(m.oxblood,P(cx-.23f,y+.89f,z),P(.08f,.33f,.55f));
                Box(house,"Chair back",P(cx-.23f,y+.87f,z),P(.08f,.40f,.55f));
            }
            void ClockFace(float x,float y,float z)
            {
                var centre=P(x,y,z);
                interior.Ring(m.bronze,centre,centre+Vector3.right*.07f,.39f,.39f,36);
                centre+=Vector3.right*.075f;
                interior.Ring(plaster,centre,centre+Vector3.right*.01f,.344f,.344f,48);
                centre+=Vector3.right*.014f;
                for(int i=0;i<12;i++)
                {
                    float a=i*Mathf.PI/6;
                    var d=P(0,Mathf.Cos(a),Mathf.Sin(a));
                    interior.Beam(m.soot,centre+d*.27f,centre+d*.312f,.014f,.019f);
                }
                interior.Beam(m.soot,centre,centre+P(0,.14f,-.1f),.021f,.019f);
                interior.Beam(m.soot,centre,centre+P(0,.04f,.24f),.012f,.012f);
            }
            void Sign(Vector3 centre,float width,int row,bool positive=true)
            {
                float sign=positive?1:-1;
                interior.Box(m.bronze,centre,P(.030f,width/7.5f+.022f,width+.038f));
                interior.Sign(signs,centre+Vector3.right*sign*.018f,width,width/7.5f,row,positive);
            }
            void SignsAndLights()
            {
                Sign(P(.16f,5.99f,0),3.73f,0);
                Sign(P(1.80f,3.03f,0),3.65f,0);
                Sign(P(.15f,2.79f,7.5f),2.15f,1);
                Sign(P(.15f,2.79f,-7.5f),2.15f,2);
                Sign(P(-6.29f,2.51f,6.25f),1.25f,4);
                Sign(P(-3.95f,Upper+2.5f,-2.23f),1.69f,3);
                Sign(P(-.17f,2.78f,7.5f),1.70f,5,false);
                var centre=P(-8.855f,1.8f,-1.15f);
                interior.Box(m.wood,centre,P(.047f,1.74f,1.36f));
                var f=centre+Vector3.right*.026f;
                var right=Vector3.forward*.627f;var up=Vector3.up*.806f;
                interior.Quad(timetable,f-right-up,f+right-up,f+right+up,f-right+up,Vector3.right);
                foreach(float level in new[]{0,Upper})
                    foreach(float z in new[]{-6.5f,0f,6.6f})
                    {
                        var p=P(-3.47f,level+2.66f,z);
                        interior.Ring(m.bronze,p+P(0,.06f,0),p+P(0,.59f,0),.022f,.027f);
                        interior.Ring(m.bronze,p+P(0,.02f,0),p+P(0,.17f,0),.19f,.071f);
                        interior.Ring(m.lamp,p-Vector3.up*.18f,p+Vector3.up*.025f,.162f,.17f,16);
                        var l=Child(house,"Opal pendant",p-Vector3.up*.24f).AddComponent<Light>();
                        l.type=LightType.Point;l.color=new Color(1,.79f,.53f);l.range=7f;l.intensity=1.5f;l.shadows=LightShadows.None;
                    }
                foreach(float z in new[]{-7.5f,0,7.5f})
                {
                    Vector3 p=P(.96f,5.03f,z);
                    shell.Beam(m.pine,p-Vector3.right*.88f,p,.056f,.056f);
                    shell.Beam(m.pine,p-Vector3.right*.65f-Vector3.up*.25f,p,.041f,.041f);
                    shell.Ring(green,p+Vector3.up*.036f,p-Vector3.up*.11f,.065f,.21f);
                    shell.Ring(m.lamp,p-Vector3.up*.18f,p-Vector3.up*.11f,.126f,.16f);
                    var l=Child(house,"Platform wall lantern",p-Vector3.up*.20f).AddComponent<Light>();
                    l.type=LightType.Point;l.color=new Color(1,.80f,.59f);l.intensity=2.6f;l.range=6f;
                }
            }
        }
    }
}
