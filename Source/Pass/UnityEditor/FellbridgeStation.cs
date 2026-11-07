using UnityEngine;
using static Winterbourne.Editor.StationFabrication;

namespace Winterbourne.Editor
{
    public static partial class PassFittings
    {
        const float StationOrigin=(PlatformFirst+PlatformLast)/2;
        const float Edge=-1.76f, Back=-8.82f;
        static Vector3 PlatformPoint(float x,float height,float chainage)
            => new Vector3(x,PlatformTop+height,StationOrigin-chainage);

        static void MakeStation(Finishes m)
        {
            var station=new GameObject("Fellbridge Station").transform;
            station.position=new Vector3(0,0,-StationOrigin);
            var structure=Child(station,"Platform structure",Vector3.zero).transform;
            var concourse=Child(station,"Passenger shelters",Vector3.zero).transform;
            var furniture=Child(station,"Furniture and wayfinding",Vector3.zero).transform;
            float depth=Edge-Back,length=PlatformLast-PlatformFirst;
            var body=new StationFabrication();
            float midX=(Back+Edge)/2;
            body.Box(m.masonry,new Vector3(midX,.48f,0),new Vector3(depth,1.66f,length));
            body.Box(m.stone,new Vector3(midX,PlatformTop-.035f,0),new Vector3(depth,.07f,length));
            // Weathered stone courses are discontinuous across each bond, rather than
            // repeated coplanar faces below the train window.
            for(int row=0;row<3;row++)
            {
                for(float s=PlatformFirst+.05f-(row%2)*.60f;s<PlatformLast;s+=1.20f)
                {
                    float a=Mathf.Max(PlatformFirst,s),b=Mathf.Min(PlatformLast,s+1.175f);
                    if(b-a<.01f)continue;
                    body.Box(((int)(s*7)%5==0)?m.ash:m.masonry,
                             PlatformPoint(Edge+.011f,-1.020f+row*.31f,(a+b)/2),
                             new Vector3(.049f,.294f,b-a));
                }
            }
            for(int j=0;j<190;j++)
            {
                float s=PlatformFirst+j+.5f;
                body.Box(m.cream,PlatformPoint(Edge-.197f,-.036f,s),new Vector3(.391f,.080f,.990f));
                body.Box(m.soot,PlatformPoint(Edge-.469f,.002f,s),new Vector3(.043f,.009f,.980f));
                if(j%3==0) body.Box(m.ash,PlatformPoint(-5.60f,.003f,s),
                                     new Vector3(5.89f,.008f,.022f));
            }
            // The strip from the edge to the buildings has been cleared. Low drifts
            // accumulate unevenly against the back fence.
            Vector3 lastInner=default,lastOuter=default;
            for(int j=0;j<=190;j++)
            {
                float s=PlatformFirst+j;
                float noise=Mathf.PerlinNoise(s*.134f,12.7f);
                var inner=PlatformPoint(Back+.55f+1.33f*noise,.012f,s);
                var outer=PlatformPoint(Back,.044f+.27f*noise,s);
                if(j>0)body.Quad(m.snow,lastInner,inner,outer,lastOuter,Vector3.up);
                lastInner=inner;lastOuter=outer;
            }
            body.Finish(structure,"Fellbridge stone platform");
            Box(structure,"Platform floor",new Vector3(midX,.51f,0),new Vector3(depth,2*(PlatformTop-.51f),length));

            var fittings=new StationFabrication();
            for(float s=PlatformFirst;s<=PlatformLast;s+=2.50f)
            {
                fittings.Box(m.pine,PlatformPoint(Back+.11f,.62f,s),new Vector3(.105f,1.24f,.105f));
                fittings.Box(m.snow,PlatformPoint(Back+.11f,1.261f,s),new Vector3(.133f,.042f,.133f));
                if(s+2.49f<=PlatformLast)
                {
                    foreach(float h in new[]{.34f,.93f})
                        fittings.Box(m.pine,PlatformPoint(Back+.11f,h,s+1.25f),new Vector3(.061f,.064f,2.5f));
                }
            }
            Box(furniture,"Rear fence",PlatformPoint(Back+.11f,.62f,StationOrigin),
                new Vector3(.115f,1.24f,length));
            foreach(float end in new[]{PlatformFirst+.06f,PlatformLast-.06f})
            {
                for(float x=Back+.18f;x<Edge;x+=1.15f)
                    fittings.Box(m.pine,PlatformPoint(x,.49f,end),new Vector3(.08f,.98f,.08f));
                fittings.Box(m.pine,PlatformPoint(midX,.93f,end),new Vector3(depth,.067f,.068f));
                Box(furniture,"Platform end railing",PlatformPoint(midX,.50f,end),
                    new Vector3(depth,1f,.08f));
            }
            // At a 1,095 m head stop, the cabin 01 window in coach 07 lies at
            // 989.46 m. The enamel board stands in front of the shelter, in that sightline.
            foreach(float s in new[]{939f,990f,1030f,1082.5f})
                Nameboard(fittings,furniture,m,-4.61f,s);
            foreach(float s in new[]{931f,950f,969f,993f,1011f,1038f,1059f,1091f,1103f})
                Lantern(fittings,furniture,m,-4.18f,s,s==950f || s==993f || s==1038f || s==1059f);
            foreach(float s in new[]{946f,961f,987f,1007f,1034f,1076f})
                Bench(fittings,furniture,m,-5.73f,s);
            fittings.Finish(furniture,"Fellbridge wayside fittings");

            Shelter(concourse,m,"Fellbridge waiting room",985f,13f,1);
            Shelter(concourse,m,"Fellbridge parcels office",1053f,8.8f,3);
        }

