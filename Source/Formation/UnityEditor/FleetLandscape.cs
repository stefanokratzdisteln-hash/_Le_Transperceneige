// Render-only countryside in the demonstration scene. The rolling-stock prefabs
// have no dependency on this mesh, its materials or its lighting.
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Winterbourne.Editor
{
    public static class FleetLandscape
    {
        const string Folder=FleetImport.Base+"/Demo";
        static Material Surface(string name,Color tint,float smoothness,float metallic=0)
        {
            string path=Folder+"/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!material)
            {
                material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material,path);
            }
            material.SetColor("_BaseColor",tint);
            material.SetFloat("_Smoothness",smoothness);
            material.SetFloat("_Metallic",metallic);
            EditorUtility.SetDirty(material);
            return material;
        }

        public static void Build(float reach)
        {
            var vertices=new List<Vector3>();
            var faces=new[]{new List<int>(),new List<int>(),new List<int>()};
            void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,int slot)
            {
                int n=vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                faces[slot].AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            float Height(float x,float z)
            {
                float lateral=Mathf.Abs(x);
                float broad=Mathf.PerlinNoise(x*.040f+53,z*.034f+21);
                float fine=Mathf.PerlinNoise(x*.115f+12,z*.084f+32);
                float rise=Mathf.SmoothStep(0,1,Mathf.InverseLerp(14,65,lateral));
                float ridge=rise*(6+24*Mathf.PerlinNoise(z*.028f+9,x*.032f+41));
                float fringe=Mathf.SmoothStep(0,1,Mathf.InverseLerp(1.2f,6,lateral));
                return -.167f+fringe*(.10f+broad*.55f+fine*.20f)+ridge;
            }
            int segments=Mathf.CeilToInt(reach/3f);
            float[] strips={-118,-89,-72,-62,-53,-44,-35,-27,-19,-12,-8,-5,-3,-1.5f,0,1.5f,3,5,8,12,19,27,35,44,53,62,72,89,118};
            for(int i=0;i<strips.Length-1;i++)
                for(int j=-segments;j<segments;j++)
                {
                    float x=strips[i],xx=strips[i+1],z=j*3,zz=z+3;
                    Quad(new Vector3(x,Height(x,z),z), new Vector3(x,Height(x,zz),zz),
                        new Vector3(xx,Height(xx,zz),zz),new Vector3(xx,Height(xx,z),z),0);
                }
            int terrainVertices=vertices.Count;
            // Two rail heads in the snow; the running surface aligns with wheel datum.
            foreach(float x in new[]{-.752f,.752f})
            {
                const float width=.033f;
                Quad(new Vector3(x-width,.001f,-reach),new Vector3(x-width,.001f,reach),
                     new Vector3(x+width,.001f,reach),new Vector3(x+width,.001f,-reach),1);
                foreach(float side in new[]{-1f,1f})
                {
                    float edge=x+side*width;
                    var a=new Vector3(edge,.001f,-reach);
                    var b=new Vector3(edge,-.14f,-reach);
                    var c=new Vector3(edge,-.14f,reach);
                    var d=new Vector3(edge,.001f,reach);
                    if(side<0) Quad(a,b,c,d,1); else Quad(d,c,b,a,1);
                }
            }
            for(float z=-reach;z<reach;z+=.68f)
                Quad(new Vector3(-1.20f,-.118f,z-.115f),new Vector3(-1.20f,-.118f,z+.115f),
                     new Vector3(1.20f,-.118f,z+.115f),new Vector3(1.20f,-.118f,z-.115f),2);
            string path=Folder+"/WinterLandscape.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!mesh)
            {
                mesh=new Mesh {name="Winter landscape"};
                AssetDatabase.CreateAsset(mesh,path);
            }
            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.subMeshCount=faces.Length;
            for(int i=0;i<faces.Length;i++) mesh.SetTriangles(faces[i],i);
            mesh.RecalculateNormals();
            var normals=mesh.normals;
            for(int i=0;i<terrainVertices;i++)
            {
                var p=vertices[i];
                float dx=Height(p.x+.15f,p.z)-Height(p.x-.15f,p.z);
                float dz=Height(p.x,p.z+.15f)-Height(p.x,p.z-.15f);
                normals[i]=new Vector3(-dx,.30f,-dz).normalized;
            }
            mesh.normals=normals;
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            var scenery=new GameObject("Winter valley and permanent way");
            scenery.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=scenery.AddComponent<MeshRenderer>();
            renderer.sharedMaterials=new[]{Surface("WinterSnow",new Color(.72f,.82f,.90f),.10f),
                Surface("WinterRail",new Color(.24f,.28f,.31f),.55f,.56f),
                Surface("WinterSleepers",new Color(.18f,.18f,.17f),.16f)};
        }
    }
}
