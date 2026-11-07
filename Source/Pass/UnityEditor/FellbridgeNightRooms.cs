using UnityEditor;
using UnityEngine;
using static Winterbourne.Editor.StationFabrication;

namespace Winterbourne.Editor
{
    public static partial class PassFittings
    {
        sealed partial class StationHouse
        {
            Material counterOak, carvedOak, prints, dampTile, wicker, paperStock;
            Material lampReflection, linenBlind, etchedOpal;

            void FitOutMaterials()
            {
                var lime=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/DistemperedPlaster.png");
                plaster.SetTexture("_BaseMap",lime);
                plaster.SetFloat("_Smoothness",.19f);
                EditorUtility.SetDirty(plaster);
                var grain=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/WeatheredWalnut.png");
                counterOak=Paint("Night booking counter oak",new Color(.61f,.48f,.36f),.04f,.45f);
                carvedOak=Paint("Polished counter rails",new Color(.81f,.62f,.42f),.04f,.58f);
                foreach(var wood in new[]{counterOak,carvedOak}) {
                    wood.SetTexture("_BaseMap",grain);
                    EditorUtility.SetDirty(wood);
                }
                dampTile=Paint("Wet station threshold tiles",new Color(.347f,.326f,.255f),.03f,.78f);
                wicker=Paint("Split willow",new Color(.54f,.375f,.203f),0,.22f);
                paperStock=Paint("Buff ticket stock",new Color(.72f,.59f,.399f),0,.17f);
                prints=Graphic("Fellbridge night duty print", "NightDutyPrints");
                prints.SetFloat("_Smoothness",.16f);
                // The warm indirect floor beneath the hood reaches the station
                // stationery even with additional lights capped per renderer.
                foreach(var printed in new[]{prints,signs}) {
                    printed.SetTexture("_EmissionMap",printed.GetTexture("_BaseMap"));
                    printed.SetColor("_EmissionColor",new Color(.12f,.087f,.055f));
                    printed.EnableKeyword("_EMISSION");
                    EditorUtility.SetDirty(printed);
                }
                linenBlind=Paint("Lit buff roller linen",new Color(.74f,.52f,.27f),0,.16f,.43f,true);
                lampReflection=MakeLampReflection();
                const string opalPath=Folder+"/Lit station opal.mat";
                etchedOpal=AssetDatabase.LoadAssetAtPath<Material>(opalPath);
                if(!etchedOpal) {
                    etchedOpal=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    AssetDatabase.CreateAsset(etchedOpal,opalPath);
                }
                etchedOpal.SetColor("_BaseColor",new Color(1,.82f,.58f));
                EditorUtility.SetDirty(etchedOpal);
            }