        static void Nameboard(StationFabrication f,Transform parent,Finishes m,float x,float s)
        {
            foreach(float delta in new[]{-1.53f,1.53f})
            {
                f.Box(m.pine,PlatformPoint(x,.91f,s+delta),new Vector3(.125f,1.82f,.125f));
                f.Box(m.cream,PlatformPoint(x,1.89f,s+delta),new Vector3(.152f,.117f,.152f));
                f.Ring(m.bronze,PlatformPoint(x,1.949f,s+delta),PlatformPoint(x,2.043f,s+delta),.068f,.022f);
                Box(parent,"Nameboard post",PlatformPoint(x,.95f,s+delta),new Vector3(.14f,1.90f,.14f));
            }
            f.Box(m.wood,PlatformPoint(x+.013f,1.58f,s),new Vector3(.115f,.563f,3.13f));
            f.Box(m.pine,PlatformPoint(x+.022f,1.894f,s),new Vector3(.196f,.076f,3.30f));
            f.Box(m.snow,PlatformPoint(x+.013f,1.949f,s),new Vector3(.18f,.036f,3.24f));
            f.Sign(m.screen,PlatformPoint(x+.076f,1.58f,s),3.08f,.522f,0);
            if(Mathf.Abs(s-990f)<1)
            {
                // A hooded reading light washes the painted sign after dark.
                f.Beam(m.pine,PlatformPoint(x,1.91f,s),PlatformPoint(x+.34f,2.07f,s),.035f,.035f);
                f.Box(m.pine,PlatformPoint(x+.34f,2.074f,s),new Vector3(.097f,.056f,.485f));
                f.Box(m.lamp,PlatformPoint(x+.33f,2.043f,s),new Vector3(.066f,.014f,.423f));
                var glow=Child(parent,"Nameboard downlight",PlatformPoint(x+.37f,2.03f,s)).AddComponent<Light>();
                glow.type=LightType.Spot;glow.color=new Color(1f,.84f,.64f);
                glow.range=3.5f;glow.intensity=1.12f;glow.spotAngle=128f;glow.innerSpotAngle=85f;
                glow.transform.rotation=Quaternion.LookRotation(new Vector3(-.31f,-.45f,0));
                glow.shadows=LightShadows.None;
            }
        }

        static void Lantern(StationFabrication f,Transform parent,Finishes m,float x,float s,bool castLight)
        {
            Vector3 P(float h)=>PlatformPoint(x,h,s);
            foreach(var seg in new[]{(0f,.16f,.185f,.175f),(.16f,.34f,.139f,.103f),
                (.34f,.64f,.101f,.065f),(.64f,2.75f,.065f,.048f),(2.75f,2.82f,.099f,.099f),
                (2.82f,2.91f,.072f,.138f)})
                f.Ring(m.pine,P(seg.Item1),P(seg.Item2),seg.Item3,seg.Item4);
            for(int side=0;side<4;side++)
            {
                float angle=side*Mathf.PI/2+Mathf.PI/4;
                var radial=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
                f.Ring(m.pine,P(2.93f)+radial*.17f,P(3.384f)+radial*.255f,.015f,.014f,6);
            }
            // Square glass lantern, pyramidal cap and finial.
            f.Ring(m.lamp,P(2.924f),P(3.377f),.153f,.232f,4);
            f.Ring(m.pine,P(3.375f),P(3.403f),.267f,.268f,4);
            f.Ring(m.soot,P(3.403f),P(3.571f),.25f,.052f,4);
            f.Ring(m.bronze,P(3.562f),P(3.665f),.042f,.012f);
            Box(parent,"Lamp post",P(1.43f),new Vector3(.12f,2.86f,.12f));
            if(castLight)
            {
                var light=Child(parent,"Station lantern",P(2.894f)+Vector3.right*.14f).AddComponent<Light>();
                light.type=LightType.Point;light.color=new Color(1,.727f,.44f);
                light.range=6.2f;light.intensity=2.35f;light.shadows=LightShadows.None;
            }
        }

