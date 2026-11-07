using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Winterbourne.Editor
{
    // Each replaceable station module is one mesh with shared stock finishes.
    internal sealed class StationFabrication
    {
        public const string Folder = "Assets/Transperceneige/Pass/Station";
        readonly List<Vector3> vertices = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<Material> finishes = new List<Material>();
        readonly List<List<int>> faces = new List<List<int>>();

        public static Material Paint(string name, Color color, float metallic=0, float smooth=.34f,
                                     float emission=0, bool sided=false)
        {
            string path=$"{Folder}/{name}.mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat,path);
            }
            mat.SetColor("_BaseColor",color);
            mat.SetFloat("_Metallic",metallic);
            mat.SetFloat("_Smoothness",smooth);
            mat.SetFloat("_Cull",sided ? 0 : 2);
            if (emission>0) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor",color*emission); }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static Mesh Save(Mesh pending,string name)
        {
            string path=$"{Folder}/{name}.asset";
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            pending.name=name;
            if (!existing) { AssetDatabase.CreateAsset(pending,path); return pending; }
            EditorUtility.CopySerialized(pending,existing);
            UnityEngine.Object.DestroyImmediate(pending);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        public static GameObject Child(Transform parent,string name,Vector3 local)
        {
            var go=new GameObject(name);
            go.transform.SetParent(parent,false);
            go.transform.localPosition=local;
            return go;
        }

        List<int> Slot(Material material)
        {
            int i = finishes.IndexOf(material);
            if (i>=0) return faces[i];
            finishes.Add(material);
            var submesh=new List<int>();
            faces.Add(submesh);
            return submesh;
        }

        void Face(Material mat,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 normal,Vector2 low,Vector2 high)
        {
            bool reverse=Vector3.Dot(Vector3.Cross(b-a,c-a),normal)<0;
            int i=vertices.Count;
            vertices.Add(a);vertices.Add(b);vertices.Add(c);vertices.Add(d);
            uv.Add(low);uv.Add(new Vector2(high.x,low.y));uv.Add(high);uv.Add(new Vector2(low.x,high.y));
            var slot=Slot(mat);
            if(reverse) {slot.Add(i);slot.Add(i+2);slot.Add(i+1);slot.Add(i);slot.Add(i+3);slot.Add(i+2);}
            else {slot.Add(i);slot.Add(i+1);slot.Add(i+2);slot.Add(i);slot.Add(i+2);slot.Add(i+3);}
        }

        public void Quad(Material mat,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 normal)
            => Face(mat,a,b,c,d,normal,Vector2.zero,Vector2.one);

        public void AtlasQuad(Material mat,Vector3 a,Vector3 b,Vector3 c,Vector3 d,Vector3 normal,
                              Vector2 lowerLeft,Vector2 upperRight)
            => Face(mat,a,b,c,d,normal,lowerLeft,upperRight);

        public void Sign(Material mat,Vector3 centre,float width,float height,int row,bool faceTrain=true)
        {
            var right=Vector3.forward*(faceTrain ? 1 : -1)*width/2;
            var h=Vector3.up*height/2;
            Face(mat,centre-right-h,centre+right-h,centre+right+h,centre-right+h,
                 faceTrain?Vector3.right:Vector3.left,new Vector2(.002f,1-(row+1)/8f+.002f),
                 new Vector2(.998f,1-row/8f-.002f));
        }

        public void Box(Material mat,Vector3 p,Vector3 size,Quaternion? pose=null)
        {
            Quaternion rotation=pose??Quaternion.identity;
            Vector3 i=rotation*Vector3.right*size.x/2, j=rotation*Vector3.up*size.y/2,
                    k=rotation*Vector3.forward*size.z/2;
            void Cap(Vector3 axis,Vector3 u,Vector3 v)
            {
                var q=p+axis;
                Quad(mat,q-u-v,q+u-v,q+u+v,q-u+v,axis);
            }
            Cap(i,j,k); Cap(-i,j,k); Cap(j,i,k); Cap(-j,i,k); Cap(k,i,j); Cap(-k,i,j);
        }

        public void Beam(Material mat,Vector3 a,Vector3 b,float width,float depth)
        {
            var vec=b-a;
            Box(mat,(a+b)/2,new Vector3(width,depth,vec.magnitude),
                Quaternion.LookRotation(vec,Mathf.Abs(Vector3.Dot(vec.normalized,Vector3.up))>.98f
                    ? Vector3.forward : Vector3.up));
        }

        public void Ring(Material mat,Vector3 a,Vector3 b,float ra,float rb,int sides=12)
        {
            var dir=(b-a).normalized;
            Vector3 u=Vector3.Cross(dir,Mathf.Abs(dir.y)<.9f?Vector3.up:Vector3.forward).normalized;
            Vector3 v=Vector3.Cross(dir,u);
            for(int i=0;i<sides;i++)
            {
                float angle=Mathf.PI*2*i/sides, next=Mathf.PI*2*(i+1)/sides;
                var n=u*Mathf.Cos(angle)+v*Mathf.Sin(angle);
                var m=u*Mathf.Cos(next)+v*Mathf.Sin(next);
                Quad(mat,a+n*ra,a+m*ra,b+m*rb,b+n*rb,n+m);
                Triangle(mat,a,a+m*ra,a+n*ra,-dir);
                Triangle(mat,b,b+n*rb,b+m*rb,dir);
            }
        }

        public void Triangle(Material mat,Vector3 a,Vector3 b,Vector3 c,Vector3 outward)
        {
            int start=vertices.Count;
            vertices.Add(a);vertices.Add(b);vertices.Add(c);
            uv.Add(Vector2.zero);uv.Add(Vector2.right);uv.Add(Vector2.up);
            bool reverse=Vector3.Dot(Vector3.Cross(b-a,c-a),outward)<0;
            var ids=Slot(mat);ids.Add(start);ids.Add(start+(reverse?2:1));ids.Add(start+(reverse?1:2));
        }

        public GameObject Finish(Transform parent,string name)
        {
            var mesh=new Mesh();
            if(vertices.Count>65534)mesh.indexFormat=IndexFormat.UInt32;
            mesh.SetVertices(vertices); mesh.SetUVs(0,uv);
            mesh.subMeshCount=finishes.Count;
            for(int i=0;i<faces.Count;i++) mesh.SetTriangles(faces[i],i);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var go=Child(parent,name,Vector3.zero);
            go.AddComponent<MeshFilter>().sharedMesh=Save(mesh,name);
            go.AddComponent<MeshRenderer>().sharedMaterials=finishes.ToArray();
            return go;
        }
    }
}