            // A faint film of condensation scatters the interior light more near the
            // cold sash edges. The marks can be seen from the platform; the middle
            // stays transparent and the office keeps its unobstructed outward view.
            static Material MakeLampReflection()
            {
                const string materialPath=Folder+"/Glass in lamplight.mat";
                const string mapPath=Folder+"/Lamplit sash condensation.asset";
                const int width=64,height=96;
                var map=AssetDatabase.LoadAssetAtPath<Texture2D>(mapPath);
                if(!map) {
                    map=new Texture2D(width,height,TextureFormat.RGBA32,true);
                    map.name="Lamplit sash condensation";
                    map.wrapMode=TextureWrapMode.Clamp;
                    var pixels=new Color32[width*height];
                    for(int y=0;y<height;y++)
                        for(int x=0;x<width;x++) {
                            float u=(x+.5f)/width,v=(y+.5f)/height;
                            float rim=Mathf.Exp(-Mathf.Min(u,1-u)*13f)+.65f*Mathf.Exp(-v*12f);
                            float glow=Mathf.Exp(-2.3f*(1-v))*Mathf.Exp(-5.7f*(u-.67f)*(u-.67f));
                            float moisture=Mathf.PerlinNoise(u*7f+.31f,v*5.6f)*.043f;
                            float density=.075f+.16f*rim+.265f*glow+moisture;
                            pixels[x+y*width]=new Color32(249,176,100,(byte)(255*Mathf.Clamp01(density)));
                        }
                    map.SetPixels32(pixels);map.Apply();
                    AssetDatabase.CreateAsset(map,mapPath);
                }
                var mat=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(!mat) {
                    mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                    AssetDatabase.CreateAsset(mat,materialPath);
                }
                mat.SetTexture("_BaseMap",map);
                mat.SetColor("_BaseColor",Color.white);
                mat.SetFloat("_Surface",1);
                mat.SetFloat("_Blend",0);
                mat.SetFloat("_SrcBlend",(int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend",(int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite",0);
                mat.SetFloat("_Cull",(int)UnityEngine.Rendering.CullMode.Back);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetOverrideTag("RenderType","Transparent");
                mat.renderQueue=(int)UnityEngine.Rendering.RenderQueue.Transparent+1;
                mat.SetShaderPassEnabled("ShadowCaster",false);
                EditorUtility.SetDirty(mat);
                return mat;
            }

            void FitLitSash(Vector3 centre,Vector3 n,Vector3 axis,float sill,float lintel,float width,int bay,int storey)
            {
                var side=axis*(width/2-.045f);
                var bottom=centre+Vector3.up*(sill+.039f)+n*.020f;
                var top=centre+Vector3.up*(lintel-.037f)+n*.020f;
                shell.Quad(lampReflection,bottom-side,bottom+side,top+side,top-side,n);
                // Roller blinds hang a little lower in the clerical rooms. Keep the
                // lower sash clear so the scale, seats and passing coaches are visible.
                if(storey==0 && n.x>0 && bay==1) return;
                float fall=storey==1 ? (bay%2==0 ? .33f:.24f) : .19f;
                var rail=centre+Vector3.up*(lintel-.079f)-n*.107f;
                var edge=rail-Vector3.up*fall;
                interior.Ring(carvedOak,rail-side,rail+side,.034f,.034f,12);
                interior.Quad(linenBlind,rail-side,rail+side,edge+side,edge-side,n);
                interior.Beam(carvedOak,edge-side,edge+side,.014f,.019f);
            }

            void OpalGlobe(StationFabrication mesh,Vector3 top,float radius)
            {
                var middle=top-Vector3.up*radius*.63f;
                var basePoint=top-Vector3.up*radius*1.18f;
                mesh.Ring(etchedOpal,top,middle,radius*.82f,radius,24);
                mesh.Ring(etchedOpal,middle,basePoint,radius,radius*.68f,24);
                mesh.Ring(etchedOpal,basePoint,basePoint-Vector3.up*radius*.25f,radius*.68f,radius*.22f,24);
                mesh.Ring(m.bronze,top,top+Vector3.up*.017f,radius*.89f,radius*.89f,24);
            }

            Transform Model(string name,Vector3 position,float yaw)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Heritage/"+name+".fbx");
                PassBuild.Check(prefab,"Missing Fellbridge station fitting "+name);
                var model=((GameObject)PrefabUtility.InstantiatePrefab(prefab,house)).transform;
                model.localPosition=position;
                model.localRotation=Quaternion.Euler(0,yaw,0);
                return model;
            }

            void Print(int cell,Vector3 centre,Vector3 right,Vector3 up)
            {
                const float margin=.0012f;
                Vector2 lo=new Vector2((cell%4)*.25f+margin,.75f-(cell/4)*.25f+margin);
                Vector2 hi=lo+Vector2.one*(.25f-2*margin);
                interior.AtlasQuad(prints,centre-right-up,centre+right-up,centre+right+up,centre-right+up,
                    Vector3.Cross(up,right),lo,hi);
            }

            void WallPrint(int cell,Vector3 centre,Vector3 right,float height)
            {
                var up=Vector3.up*height;
                var normal=Vector3.Cross(up,right).normalized;
                var frame=centre-normal*.018f;
                interior.Beam(carvedOak,frame-right-up,frame+right-up,.035f,.047f);
                interior.Beam(carvedOak,frame-right+up,frame+right+up,.035f,.047f);
                interior.Beam(carvedOak,frame-right-up,frame-right+up,.035f,.047f);
                interior.Beam(carvedOak,frame+right-up,frame+right+up,.035f,.047f);
                Print(cell,centre,right,up);
            }