        static void Bench(StationFabrication f,Transform parent,Finishes m,float x,float s)
        {
            foreach(float dz in new[]{-.69f,.69f})
            {
                foreach(float dx in new[]{-.216f,.216f})
                    f.Beam(m.pine,PlatformPoint(x+dx,.032f,s+dz),PlatformPoint(x+dx*.8f,.443f,s+dz),.051f,.052f);
                f.Beam(m.pine,PlatformPoint(x-.21f,.415f,s+dz),PlatformPoint(x-.31f,.954f,s+dz),.046f,.046f);
            }
            for(int i=0;i<4;i++)
                f.Box(m.wood,PlatformPoint(x-.188f+i*.122f,.469f,s),new Vector3(.108f,.063f,2.05f));
            for(int i=0;i<3;i++)
                f.Box(m.wood,PlatformPoint(x-.266f-i*.021f,.671f+i*.115f,s),new Vector3(.054f,.091f,2.05f));
            Box(parent,"Oak platform seat",PlatformPoint(x,.26f,s),new Vector3(.57f,.52f,2.06f));
            Box(parent,"Seat back",PlatformPoint(x-.285f,.73f,s),new Vector3(.075f,.48f,2.05f));
        }

        static void Shelter(Transform station,Finishes m,string name,float s,float length,int sign)
        {
            var root=Child(station,name,PlatformPoint(-6.73f,0,s)).transform;
            var g=new StationFabrication();
            // Open-front timber building: separate posts, back wall, roof and canopy,
            // with no invisible box closing off the public shelter.
            float xBack=-1.37f,xFront=1.01f;
            g.Box(m.masonry,new Vector3(xBack,.41f,0),new Vector3(.17f,.82f,length));
            g.Box(m.cream,new Vector3(xBack,1.74f,0),new Vector3(.126f,1.88f,length));
            g.Box(m.wood,new Vector3(xBack+.078f,.92f,0),new Vector3(.042f,.11f,length));
            Box(root,"Waiting room rear wall",new Vector3(xBack,1.33f,0),new Vector3(.17f,2.66f,length));
            for(int side=-1;side<=1;side+=2)
            {
                float z=side*(length/2-.055f);
                g.Box(m.cream,new Vector3(-.44f,1.31f,z),new Vector3(1.84f,2.62f,.11f));
                g.Box(m.wood,new Vector3(-.44f,.83f,z-side*.061f),new Vector3(1.84f,.079f,.036f));
                Box(root,"Shelter end",new Vector3(-.44f,1.31f,z),new Vector3(1.84f,2.62f,.11f));
            }
            for(int i=0;i<4;i++)
            {
                float z=Mathf.Lerp(-length*.48f,length*.48f,i/3f);
                g.Box(m.pine,new Vector3(xFront,1.30f,z),new Vector3(.117f,2.60f,.117f));
                g.Box(m.cream,new Vector3(xFront,1.88f,z),new Vector3(.133f,.077f,.133f));
                g.Beam(m.pine,new Vector3(xFront,2.13f,z),new Vector3(xFront+.65f,2.72f,z),.064f,.066f);
                g.Beam(m.pine,new Vector3(xFront,2.13f,z),new Vector3(xFront,2.72f,z+.54f),.064f,.066f);
                Box(root,"Canopy post",new Vector3(xFront,1.3f,z),new Vector3(.126f,2.6f,.126f));
            }
            g.Box(m.pine,new Vector3(xFront+.07f,2.645f,0),new Vector3(.256f,.126f,length+.7f));
            float z0=-length/2-.41f,z1=length/2+.41f;
            foreach(float side in new[]{-1f,1f})
            {
                float x=side<0 ? -1.73f : 1.86f;
                var a=new Vector3(x,2.70f,z0);var b=new Vector3(x,2.70f,z1);
                var c=new Vector3(.02f,3.22f,z1);var d=new Vector3(.02f,3.22f,z0);
                g.Quad(m.soot,a,b,c,d,Vector3.down);
                g.Quad(m.snow,a+Vector3.up*.061f,b+Vector3.up*.061f,
                       c+Vector3.up*.061f,d+Vector3.up*.061f,Vector3.up);
                g.Box(m.cream,new Vector3(x,2.686f,0),new Vector3(.065f,.093f,z1-z0));
            }
            g.Box(m.pine,new Vector3(1.12f,2.330f,0),new Vector3(.092f,.346f,2.21f));
            g.Sign(m.screen,new Vector3(1.175f,2.332f,0),2.17f,.31f,sign);
            for(int i=0;i<2;i++)
            {
                float z=(i==0?-.25f:.25f)*length;
                g.Box(m.wood,new Vector3(xBack+.101f,1.73f,z),new Vector3(.063f,.92f,.72f));
                g.Box(m.khaki,new Vector3(xBack+.138f,1.73f,z),new Vector3(.013f,.839f,.648f));
                for(int j=0;j<10;j++)
                    g.Box(m.soot,new Vector3(xBack+.147f,1.446f+j*.056f,z),new Vector3(.006f,.010f,.485f));
            }
            g.Finish(root,name+" kit");
        }
    }
}
