using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Winterbourne.Editor
{
    // Cut only the meshes overridden by the Pass scene. Imported coachwork remains usable
    // on its own. Clipping the triangles also preserves the timber, sheet seams and snow UVs.
    internal static class PassHatchCut
    {
        internal static bool Cut(MeshFilter filter, Transform car, Rect opening, int number)
        {
            var original = filter.sharedMesh;
            var toCar = car.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            var positions = new List<Vector3>(original.vertices);
            var normals = new List<Vector3>(original.normals);
            var uv = new List<Vector2>(original.uv);
            bool hasNormals = normals.Count == positions.Count;
            bool hasUV = uv.Count == positions.Count;
            var footprint = new List<Vector2>(positions.Count);
            foreach (var vertex in positions)
            {
                Vector3 p = toCar.MultiplyPoint3x4(vertex);
                footprint.Add(new Vector2(p.x,p.z));
            }
            var pieces = new List<List<int>>();
            bool changed = false;
            var edges = new[] { opening.xMin, opening.xMax, opening.yMin, opening.yMax };
            float Distance(int index,int edge)
            {
                float coord = edge < 2 ? footprint[index].x : footprint[index].y;
                return edge % 2 == 0 ? coord - edges[edge] : edges[edge] - coord;
            }
            int Intersection(int a, int b, float t)
            {
                if (t <= .000001f) return a;
                if (t >= .999999f) return b;
                int index = positions.Count;
                positions.Add(Vector3.LerpUnclamped(positions[a],positions[b],t));
                footprint.Add(Vector2.LerpUnclamped(footprint[a],footprint[b],t));
                if (hasNormals) normals.Add(Vector3.LerpUnclamped(normals[a],normals[b],t).normalized);
                if (hasUV) uv.Add(Vector2.LerpUnclamped(uv[a],uv[b],t));
                return index;
            }
            void Triangulate(List<int> polygon,List<int> triangles)
            {
                for (int i=2; i<polygon.Count; i++)
                {
                    int a=polygon[0], b=polygon[i-1], c=polygon[i];
                    if (Vector3.Cross(positions[b]-positions[a],positions[c]-positions[a]).sqrMagnitude < 1e-16f) continue;
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                }
            }
            for (int submesh=0; submesh<original.subMeshCount; submesh++)
            {
                int[] faces=original.GetTriangles(submesh);
                var triangles=new List<int>(faces.Length);
                pieces.Add(triangles);
                for (int i=0; i<faces.Length; i+=3)
                {
                    int a=faces[i], b=faces[i+1], c=faces[i+2];
                    bool separate=false;
                    for(int edge=0;edge<4;edge++)
                        if(Distance(a,edge)<=0 && Distance(b,edge)<=0 && Distance(c,edge)<=0) { separate=true; break; }
                    if(separate) { triangles.Add(a); triangles.Add(b); triangles.Add(c); continue; }

                    var inside=new List<int>(6){a,b,c};
                    // At each plane retain the outside fragment and pass the inside
                    // fragment on. After four cuts only the rectangular hole is discarded.
                    for(int edge=0;edge<4 && inside.Count>0;edge++)
                    {
                        var outside=new List<int>(6);
                        var next=new List<int>(6);
                        int prev=inside[inside.Count-1];
                        float prevDist=Distance(prev,edge);
                        foreach(int current in inside)
                        {
                            float dist=Distance(current,edge);
                            if((prevDist<0 && dist>0)||(prevDist>0 && dist<0))
                            {
                                int at=Intersection(prev,current,prevDist/(prevDist-dist));
                                outside.Add(at); next.Add(at);
                            }
                            if(dist<=0)outside.Add(current);
                            if(dist>=0)next.Add(current);
                            prev=current;prevDist=dist;
                        }
                        Triangulate(outside,triangles);
                        inside=next;
                    }
                    changed=true;
                }
            }
            if (!changed) return false;
            var mesh=new Mesh();
            if(positions.Count>65534) mesh.indexFormat=IndexFormat.UInt32;
            mesh.SetVertices(positions);
            if(hasUV) mesh.SetUVs(0,uv);
            if(hasNormals) mesh.SetNormals(normals);
            mesh.subMeshCount=pieces.Count;
            for(int i=0;i<pieces.Count;i++)mesh.SetTriangles(pieces[i],i);
            if(!hasNormals)mesh.RecalculateNormals();
            if(hasUV)mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            filter.sharedMesh=StationFabrication.Save(mesh,$"Car {number:D2} hatch cut {filter.name}");
            PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
            return true;
        }
    }
}
