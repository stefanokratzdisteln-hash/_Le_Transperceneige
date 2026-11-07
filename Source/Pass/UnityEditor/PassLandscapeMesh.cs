using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Winterbourne.Editor
{
    public static partial class PassLandscape
    {
        const string Folder="Assets/Transperceneige/Pass/Scenery";
        enum Paint { Terrain, Snow, Basalt, Shale, Mortar, Brick, BrickLight, Steel, Iron, Rust, Rail, Timber,
            Ballast, Needle, Moss, Bark, Ice, Water, Amber, Ivory, Signal, Cable, Spruce, Masonry }
        static readonly Dictionary<Paint,Material> materials=new Dictionary<Paint,Material>();
        static Transform landscape;
        static Vector3 W(float x,float y,float s) => new Vector3(x,y,-s);
        static float Between(System.Random r,float lo,float hi) => Mathf.Lerp(lo,hi,(float)r.NextDouble());

        sealed class Batch
        {
            readonly List<Vector3> vertices=new List<Vector3>();
            readonly List<Vector2> uv=new List<Vector2>();
            readonly List<Paint> slots=new List<Paint>();
            readonly List<List<int>> parts=new List<List<int>>();
            List<int> Slot(Paint p) {
                int i=slots.IndexOf(p);
                if(i<0){slots.Add(p);parts.Add(new List<int>());i=slots.Count-1;}
                return parts[i];
            }
            public void Tri(Paint p,Vector3 a,Vector3 b,Vector3 c,Vector3 outward)
            {
                if(Vector3.Dot(Vector3.Cross(b-a,c-a),outward)<0){var v=c;c=b;b=v;}
                int i=vertices.Count; vertices.Add(a);vertices.Add(b);vertices.Add(c);
                var t=Slot(p);t.Add(i);t.Add(i+1);t.Add(i+2);
            }
            public void Quad(Paint p,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 outward)
            {
                if(Vector3.Dot(Vector3.Cross(b-a,c-a),outward)<0){var v=b;b=d;d=v;}
                int i=vertices.Count; vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
                var t=Slot(p); t.Add(i);t.Add(i+1);t.Add(i+2); t.Add(i);t.Add(i+2);t.Add(i+3);
            }
            public void Block(Paint p,Vector3 centre,Vector3 dims)
            {
                Prism(p,centre,Vector3.right*dims.x*.5f,Vector3.up*dims.y*.5f,Vector3.forward*dims.z*.5f);
            }
            public void FoliageFace(Vector3 root,Vector3 across,float height,int variant)
            {
                while(uv.Count<vertices.Count)uv.Add(Vector2.zero);
                int i=vertices.Count;
                var vertical=Vector3.up*height;
                vertices.Add(root-across);vertices.Add(root+across);
                vertices.Add(root+across+vertical);vertices.Add(root-across+vertical);
                float left=(variant+.002f)/3f,right=(variant+.998f)/3f;
                uv.Add(new Vector2(left,0));uv.Add(new Vector2(right,0));
                uv.Add(new Vector2(right,1));uv.Add(new Vector2(left,1));
                var t=Slot(Paint.Spruce);
                t.Add(i);t.Add(i+1);t.Add(i+2);t.Add(i);t.Add(i+2);t.Add(i+3);
            }
            void Prism(Paint p,Vector3 o,Vector3 a,Vector3 b,Vector3 c)
            {
                Quad(p,o+a-b-c,o+a+b-c,o+a+b+c,o+a-b+c,a);
                Quad(p,o-a-b+c,o-a+b+c,o-a+b-c,o-a-b-c,-a);
                Quad(p,o-a+b-c,o-a+b+c,o+a+b+c,o+a+b-c,b);
                Quad(p,o-a-b+c,o-a-b-c,o+a-b-c,o+a-b+c,-b);
                Quad(p,o-a-b+c,o+a-b+c,o+a+b+c,o-a+b+c,c);
                Quad(p,o+a-b-c,o-a-b-c,o-a+b-c,o+a+b-c,-c);
            }
            public void Bar(Paint p,Vector3 a,Vector3 b,float width,float depth)
            {
                var v=b-a;if(v.sqrMagnitude<.00002f)return;
                var axis=v.normalized;
                var across=Vector3.Cross(axis,Vector3.up);
                if(across.sqrMagnitude<.001f)across=Vector3.right;else across.Normalize();
                var slope=Vector3.Cross(axis,across).normalized;
                Prism(p,(a+b)/2,across*width*.5f,slope*depth*.5f,v*.5f);
            }
            public void Taper(Paint p,Vector3 a,Vector3 b,float rad1,float rad2,int sides)
            {
                var axis=(b-a).normalized;
                var u=Vector3.Cross(axis,Vector3.right);
                if(u.sqrMagnitude<.001f)u=Vector3.forward;else u.Normalize();
                var v=Vector3.Cross(axis,u);
                for(int i=0;i<sides;i++){
                    float t=i*2*Mathf.PI/sides,tt=(i+1)*2*Mathf.PI/sides;
                    var n=u*Mathf.Cos(t)+v*Mathf.Sin(t);
                    var nn=u*Mathf.Cos(tt)+v*Mathf.Sin(tt);
                    Quad(p,a+n*rad1,a+nn*rad1,b+nn*rad2,b+n*rad2,n+nn);
                }
            }
            public GameObject Save(string key,bool collide=false)
            {
                if(vertices.Count==0)return null;
                Mesh m=MeshAt(key);
                m.indexFormat=vertices.Count>65534?IndexFormat.UInt32:IndexFormat.UInt16;
                m.SetVertices(vertices);m.subMeshCount=slots.Count;
                if(uv.Count>0){while(uv.Count<vertices.Count)uv.Add(Vector2.zero);m.SetUVs(0,uv);}
                for(int i=0;i<slots.Count;i++)m.SetTriangles(parts[i],i);
                m.RecalculateNormals();m.RecalculateBounds();
                var mats=new Material[slots.Count];for(int i=0;i<mats.Length;i++)mats[i]=materials[slots[i]];
                return Place(key,m,mats,collide);
            }
        }

        static Mesh MeshAt(string key)
        {
            string path=Folder+"/"+key+".asset";
            var m=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!m){m=new Mesh {name=key};AssetDatabase.CreateAsset(m,path);}else m.Clear();
            return m;
        }
        static GameObject Place(string name,Mesh m,Material[] mats,bool collide)
        {
            EditorUtility.SetDirty(m);
            var o=new GameObject(name);o.transform.SetParent(landscape,false);
            o.AddComponent<MeshFilter>().sharedMesh=m;o.AddComponent<MeshRenderer>().sharedMaterials=mats;
            if(collide)o.AddComponent<MeshCollider>().sharedMesh=m;
            return o;
        }
        static void ColliderBox(string label,Vector3 pos,Vector3 dims)
        {
            var obj=new GameObject(label);obj.transform.SetParent(landscape,false);
            obj.transform.position=pos;obj.AddComponent<BoxCollider>().size=dims;
        }
        static Material Create(string name,string shaderName)
        {
            string path=Folder+"/"+name+".mat";
            Shader shader=Shader.Find(shaderName);
            if(!shader)throw new InvalidOperationException("Route requires "+shaderName);
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(shader);AssetDatabase.CreateAsset(m,path);}else m.shader=shader;
            EditorUtility.SetDirty(m);return m;
        }
        static Color Colour(string hex)
        {
            ColorUtility.TryParseHtmlString("#"+hex,out Color c);return c;
        }
        static void Surface(Paint p,string hex,float smooth=.20f,float metal=0)
        {
            var m=Create(p.ToString(),"Universal Render Pipeline/Lit");
            m.SetColor("_BaseColor",Colour(hex));m.SetFloat("_Smoothness",smooth);m.SetFloat("_Metallic",metal);
            materials[p]=m;
        }
        static void Palette()
        {
            materials.Clear();materials[Paint.Terrain]=Create("Wind scoured schist","Winterbourne/Snow and schist");
            Surface(Paint.Snow,"DDE6EB",.24f);Surface(Paint.Basalt,"54606A");Surface(Paint.Shale,"747D83");
            Surface(Paint.Mortar,"777B7B");Surface(Paint.Brick,"614E47");Surface(Paint.BrickLight,"89786B");
            Surface(Paint.Steel,"435651",.43f,.45f);Surface(Paint.Iron,"313E46",.40f,.58f);
            Surface(Paint.Rust,"83533E",.29f,.30f);Surface(Paint.Rail,"A3A6A4",.65f,.69f);
            Surface(Paint.Timber,"584A40",.19f);Surface(Paint.Ballast,"696B70",.16f);
            Surface(Paint.Needle,"263F3B");Surface(Paint.Moss,"46584B");Surface(Paint.Bark,"4E4944");
            Surface(Paint.Ice,"A6C3CA",.47f);Surface(Paint.Water,"4F767F",.67f);
            Surface(Paint.Amber,"F9C580",.35f);Surface(Paint.Ivory,"D5CCAD");Surface(Paint.Signal,"9F443C");
            Surface(Paint.Cable,"41474B",.21f,.30f);
            foreach(var p in new[]{Paint.Brick,Paint.BrickLight,Paint.Mortar,Paint.Masonry}){
                var stone=Create(p.ToString(),"Winterbourne/Coursed masonry");
                stone.SetColor("_Tint",Colour(p==Paint.Brick?"765F55":p==Paint.BrickLight?"A09582":p==Paint.Mortar?"6B6864":"827968"));
                stone.SetFloat("_Course",p==Paint.Masonry?.55f:.32f);materials[p]=stone;
            }
            Material lit=materials[Paint.Amber];
            lit.EnableKeyword("_EMISSION");lit.SetColor("_EmissionColor",new Color(1.0f,.52f,.17f)*2.7f);
            string path=Folder+"/FellSpruces.png";
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(importer==null)throw new InvalidOperationException("Missing original FellSpruces atlas.");
            if(importer.npotScale!=TextureImporterNPOTScale.None || !importer.mipMapsPreserveCoverage){
                importer.npotScale=TextureImporterNPOTScale.None;importer.alphaIsTransparency=true;
                importer.mipMapsPreserveCoverage=true;importer.alphaTestReferenceValue=.28f;
                importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            }
            var fir=Create("Snowbound spruce","Winterbourne/Snowbound spruce");
            fir.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(path));materials[Paint.Spruce]=fir;
        }
    }
}