            void TaskLight(string name,Vector3 origin,Vector3 target,float cone,float range,float intensity)
            {
                var lamp=Child(house,name,origin).AddComponent<Light>();
                lamp.transform.localRotation=Quaternion.LookRotation(target-origin);
                lamp.type=LightType.Spot;
                lamp.spotAngle=cone; lamp.innerSpotAngle=cone*.55f;
                lamp.color=new Color(1,.824f,.626f);
                lamp.range=range; lamp.intensity=intensity;
                lamp.shadows=LightShadows.None;
            }

            void BookingWindow()
            {
                const float z=6.25f;
                Solid(interior,counterOak,"Booking counter",P(-6.60f,.510f,z),P(.86f,1.02f,4.74f));
                // Build around the physical pass-through. The tray's inset remains below its brass edging.
                var desk=P(-6.59f,1.041f,z);
                interior.Box(carvedOak,desk+P(-.22f,0,0),P(.60f,.060f,4.83f));
                foreach(float hand in new[]{-1f,1f})
                    interior.Box(carvedOak,desk+P(.285f,0,hand*1.36f),P(.42f,.060f,2.11f));
                interior.Box(m.bronze,P(-6.30f,1.027f,z),P(.428f,.013f,.62f));
                for(int h=-1;h<=1;h+=2)
                    interior.Box(m.bronze,P(-6.30f,1.051f,z+h*.31f),P(.43f,.040f,.022f));
                for(int panel=0;panel<5;panel++)
                {
                    float bz=4.36f+panel*.94f;
                    interior.Box(green,P(-6.155f,.54f,bz),P(.047f,.69f,.78f));
                    foreach(int h in new[]{-1,1})
                        interior.Box(carvedOak,P(-6.117f,.54f,bz+h*.36f),P(.035f,.66f,.026f));
                    foreach(float y in new[]{.228f,.852f})
                        interior.Box(carvedOak,P(-6.117f,y,bz),P(.035f,.030f,.74f));
                }
                interior.Box(carvedOak,P(-6.115f,.10f,z),P(.106f,.14f,4.8f));
                var barX=-6.36f;
                interior.Box(carvedOak,P(barX,2.64f,z),P(.18f,.14f,4.77f));
                interior.Box(counterOak,P(barX,2.75f,z),P(.21f,.073f,4.90f));
                interior.Box(carvedOak,P(barX,2.81f,z),P(.235f,.052f,4.96f));
                // Two glazed sidelights with muntins leave a broad central opening at eye level.
                foreach(int hand in new[]{-1,1})
                {
                    float bz=z+hand*1.47f;
                    interior.Box(glass,P(barX,1.86f,bz),P(.015f,1.36f,1.60f));
                    foreach(float dy in new[]{1.19f,2.06f,2.54f})
                        interior.Box(carvedOak,P(barX,dy,bz),P(.090f,.046f,1.68f));
                    for(int mull=-1;mull<=1;mull++)
                        interior.Box(carvedOak,P(barX,1.86f,bz+mull*.53f),P(.072f,1.36f,.033f));
                    foreach(float edge in new[]{.61f,2.31f})
                    {
                        float iz=z+hand*edge;
                        interior.Box(counterOak,P(barX,1.84f,iz),P(.14f,1.53f,.095f));
                        interior.Box(carvedOak,P(barX,1.23f,iz),P(.17f,.27f,.12f));
                        interior.Box(carvedOak,P(barX,2.44f,iz),P(.17f,.11f,.14f));
                    }
                    Box(house,"Glazed ticket screen",P(barX,1.88f,bz),P(.11f,1.52f,1.70f));
                }
                for(int bar=0;bar<12;bar++)
                {
                    float t=bar/11f;
                    float rise=Mathf.Sin(t*Mathf.PI)*.18f;
                    var a=P(barX,2.19f+rise,z-.60f+1.2f*t);
                    interior.Ring(m.bronze,a,P(barX,2.57f,a.z),.010f,.010f,8);
                    if(bar<11) interior.Beam(carvedOak,a,P(barX,2.19f+Mathf.Sin((bar+1)/11f*Mathf.PI)*.18f,
                        z-.60f+1.2f*(bar+1)/11),.055f,.045f);
                }
                Model("TicketDatingPress",P(-6.52f,1.072f,z+.36f),-76);
                // A 57 x 30 mm Edmondson ticket lies ready to slide out of the window.
                Print(0,P(-6.207f,1.0355f,z-.045f),Vector3.forward*.0285f,Vector3.left*.015f);
                var ledger=P(-6.55f,1.078f,z-.37f);
                interior.Box(m.oxblood,ledger,P(.224f,.013f,.305f));
                interior.Box(paperStock,ledger+Vector3.up*.011f,P(.212f,.014f,.294f));
                Print(1,ledger+Vector3.up*.020f,Vector3.forward*.141f,Vector3.left*.103f);
                TaskLight("Booking clerk's counter light",P(-6.15f,2.51f,6.27f),P(-6.57f,1.07f,6.33f),76,3.0f,2.55f);
                var down=P(-6.05f,2.44f,6.25f);
                interior.Box(m.bronze,down,P(.14f,.055f,.36f));
                interior.Box(m.lamp,down+Vector3.down*.032f,P(.103f,.012f,.275f));
            }

