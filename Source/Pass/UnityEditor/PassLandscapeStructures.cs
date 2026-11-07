using UnityEngine;

namespace Winterbourne.Editor
{
    public static partial class PassLandscape
    {
        static Vector3 Arch(float r,float a,float s)
        {
            return W(Mathf.Cos(a)*r,2.67f+Mathf.Sin(a)*r,s);
        }

        static void ArchBand(Batch b,float s,float thickness,float inside,float outside,bool dressed)
        {
            const int pieces=23;
            for(int i=0;i<pieces;i++){
                float a=i*Mathf.PI/pieces, j=(i+1)*Mathf.PI/pieces;
                float seam=dressed?.0035f:0;
                Vector3 inward=-new Vector3(Mathf.Cos((a+j)/2),Mathf.Sin((a+j)/2),0);
                Paint mat=dressed ? (i%4==0 ? Paint.Shale:Paint.BrickLight):Paint.Brick;
                float key=dressed&&i==pieces/2?.21f:0;
                var lo=Arch(inside-key*.35f,a+seam,s);
                var hi=Arch(inside-key*.35f,j-seam,s);
                var top=Arch(outside+key,j-seam,s);
                var bottom=Arch(outside+key,a+seam,s);
                Vector3 d=Vector3.back*thickness;
                b.Quad(mat,lo,hi,top,bottom,Vector3.forward);
                b.Quad(mat,lo+d,hi+d,top+d,bottom+d,Vector3.back);
                b.Quad(mat,lo,hi,hi+d,lo+d,inward);
            }
        }

        static void Portal(Batch b,float s,int sign)
        {
            float sf=s+sign*.47f;
            Vector3 toward=Vector3.back*sign;
            // Masonry face with a genuine arched aperture and a built-up stone cap.
            for(int i=0;i<26;i++){
                float a=Mathf.PI*i/26,j=Mathf.PI*(i+1)/26;
                var p=Arch(4.48f,a,sf);var q=Arch(4.48f,j,sf);
                b.Quad(Paint.Brick,p,q,W(q.x,8.3f,sf),W(p.x,8.3f,sf),toward);
            }
            b.Block(Paint.Brick,W(0,8.56f,sf-sign*.24f),new Vector3(13.25f,.54f,.83f));
            b.Block(Paint.Shale,W(0,8.91f,sf-sign*.24f),new Vector3(13.59f,.18f,.96f));
            b.Block(Paint.Snow,W(0,9.02f,sf-sign*.24f),new Vector3(13.64f,.09f,1.02f));
            for(int h=-1;h<=1;h+=2){
                b.Block(Paint.Brick,W(h*5.57f,4.18f,sf-sign*.25f),new Vector3(2.18f,8.34f,.80f));
                for(int k=0;k<6;k++){
                    float z=k*.448f+.19f;
                    b.Block(k%3==0?Paint.Shale:Paint.BrickLight,W(h*4.24f,z,sf+sign*.054f),new Vector3(.51f,.431f,.76f));
                }
                // Battered wing walls flank the approach cutting.
                Vector3 a=W(h*5.41f,5.5f,sf),end=W(h*7.5f,2.4f,sf+sign*6.6f);
                b.Quad(Paint.Brick,a,end,W(end.x,-.3f,sf+sign*6.6f),W(a.x,-.3f,sf),Vector3.left*h);
                b.Bar(Paint.Snow,a,end,.49f,.12f);
                b.Block(Paint.BrickLight,W(h*6.10f,4.23f,sf+sign*.18f),new Vector3(.71f,8.60f,.72f));
                b.Block(Paint.Snow,W(h*6.10f,8.59f,sf+sign*.18f),new Vector3(.83f,.11f,.89f));
            }
            ArchBand(b,sf-.25f,.5f,4.0f,4.55f,true);
        }

