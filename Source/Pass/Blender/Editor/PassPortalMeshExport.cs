using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Winterbourne.Pass;

namespace Winterbourne.Editor
{
    // Copy into an Assets/Editor folder and run with -executeMethod
    // Winterbourne.Editor.PassPortalMeshExport.Run -portalMesh <temporary.json.gz>.
    public static class PassPortalMeshExport
    {
        [Serializable] class Surface {
            public string name, colourMap, normalMap, shader;
            public Color colour, glow;
            public Vector2 tiling, offset;
            public float metal, smooth, bump;
        }
        [Serializable] class Part { public int material; public int[] triangles; }
        [Serializable] class Geometry { public Vector3[] positions; public Vector2[] uv; public Part[] parts; }
        [Serializable] class Element {
            public string name, lettering;
            public int parent;
            public Vector3 position, scale;
            public Quaternion rotation;
            public Geometry mesh;
            public Color textColour;
            public float textSize;
        }
        [Serializable] class Model {
            public List<Surface> materials = new List<Surface>();
            public List<Element> nodes = new List<Element>();
            public int leaf;
            public Vector3 closedOffset;
            public float duration;
        }

        public static void Run()
        {
            var args=Environment.GetCommandLineArgs();
            int flag=Array.IndexOf(args,"-portalMesh");
            if(flag<0 || flag+1>=args.Length) throw new ArgumentException("-portalMesh <temporary .json.gz> required");
            var gate=PrefabUtility.LoadPrefabContents("Assets/Transperceneige/Pass/Waygate.prefab");
            try {
                var model=new Model();
                var materials=new Dictionary<Material,int>();
                var nodes=new Dictionary<Transform,int>();
                var cover=gate.GetComponent<PassBookcaseCover>();
                if(!cover || !cover.leaf) throw new InvalidOperationException("Authored sliding cover is missing");
                int Index(Material m) {
                    if(materials.TryGetValue(m,out int id)) return id;
                    string map=m.HasProperty("_BaseMap")?"_BaseMap":"_MainTex";
                    var texture=m.HasProperty(map)?m.GetTexture(map):null;
                    var normal=m.HasProperty("_BumpMap")?m.GetTexture("_BumpMap"):null;
                    var s=new Surface {
                        name=m.name, shader=m.shader.name, colourMap=texture?AssetDatabase.GetAssetPath(texture):"",
                        normalMap=normal?AssetDatabase.GetAssetPath(normal):"",
                        colour=m.HasProperty("_BaseColor")?m.GetColor("_BaseColor"):m.HasProperty("_Tint")?m.GetColor("_Tint"):Color.white,
                        glow=m.HasProperty("_EmissionColor") && m.IsKeywordEnabled("_EMISSION")?m.GetColor("_EmissionColor"):Color.black,
                        metal=m.HasProperty("_Metallic")?m.GetFloat("_Metallic"):0,
                        smooth=m.HasProperty("_Smoothness")?m.GetFloat("_Smoothness"):.34f,
                        bump=m.HasProperty("_BumpScale")?m.GetFloat("_BumpScale"):1,
                        tiling=texture?m.GetTextureScale(map):Vector2.one, offset=texture?m.GetTextureOffset(map):Vector2.zero
                    };
                    id=model.materials.Count; model.materials.Add(s);materials.Add(m,id);return id;
                }
                foreach(var t in gate.GetComponentsInChildren<Transform>(true)) {
                    var node=new Element {name=t.name,parent=t==gate.transform?-1:nodes[t.parent],position=t.localPosition,rotation=t.localRotation,scale=t.localScale};
                    nodes[t]=model.nodes.Count;model.nodes.Add(node);
                    var filter=t.GetComponent<MeshFilter>();var rend=t.GetComponent<MeshRenderer>();
                    if(filter && filter.sharedMesh && rend) {
                        var m=filter.sharedMesh;
                        node.mesh=new Geometry {positions=m.vertices,uv=m.uv,parts=Enumerable.Range(0,m.subMeshCount).Select(i=>
                            new Part{material=Index(rend.sharedMaterials[i]),triangles=m.GetTriangles(i)}).ToArray()};
                    }
                    var text=t.GetComponent<TextMesh>();
                    if(text) {node.lettering=text.text;node.textSize=text.characterSize*(text.fontSize/10f);node.textColour=text.color;}
                }
                model.leaf=nodes[cover.leaf];model.closedOffset=cover.closedOffset;model.duration=cover.travelSeconds;
                using(var output=File.Create(args[flag+1]))
                using(var zip=new GZipStream(output,System.IO.Compression.CompressionLevel.Optimal))
                using(var writer=new StreamWriter(zip)) writer.Write(JsonUtility.ToJson(model));
                Debug.Log($"PORTAL SOURCE: {model.nodes.Count} nodes, {model.materials.Count} surfaces; slide {cover.closedOffset} metres");
            } finally {PrefabUtility.UnloadPrefabContents(gate);}
        }
    }
}