            void TicketRack()
            {
                const float x=-8.60f,z=6.29f;
                ReadingShade("Ticket cabinet lamp",P(-7.38f,2.73f,z),P(x,1.46f,z),5.3f);
                interior.Box(counterOak,P(x-.10f,1.49f,z),P(.08f,1.58f,3.34f));
                for(int col=0;col<=8;col++)
                    interior.Box(carvedOak,P(x,1.51f,z-1.62f+col*.405f),P(.23f,1.47f,.027f));
                for(int row=0;row<=4;row++)
                    interior.Box(carvedOak,P(x,.79f+row*.356f,z),P(.23f,.034f,3.32f));
                for(int col=0;col<8;col++)
                {
                    float at=z-1.418f+col*.405f;
                    for(int row=0;row<4;row++)
                    {
                        float h=.82f+row*.356f, stack=.09f+.023f*((row*3+col)%6);
                        var stock=row==2?m.khaki:paperStock;
                        for(int i=0;i<3;i++)
                        {
                            var card=P(x+.037f,h+stack/2,at+(i-1)*.093f);
                            interior.Box(stock,card,P(.115f,stack,.077f));
                            for(int line=1;line<=4;line++)interior.Box(m.khaki,card+P(.058f,(line-2.5f)*stack/5,0),P(.003f,.005f,.077f));
                        }
                    }
                    Print(2+col%4,P(x+.123f,2.272f,at),Vector3.forward*.197f,Vector3.up*.046f);
                }
            }

            void BenchAt(float x,float y,float z)
            {
                // Slatted oak, curved arms, and flared cast-iron stanchions against the dado.
                for(int side=-1;side<=1;side+=2)
                {
                    float sz=z+side*.77f;
                    foreach(float dx in new[]{-.19f,.17f})
                    {
                        var foot=P(x+dx,y+.048f,sz);
                        interior.Box(m.soot,foot,P(.125f,.055f,.13f));
                        interior.Ring(green,foot,P(x+dx*.76f,y+.444f,sz),.051f,.035f,10);
                    }
                    interior.Beam(green,P(x-.18f,y+.251f,sz),P(x+.16f,y+.429f,sz),.039f,.051f);
                    interior.Beam(green,P(x+.18f,y+.407f,sz),P(x+.29f,y+.966f,sz),.066f,.051f);
                    interior.Ring(m.soot,P(x-.18f,y+.45f,sz),P(x-.18f,y+.633f,sz),.027f,.024f);
                    interior.Beam(carvedOak,P(x-.22f,y+.652f,sz),P(x+.22f,y+.697f,sz),.069f,.057f);
                }
                for(int slat=0;slat<4;slat++)
                    interior.Box(counterOak,P(x-.209f+slat*.126f,y+.47f,z),P(.114f,.055f,1.88f));
                for(int slat=0;slat<3;slat++)
                    interior.Box(counterOak,P(x+.205f+slat*.032f,y+.585f+slat*.13f,z),P(.052f,.110f,1.88f),Quaternion.Euler(0,0,-12));
                interior.Box(carvedOak,P(x+.28f,y+.932f,z),P(.077f,.055f,1.92f));
                Box(house,"Waiting bench",P(x,y+.447f,z),P(.55f,.09f,1.90f));
                Box(house,"Bench back",P(x+.24f,y+.76f,z),P(.08f,.50f,1.90f));
            }