        static void Bore()
        {
            var b=new Batch();const float R=4.16f;
            for(int s=480;s<780;s+=4)
            {
                for(int i=0;i<22;i++){
                    float a=i*Mathf.PI/22,j=(i+1)*Mathf.PI/22;
                    b.Quad(Paint.Brick,Arch(R,a,s),Arch(R,j,s),Arch(R,j,s+4),Arch(R,a,s+4),
                        -new Vector3(Mathf.Cos((a+j)/2),Mathf.Sin((a+j)/2),0));
                }
                for(int h=-1;h<=1;h+=2){
                    b.Block(Paint.Mortar,W(h*4.25f,1.22f,s+2),new Vector3(.20f,2.93f,4));
                    for(int j=0;j<5;j++){
                        for(int k=0;k<3;k++){
                            float x=4.13f*h,y=.21f+j*.516f;
                            float off=(j%2)*.30f;
                            float from=k*1.333f+off,through=Mathf.Min(from+1.302f,3.993f);
                            b.Block((k+j+s)%5==0?Paint.Basalt:Paint.Brick,W(x,y,s+(from+through)/2),
                                    new Vector3(.070f,.493f,through-from));
                            if(k==0 && off>0)b.Block(Paint.Brick,W(x,y,s+off*.5f),new Vector3(.07f,.493f,off-.024f));
                        }
                    }
                    b.Bar(Paint.Cable,W(h*3.99f,2.37f,s),W(h*3.99f,2.37f,s+4),.046f,.046f);
                }
                // Surface over the bore, joined to the shoulders of the fell; coal-black tube underneath.
                for(int k=-4;k<4;k++){
                    float x=k*3.30f,xx=(k+1)*3.30f;
                    float Crown(float u,float d) => Mathf.Max(7.32f+6.8f*(1-Mathf.Abs(u)/15)+2*Mathf.PerlinNoise(u*.12f+45,d*.10f),
                        Winterbourne.Pass.PassAtmosphere.GroundHeight(u,d)+.08f);
                    float taper=Mathf.SmoothStep(.12f,1,Mathf.Clamp01(Mathf.Min(s-480,780-s)/20f));
                    float Roof(float u,float chain) => Mathf.Lerp(8.6f,Crown(u,chain),taper);
                    b.Quad(Paint.Terrain,W(x,Roof(x,s),s),W(xx,Roof(xx,s),s),
                        W(xx,Roof(xx,s+4),s+4),W(x,Roof(x,s+4),s+4),Vector3.up);
                }
            }
            for(int s=486;s<780;s+=12){
                ArchBand(b,s,.42f,3.98f,4.23f,false);
                for(int h=-1;h<=1;h+=2) b.Block(Paint.Brick,W(h*4.08f,1.25f,s+.20f),new Vector3(.22f,2.88f,.42f));
            }
            for(int s=491;s<780;s+=20){
                for(int h=-1;h<=1;h+=2){
                    int off=h==1?6:0;
                    b.Block(Paint.Iron,W(h*3.99f,2.68f,s+off),new Vector3(.17f,.55f,.39f));
                    b.Block(Paint.Amber,W(h*3.88f,2.67f,s+off),new Vector3(.11f,.28f,.205f));
                    for(int k=-1;k<=1;k++)b.Block(Paint.Iron,W(h*3.805f,2.67f+.096f*k,s+off),new Vector3(.025f,.016f,.216f));
                    b.Block(Paint.Iron,W(h*3.908f,2.900f,s+off),new Vector3(.26f,.06f,.40f));
                    var lamp=new GameObject("Bore lamp "+(s+off));lamp.transform.SetParent(landscape,false);
                    lamp.transform.position=W(h*3.71f,2.65f,s+off);
                    var l=lamp.AddComponent<Light>();l.type=LightType.Point;l.color=new Color(1,.59f,.26f);
                    l.intensity=3.0f;l.range=8.9f;l.shadows=LightShadows.None;
                }
            }
            Portal(b,480,-1);Portal(b,780,1);
            b.Save("Black Rigg tunnel");
        }

