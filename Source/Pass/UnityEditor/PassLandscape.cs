using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Winterbourne.Pass;

namespace Winterbourne.Editor
{
    public static partial class PassLandscape
    {
        static readonly float[] AlongSlopes={-1080,-980,-880,-795,-720,-660,-598,-552,-510,-472,-440,-410,-382,-355,-326,
            -296,-268,-241,-218,-196,-176,-159,-145,-131,-116,-104,-92,-81,-70,-60,-51,-43,-35,-28,-22,-17,-12,-8,-5,-3,
            -1.75f,0,1.75f,3,5,8,12,17,22,28,35,43,51,60,70,81,92,104,116,131,145,159,176,196,218,241,268,296,326,
            355,382,410,440,472,510,552,598,660,720,795,880,980,1080};

        public static void Build()
        {
            var old=GameObject.Find("Pass landscape");if(old)Object.DestroyImmediate(old);
            if(!AssetDatabase.IsValidFolder(Folder))AssetDatabase.CreateFolder("Assets/Transperceneige/Pass","Scenery");
            landscape=new GameObject("Pass landscape").transform;
            Palette();
            for(int at=-240;at<2360;at+=100)
            {
                LandscapeTile(at,at+100);
                var permanent=new Batch();
                Track(permanent,at,at+100);
                permanent.Save("Way "+(at+240).ToString("D4"));
                var plants=new Batch();PlantTile(plants,at);plants.Save("Fells "+(at+240).ToString("D4"));
            }
            Bore();Viaduct();RiverAndRubble();Signals();
            var atmo=landscape.gameObject.AddComponent<PassAtmosphere>();
            atmo.sky=Create("North Fell sky","Winterbourne/Pass sky");
            var clouds=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Drifting cloud density.png");
            PassBuild.Check(clouds && clouds.isReadable,"Moonlight needs the readable cloud cover used by the sky");
            atmo.sky.SetTexture("_CloudDensity",clouds);
            EditorUtility.SetDirty(atmo.sky);
            atmo.passengerEye=Camera.main;
            atmo.sun=RenderSettings.sun;
            if(!atmo.sun){
                foreach(var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if(light.type==LightType.Directional){atmo.sun=light;break;}
            }
            RenderSettings.skybox=atmo.sky;
            atmo.flakeMaterial=Snowflake();
            if(atmo.passengerEye) {
                atmo.passengerEye.clearFlags=CameraClearFlags.Skybox;atmo.passengerEye.farClipPlane=1650;
            }
            atmo.SampleWeather(0,0);
            AssetDatabase.SaveAssets();
        }

        static Material Snowflake()
        {
            // Soft, irregular flecks; drawn as camera-facing particles outside each flank.
            var mat=Create("Spindrift","Winterbourne/Windborne powder");
            return mat;
        }

        static void LandscapeTile(float start,float end)
        {
            const int steps=20;
            int cols=AlongSlopes.Length;
            var v=new List<Vector3>((steps+1)*cols);var n=new List<Vector3>();var tris=new List<int>();
            for(int j=0;j<=steps;j++) {
                float s=Mathf.Lerp(start,end,j/(float)steps);
                for(int i=0;i<cols;i++) {
                    float x=AlongSlopes[i],h=PassAtmosphere.GroundHeight(x,s);
                    v.Add(W(x,h,s));
                    float delta=Mathf.Lerp(.35f,1.8f,Mathf.InverseLerp(4,180,Mathf.Abs(x)));
                    float dx=PassAtmosphere.GroundHeight(x+delta,s)-PassAtmosphere.GroundHeight(x-delta,s);
                    float ds=PassAtmosphere.GroundHeight(x,s+delta)-PassAtmosphere.GroundHeight(x,s-delta);
                    n.Add(new Vector3(-dx,2*delta,ds).normalized);
                    if(i>0&&j>0) {
                        int k=j*cols+i;
                        tris.Add(k-cols-1);tris.Add(k-cols);tris.Add(k);
                        tris.Add(k-cols-1);tris.Add(k);tris.Add(k-1);
                    }
                }
            }
            string name="Strath "+((int)start+240).ToString("D4");
            var m=MeshAt(name);m.SetVertices(v);m.SetNormals(n);m.SetTriangles(tris,0);m.RecalculateBounds();
            Place(name,m,new[]{materials[Paint.Terrain]},true);
        }

        static void Track(Batch b,int start,int end)
        {
            var up=Vector3.up;
            for(int s=start;s<end;s+=2){
                if(s>=2120 && s<2150)continue;
                if(s<1400 || s>=2210) {
                    // The flared shoulders vanish at the abutment, leaving actual air under the span.
                    b.Quad(Paint.Ballast,W(-1.42f,-.162f,s),W(1.42f,-.162f,s),W(1.42f,-.162f,s+2),W(-1.42f,-.162f,s+2),up);
                    foreach(int h in new[]{-1,1})
                        b.Quad(Paint.Ballast,W(h*1.42f,-.162f,s),W(h*1.89f,-.31f,s),
                          W(h*1.89f,-.31f,s+2),W(h*1.42f,-.162f,s+2),up);
                }
            }
            for(int i=Mathf.CeilToInt(start/.67f);i*.67f<end;i++) {
                float s=i*.67f;
                if(s>2119.87f&&s<2150.13f)continue;
                b.Block(Paint.Timber,W(0,-.155f,s),new Vector3(2.55f,.12f,.224f));
            }
            // Continuous rails split at the exact position of the break.
            foreach(float x in new[]{-.752f,.752f})
            {
                RailPiece(b,x,start,Mathf.Min(end,2120));
                RailPiece(b,x,Mathf.Max(2150,start),end);
            }
        }

        static void RailPiece(Batch b,float x,float a,float e)
        {
            if(e<=a)return;
            b.Block(Paint.Rail,W(x,-.021f,(a+e)/2),new Vector3(.071f,.042f,e-a));
            b.Block(Paint.Iron,W(x,-.070f,(a+e)/2),new Vector3(.025f,.055f,e-a));
        }

        static void PlantTile(Batch b,int start)
        {
            var rng=new System.Random(start+7919);
            for(int i=0;i<105;i++)
            {
                int side=(i%2==0?-1:1);
                float s=start+Between(rng,0,100);
                float distance=i<13 ? Between(rng,8.8f,31) : Between(rng,27,272);
                if(s>888 && s<1135 && distance<23) continue;
                if(s>1370 && s<2225 && distance<55)continue;
                if(s>473 && s<786 && distance<11)continue;
                float x=side*distance;
                float h=PassAtmosphere.GroundHeight(x,s);
                float size=Between(rng,3.2f,10.8f)*(distance<85?1:1.27f);
                if(i%9==0 && distance<60) Larch(b,W(x,h-.1f,s),size,rng);
                else Spruce(b,W(x,h-.23f,s),size,rng,distance<48);
                if(distance>24 && i%3!=0) {
                    int stand=rng.Next(2,7);
                    for(int k=0;k<stand;k++){
                        float xx=x+Between(rng,-8,8),ss=s+Between(rng,-8,8);
                        if(InStationHousePlot(xx,ss))continue;
                        float y=PassAtmosphere.GroundHeight(xx,ss);
                        Spruce(b,W(xx,y-.27f,ss),size*Between(rng,.49f,1.18f),rng,false);
                    }
                }
            }
            for(int i=0;i<17;i++){
                float s=start+Between(rng,0,100);
                float x=(i%2==0?-1:1)*Between(rng,9.5f,i<5?30:115);
                if((s>905&&s<1126&&Mathf.Abs(x)<18)||InStationHousePlot(x,s)||(s>1395&&s<2220&&Mathf.Abs(x)<42))continue;
                Boulder(b,W(x,PassAtmosphere.GroundHeight(x,s)-.5f,s),Between(rng,.63f,2.95f),rng);
            }
            for(int k=Mathf.CeilToInt(start/34f);k*34<start+100;k++)
            {
                float s=k*34;
                if(s>465 && s<790 || s>1384 && s<2220)continue;
                float x=(s>859&&s<1160)?8.5f:-5.4f;
                float y=PassAtmosphere.GroundHeight(x,s);
                b.Taper(Paint.Timber,W(x,y,s),W(x+.09f,y+5.75f,s),.109f,.075f,7);
                b.Block(Paint.Timber,W(x,y+5.21f,s),new Vector3(2.07f,.123f,.13f));
                for(int h=-1;h<=1;h+=2) {
                    b.Taper(Paint.Ivory,W(x+.78f*h,y+5.24f,s),W(x+.78f*h,y+5.48f,s),.07f,.052f,7);
                    float ns=s+34,nx=(ns>859&&ns<1160)?8.5f:-5.4f;
                    if(ns>465&&ns<790||ns>1384&&ns<2220||Mathf.Abs(nx-x)>1)continue;
                    float ny=PassAtmosphere.GroundHeight(nx,ns)+5.47f;
                    Vector3 a=W(x+.78f*h,y+5.47f,s),c=W(nx+.78f*h,ny,ns),mid=(a+c)/2+Vector3.down*.54f;
                    b.Bar(Paint.Cable,a,mid,.018f,.018f);b.Bar(Paint.Cable,mid,c,.018f,.018f);
                }
            }
            if(start<450 && start+100>65) {
                for(int i=Mathf.CeilToInt(Mathf.Max(65,start)/2.6f);i*2.6f<Mathf.Min(450,start+100);i++) {
                    float s=i*2.6f;if(i%37>=19)continue;
                    float x=7+.48f*Mathf.Sin(s*.026f);float h=PassAtmosphere.GroundHeight(x,s);
                    b.Block(Paint.Timber,W(x,h+.68f,s),new Vector3(.13f,1.45f,.14f));
                    for(int j=1;j<4;j++)
                        b.Bar(Paint.Timber,W(x,h+j*.32f,s),W(x,h+j*.32f,s+2.6f),.10f,.10f);
                }
            }
        }

        static bool InStationHousePlot(float x,float s) => x < -7 && x > -23 && s > 991 && s < 1019;

        static void Boulder(Batch b,Vector3 o,float r,System.Random rng)
        {
            const int sides=7;
            float offset=Between(rng,0,6),height=r*Between(rng,.47f,1.03f);
            var mid=new Vector3[sides];var top=new Vector3[sides];var bottom=new Vector3[sides];
            for(int i=0;i<sides;i++){
                float a=offset+i*2*Mathf.PI/sides;var d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)*.82f);
                bottom[i]=o+d*r*.88f-Vector3.up*r*.63f;
                mid[i]=o+d*r*Between(rng,.83f,1.12f)+Vector3.up*height*.42f;
                top[i]=o+d*r*Between(rng,.42f,.73f)+Vector3.up*height*Between(rng,.80f,1f);
            }
            for(int i=0;i<sides;i++){
                int j=(i+1)%sides;var outward=(mid[i]+mid[j])*.5f-o;
                b.Quad(Paint.Basalt,bottom[i],bottom[j],mid[j],mid[i],outward);
                b.Quad(i%3==0?Paint.Shale:Paint.Basalt,mid[i],mid[j],top[j],top[i],outward);
                b.Tri(Paint.Snow,top[i],top[j],o+Vector3.up*(height*1.04f),Vector3.up);
            }
        }