            void WaitingHearth()
            {
                // Register the iron surround against the transverse wall, leaving both door routes clear.
                var grate=Model("WaitingRoomGrate",P(-4.63f,.017f,-3.78f),180);
                Box(house,"Waiting hearth",P(-4.63f,.42f,-3.80f),P(1.30f,.85f,.57f));
                var glow=Child(grate,"Banked coals",P(0,.29f,-.20f)).AddComponent<Light>();
                glow.type=LightType.Point;glow.color=new Color(1,.37f,.125f);glow.intensity=.36f;glow.range=1.55f;
                WallPrint(11,P(-3.12f,1.97f,-4.036f),Vector3.left*.32f,.41f);
            }

            void ParcelWorkroom()
            {
                Vector3 origin=P(-2.87f,.012f,-8.74f);
                Model("ParcelsScale",origin,165);
                Box(house,"Parcels weighing platform",origin+P(0,.11f,0),P(.97f,.23f,1.04f));
                Box(house,"Scale weigh column",origin+P(.12f,.54f,-.32f),P(.21f,1.03f,.20f));
                var bench=P(-5.18f,.885f,-9.01f);
                Solid(interior,counterOak,"Parcels clerk's bench",bench,P(2.30f,.09f,.79f));
                foreach(float dx in new[]{-1.02f,1.02f})
                    foreach(float dz in new[]{-.30f,.30f})
                        interior.Box(carvedOak,P(bench.x+dx,.435f,bench.z+dz),P(.10f,.85f,.10f));
                var p=bench+P(.07f,.055f,.05f);
                interior.Box(m.khaki,p,P(.34f,.023f,.245f));
                Print(7,p+Vector3.up*.013f,Vector3.left*.164f,Vector3.back*.116f);
                for(int hand=-1;hand<=1;hand+=2)
                {
                    var basket=bench+P(hand*.79f,.08f,0);
                    interior.Box(wicker,basket,P(.43f,.04f,.32f));
                    for(int i=0;i<=4;i++)
                    {
                        var ring=basket+Vector3.up*(.02f+i*.023f);
                        foreach(int s in new[]{-1,1})
                        {
                            interior.Box(wicker,ring+Vector3.right*(s*(.210f+i*.004f)),P(.012f,.014f,.32f+i*.005f));
                            interior.Box(wicker,ring+Vector3.forward*(s*(.161f+i*.0025f)),P(.43f+i*.008f,.014f,.012f));
                        }
                    }
                    for(int s=-1;s<=1;s+=2)
                        for(int j=-4;j<=4;j++)interior.Beam(wicker,basket+P(j*.047f,.014f,s*.157f),
                            basket+P(j*.052f,.136f,s*.177f),.011f,.016f);
                    Print(hand<0?14:15,basket+P(0,.078f,.179f),Vector3.left*.082f,Vector3.up*.022f);
                }
                for(int i=0;i<4;i++)
                    for(int row=0;row<3;row++)
                    {
                        float x=-5.94f+i*.38f,y=1.34f+row*.22f;
                        interior.Box(counterOak,P(x,y,-9.60f),P(.363f,.027f,.30f));
                        interior.Box(counterOak,P(x-.178f,y+.091f,-9.60f),P(.024f,.20f,.29f));
                        if((row+i)%3!=0)interior.Box(paperStock,P(x,y+.065f,-9.59f),P(.23f,.092f,.19f));
                    }
                WallPrint(13,P(-6.33f,1.96f,-9.83f),Vector3.left*.286f,.38f);
                // Shipping ties cross both the top and label face of each packed case.
                for(int tier=0;tier<2;tier++)
                    for(int k=0;k<3;k++)
                    {
                        float h=.24f+tier*.92f,z=-8.26f+k*.96f+.10f*tier;
                        Print(6,P(-7.339f,h+.30f,z+.018f),Vector3.forward*.082f,Vector3.up*.045f);
                        interior.Beam(wicker,P(-7.338f,h+.33f,z+.093f),P(-7.337f,h+.465f,z+.195f),.008f,.008f);
                    }
                var twine=bench+P(-.30f,.075f,.07f);
                for(int winding=0;winding<10;winding++)
                    interior.Ring(wicker,twine+Vector3.up*winding*.009f,twine+Vector3.up*(winding*.009f+.008f),.047f,.047f,14);
                interior.Beam(wicker,twine+P(.028f,.051f,.032f),twine+P(.071f,0,.108f),.007f,.007f);
                // Gas-pattern enamelled task lamp with a shallow shade over the scales.
                interior.Beam(m.soot,P(-2.53f,2.40f,-9.82f),P(-2.53f,2.57f,-9.06f),.041f,.041f);
                interior.Ring(green,P(-2.53f,2.41f,-9.06f),P(-2.53f,2.56f,-9.06f),.17f,.055f);
                interior.Ring(m.lamp,P(-2.53f,2.401f,-9.06f),P(-2.53f,2.409f,-9.06f),.135f,.15f);
                TaskLight("Parcels scale lamp",P(-2.53f,2.38f,-9.06f),origin+Vector3.up*.25f,84,4.0f,2.45f);
                ReadingShade("Parcel bench and cases lamp",P(-5.72f,2.61f,-7.64f),P(-6.44f,1.05f,-8.34f),5.9f);
            }