        static void Pier(Batch b,float s,float depth,bool broken=false)
        {
            float floor=Winterbourne.Pass.PassAtmosphere.GroundHeight(0,s)-1.8f;
            if(floor>-.9f)floor=-1.4f;
            float baseWidth=4.35f+Mathf.Abs(floor)*.033f;
            float cap=broken ? floor+Mathf.Abs(floor)*.51f : depth;
            var bot=new[]{W(-baseWidth,floor,s-2.5f),W(baseWidth,floor,s-2.5f),W(baseWidth,floor,s+2.5f),W(-baseWidth,floor,s+2.5f)};
            var top=new[]{W(-3.65f,cap,s-1.52f),W(3.65f,cap,s-1.52f),W(3.65f,cap,s+1.52f),W(-3.65f,cap,s+1.52f)};
            if(broken){top[0].y-=4.6f;top[1].y-=1.6f;top[3].y-=2.8f;}
            Vector3 centre=W(0,0,s);
            for(int k=0;k<4;k++){int j=(k+1)%4;Vector3 dir=(bot[k]+bot[j])*.5f-centre;dir.y=0;
                b.Quad(k%2==0?Paint.Basalt:Paint.Shale,bot[k],bot[j],top[j],top[k],dir);}
            b.Quad(Paint.Shale,top[3],top[2],top[1],top[0],Vector3.up);
            if(!broken){
                b.Block(Paint.Shale,W(0,cap,s),new Vector3(7.89f,.65f,3.59f));
                b.Block(Paint.Snow,W(0,cap+.35f,s),new Vector3(7.84f,.085f,3.58f));
                foreach(float h in new[]{-1f,1f})b.Block(Paint.Iron,W(h*2.41f,cap+.51f,s),new Vector3(.84f,.29f,1.03f));
            }
            // Horizontal masonry string courses mark the height of the valley.
            for(float y=floor+6;y<cap-1;y+=7.1f){
                float u=Mathf.InverseLerp(floor,cap,y);
                b.Block(Paint.Basalt,W(0,y,s),new Vector3(Mathf.Lerp(baseWidth,3.65f,u)*2+.10f,.17f,Mathf.Lerp(5,3.04f,u)+.08f));
            }
        }

        static void BridgeSpan(Batch b,float s,float length)
        {
            float mid=s+length*.5f;
            b.Block(Paint.Steel,W(0,-.40f,mid),new Vector3(6.14f,.37f,length));
            ColliderBox("Viaduct deck "+s,W(0,-.38f,mid),new Vector3(6.24f,.45f,length));
            for(int side=-1;side<=1;side+=2){
                float x=side*2.51f;
                b.Block(Paint.Steel,W(x,-.84f,mid),new Vector3(.33f,.76f,length));
                b.Block(Paint.Iron,W(x,-5.68f,mid),new Vector3(.33f,.42f,length));
                float edge=side*3.02f;
                b.Block(Paint.Snow,W(edge,-.165f,mid),new Vector3(.29f,.082f,length));
                b.Block(Paint.Steel,W(edge,.89f,mid),new Vector3(.067f,.072f,length));
                b.Block(Paint.Iron,W(edge,.432f,mid),new Vector3(.039f,.044f,length));
                for(int k=0;k<8;k++){
                    float a=s+length*k/8f,e=s+length*(k+1)/8f;
                    bool rising=k%2==0;
                    b.Bar(Paint.Steel,W(x,rising?-5.53f:-.99f,a),W(x,rising?-.99f:-5.53f,e),.246f,.31f);
                    b.Bar(Paint.Iron,W(x,-5.53f,a),W(x,-.98f,a),.143f,.19f);
                    b.Block(Paint.Steel,W(edge,.366f,a),new Vector3(.070f,1.16f,.081f));
                    b.Block(Paint.Iron,W(x,-1.10f,a),new Vector3(.353f,.57f,.71f));
                    b.Block(Paint.Iron,W(x,-5.47f,a),new Vector3(.355f,.41f,.52f));
                }
            }
            for(int k=0;k<8;k++){
                float a=s+length*k/8f;
                b.Bar(Paint.Iron,W(-2.63f,-.745f,a),W(2.63f,-.745f,a),.24f,.31f);
                if(k%2==0)b.Bar(Paint.Steel,W(-2.5f,-5.6f,a),W(2.5f,-5.6f,a+length/8),.19f,.14f);
            }
        }