        static void Spruce(Batch b,Vector3 o,float height,System.Random r,bool detailed)
        {
            int variant=r.Next(3);
            float w=height*Between(r,.22f,.32f);
            float angle=Between(r,-.35f,.35f);
            var a=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle))*w;
            var c=new Vector3(-a.z,0,a.x);
            var root=o-Vector3.up*(height*.035f);
            b.FoliageFace(root,a,height,variant);
            b.FoliageFace(root,c*.94f,height,(variant+1)%3);
            if(detailed) {
                b.Taper(Paint.Bark,o,o+Vector3.up*height*.78f,height*.013f,.016f,6);
                // Lower bare branches project outside the dense, snow-laden crown.
                for(int i=0;i<5;i++){
                    float yaw=Between(r,0,Mathf.PI*2);
                    var q=o+Vector3.up*height*(.12f+.045f*i);
                    var v=new Vector3(Mathf.Cos(yaw),-.15f,Mathf.Sin(yaw));
                    b.Taper(Paint.Bark,q,q+v*w*.69f,.035f,.004f,4);
                }
            }
        }

        static void Larch(Batch b,Vector3 o,float height,System.Random r)
        {
            b.Taper(Paint.Bark,o,o+Vector3.up*height,height*.024f,.025f,7);
            for(int i=0;i<18;i++){
                float t=.24f+i*.038f,a=Between(r,0,6.28f);
                var dir=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                var root=o+Vector3.up*(height*t);
                var joint=root+dir*(height*(1-t)*.33f)+Vector3.up*height*.08f;
                var tip=joint+dir*height*.13f+Vector3.up*height*.15f;
                b.Taper(Paint.Bark,root,joint,height*.010f,.030f,4);
                b.Taper(Paint.Shale,joint,tip,.031f,.008f,4);
                var fork=joint+Quaternion.Euler(0,46,0)*dir*height*.14f+Vector3.up*.8f;
                b.Taper(Paint.Bark,joint,fork,.024f,.006f,4);
            }
        }
    }
}