            void ReadingShade(string name,Vector3 centre,Vector3 target,float intensity)
            {
                // A real pendant and opal bulb make the shelves legible from the
                // passenger side. Falloff is confined to the small counter room.
                interior.Ring(m.bronze,centre, new Vector3(centre.x,Upper-.26f,centre.z),.015f,.017f,10);
                interior.Ring(green,centre-Vector3.up*.10f,centre,.156f,.049f,24);
                OpalGlobe(interior,centre-Vector3.up*.103f,.09f);
                TaskLight(name,centre-Vector3.up*.22f,target,121f,4.7f,intensity);
            }

            void DutyRoom()
            {
                var p=P(-2.12f,Upper,-6.16f);
                var desk=Model("StationmasterDesk",p,90);
                Box(house,"Dispatch desk top",p+P(0,.77f,0),P(.81f,.087f,1.59f));
                Box(house,"Stationmaster chair",p+P(-.76f,.48f,0),P(.47f,.91f,.51f));
                var lamp=desk.localPosition+desk.localRotation*P(-.583f,1.075f,.096f);
                TaskLight("Stationmaster's desk lamp",lamp,p+P(.02f,.81f,.18f),96,2.7f,.94f);
                WallPrint(9,P(-4.61f,Upper+1.78f,-9.844f),Vector3.left*.43f,.53f);
                WallPrint(10,P(-3.52f,Upper+1.77f,-9.844f),Vector3.left*.23f,.30f);
            }

            void ClockNumerals(Vector3 centre)
            {
                // The dial fits its square glazed oak case, with the hands modelled in relief.
                interior.Box(counterOak,centre+Vector3.left*.17f,P(.14f,.75f,.75f));
                Print(12,centre,Vector3.forward*.335f,Vector3.up*.335f);
            }
        }
    }
}