        static void Viaduct()
        {
            var b=new Batch();
            for(int i=0;i<18;i++) {
                float s=1400+40*i;BridgeSpan(b,s,40);
                if(i>0)Pier(b,s,-6.32f);
                if(i>=3 && i<15) {
                    for(int side=-1;side<=1;side+=2){
                        float x=side*3.08f;
                        b.Block(Paint.Steel,W(x,3.83f,s+20),new Vector3(.147f,.17f,40));
                        b.Block(Paint.Snow,W(x,3.924f,s+20),new Vector3(.15f,.032f,40));
                        for(int j=0;j<5;j++){
                            float k=s+8*j;
                            b.Bar(Paint.Steel,W(x,.75f,k),W(x,3.76f,k+4),.095f,.133f);
                            b.Bar(Paint.Steel,W(x,3.76f,k+4),W(x,.75f,k+8),.095f,.133f);
                            b.Block(Paint.Iron,W(x,3.72f,k+4),new Vector3(.175f,.231f,.321f));
                        }
                    }
                }
                if(i%3==0){
                    // Refuge decks with green steel rails cantilever out above the gorge.
                    float h=i%2==0?-1:1;
                    b.Block(Paint.Steel,W(h*3.57f,-.22f,s+19),new Vector3(1.20f,.18f,3.35f));
                    b.Bar(Paint.Steel,W(h*2.42f,-2.18f,s+18),W(h*4.06f,-.25f,s+18),.14f,.13f);
                    b.Bar(Paint.Steel,W(h*2.42f,-2.18f,s+20),W(h*4.06f,-.25f,s+20),.14f,.13f);
                    for(float ds=17.5f;ds<21;ds+=1.5f)b.Block(Paint.Steel,W(h*4.14f,.395f,s+ds),new Vector3(.08f,1.12f,.08f));
                    b.Block(Paint.Snow,W(h*4.14f,.973f,s+19),new Vector3(.082f,.051f,3.2f));
                }
            }
            BridgeSpan(b,2150,40);BridgeSpan(b,2190,20);Pier(b,2190,-6.32f);
            Pier(b,2120,-6.30f);Pier(b,2137,-6.30f,true);Pier(b,2150,-6.30f);
            foreach(int sign in new[]{-1,1}){
                // Rails curl up at the fracture and point into air; no deck nor hidden collider crosses it.
                float x=sign*.752f;
                b.Bar(Paint.Rust,W(x,-.08f,2118.1f),W(x+sign*.25f,.31f,2120.7f),.093f,.13f);
                b.Bar(Paint.Rail,W(x+sign*.25f,.31f,2120.7f),W(x+sign*.66f,.91f,2122.7f),.070f,.094f);
                b.Bar(Paint.Rust,W(sign*2.48f,-1.03f,2118.8f),W(sign*2.91f,-3.95f,2124.6f),.32f,.42f);
                b.Bar(Paint.Steel,W(sign*2.49f,-5.61f,2119.1f),W(sign*2.20f,-7.7f,2123.6f),.28f,.34f);
                b.Bar(Paint.Rust,W(sign*2.56f,-.95f,2151.1f),W(sign*2.84f,-4.11f,2145.9f),.35f,.37f);
            }
            b.Save("Greywater viaduct");
            OldAqueduct();
        }

        static void OldAqueduct()
        {
            // The stone water conduit across the lower tributary supplies a scale reference
            // behind the fast-moving steel lattice, with the fell ridge visible above its deck.
            var b=new Batch();
            const float width=3.6f,span=25f,deck=-7.0f,spring=-22.5f;
            float X(float s)=>-124-(s-1650)*.088f+8*Mathf.Sin(s*.008f);
            for(int i=0;i<32;i++){
                float s=1390+i*span;
                var topA=W(X(s),deck,s);var topB=W(X(s+span),deck,s+span);
                b.Bar(Paint.Masonry,topA,topB,width,1.07f);
                b.Bar(Paint.Snow,topA+Vector3.up*.59f,topB+Vector3.up*.59f,width+.10f,.12f);
                float baseY=Winterbourne.Pass.PassAtmosphere.GroundHeight(X(s),s)-1.5f;
                if(baseY<spring) {
                    b.Block(Paint.Masonry,W(X(s),(spring+baseY)/2,s),new Vector3(width,spring-baseY,3.2f));
                    b.Block(Paint.Shale,W(X(s),spring-.42f,s),new Vector3(width+.51f,.49f,3.78f));
                }
                for(int j=0;j<14;j++){
                    float u=j/14f,v=(j+1)/14f;
                    float a=s+span*u,e=s+span*v;
                    float A(float t){
                        float k=(t-.5f)*2.30f;
                        return spring+(Mathf.Abs(k)>=1 ? 0:Mathf.Sqrt(1-k*k))*11.7f;
                    }
                    for(int side=-1;side<=1;side+=2){
                        float xx=X(a)+side*width/2,xe=X(e)+side*width/2;
                        b.Quad(Paint.Masonry,W(xx,deck,a),W(xe,deck,e),W(xe,A(v),e),W(xx,A(u),a),Vector3.right*side);
                        b.Bar(Paint.BrickLight,W(xx,A(u)+.22f,a),W(xe,A(v)+.22f,e),.16f,.58f);
                    }
                    b.Quad(Paint.Shale,W(X(a)-width/2,A(u),a),W(X(a)+width/2,A(u),a),
                        W(X(e)+width/2,A(v),e),W(X(e)-width/2,A(v),e),Vector3.down);
                }
            }
            b.Save("Low Fell aqueduct");
        }

        static void RiverAndRubble()
        {
            var b=new Batch();var rng=new System.Random(469);
            for(int s=1136;s<1588;s+=3){
                float a=Winterbourne.Pass.PassAtmosphere.TributaryX(s),e=Winterbourne.Pass.PassAtmosphere.TributaryX(s+3);
                float y=Winterbourne.Pass.PassAtmosphere.TributaryY(s),yy=Winterbourne.Pass.PassAtmosphere.TributaryY(s+3);
                b.Quad(Paint.Ice,W(a-4.66f,y,s),W(a+4.66f,y,s),W(e+4.66f,yy,s+3),W(e-4.66f,yy,s+3),Vector3.up);
                b.Quad(Paint.Water,W(a-.74f,y+.018f,s),W(a+1.14f,y+.018f,s),W(e+1.14f,yy+.018f,s+3),W(e-.74f,yy+.018f,s+3),Vector3.up);
            }
            for(int s=1420;s<2198;s+=4){
                float x=Winterbourne.Pass.PassAtmosphere.RiverX(s),xx=Winterbourne.Pass.PassAtmosphere.RiverX(s+4);
                b.Quad(Paint.Ice,W(x-11,-81.33f,s),W(x+12,-81.33f,s),W(xx+12,-81.33f,s+4),W(xx-11,-81.33f,s+4),Vector3.up);
                float edge=x+Mathf.Sin(s*.033f)*1.4f;
                b.Quad(Paint.Water,W(edge-1.39f,-81.305f,s),W(edge+2.05f,-81.305f,s),W(xx+Mathf.Sin((s+4)*.033f)*1.4f+2.05f,-81.305f,s+4),
                    W(xx+Mathf.Sin((s+4)*.033f)*1.4f-1.39f,-81.305f,s+4),Vector3.up);
            }
            for(int i=0;i<53;i++){
                float s=Between(rng,2116,2158),x=Between(rng,-15,12);
                float y=Winterbourne.Pass.PassAtmosphere.GroundHeight(x,s)+.62f;
                if(i%3==0)b.Bar(Paint.Rust,W(x,y,s),W(x+Between(rng,-3,4),y+Between(rng,.1f,2.48f),s+Between(rng,2,9)),.42f,.59f);
                else Boulder(b,W(x,y-.61f,s),Between(rng,.57f,2.67f),rng);
            }
            b.Save("River ice and shattered span");
        }

        static void Signals()
        {
            var b=new Batch();
            foreach(float s in new[]{118f,443f,821f,1157f,1387f,2083f}){
                const float x=4.83f;
                b.Block(Paint.Ivory,W(x,2.68f,s),new Vector3(.23f,5.75f,.23f));
                b.Block(Paint.Iron,W(x,.31f,s),new Vector3(.30f,.93f,.29f));
                b.Block(Paint.Iron,W(x,5.43f,s),new Vector3(.63f,.52f,.30f));
                b.Bar(Paint.Signal,W(x,5.51f,s),W(x-1.7f,5.51f,s),.15f,.275f);
                b.Block(Paint.Ivory,W(x-1.39f,5.51f,s),new Vector3(.16f,.28f,.16f));
                b.Taper(Paint.Amber,W(x,5.50f,s-.24f),W(x,5.50f,s+.24f),.11f,.11f,10);
                for(float y=.59f;y<5.16f;y+=.37f)b.Block(Paint.Iron,W(x+.33f,y,s),new Vector3(.297f,.030f,.050f));
                b.Block(Paint.Iron,W(x+.47f,2.64f,s),new Vector3(.031f,4.87f,.046f));
            }
            b.Save("Semaphore signals");
        }
    }
}
