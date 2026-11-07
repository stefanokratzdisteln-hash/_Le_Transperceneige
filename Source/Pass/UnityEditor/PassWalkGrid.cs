using System;
using System.Collections.Generic;
using UnityEngine;
using Winterbourne.Demo;
using Winterbourne.Pass;

namespace Winterbourne.Editor
{
    // This is a test navigator. It only proposes walking directions; the original
    // CharacterController, moving train, doors and portal triggers resolve every step.
    internal sealed class PassWalkGrid
    {
        const float Cell=.16f;
        readonly Transform origin;
        readonly float left, first, floor, radius, height;
        readonly int width, length;
        readonly bool train;
        readonly CharacterController body;
        readonly Collider[] occupants=new Collider[32];
        readonly RaycastHit[] ground=new RaycastHit[16];
        readonly sbyte[] clear;
        readonly int[] previous;
        readonly Queue<int> frontier=new Queue<int>();
        static readonly Vector2Int[] Directions={
            new Vector2Int(0,-1),new Vector2Int(0,1),new Vector2Int(-1,0),new Vector2Int(1,0),
            new Vector2Int(-1,-1),new Vector2Int(1,-1),new Vector2Int(-1,1),new Vector2Int(1,1)
        };

        internal PassWalkGrid(Transform frame, CharacterController player, float xmin, float xmax,
            float zmin, float zmax, float floorHeight, bool aboard)
        {
            origin=frame; body=player; train=aboard;
            left=xmin; first=zmin; floor=floorHeight;
            radius=body.radius; height=body.height;
            width=Mathf.CeilToInt((xmax-xmin)/Cell)+1;
            length=Mathf.CeilToInt((zmax-zmin)/Cell)+1;
            clear=new sbyte[width*length];
            previous=new int[width*length];
            Array.Fill(previous,-2);
        }

        Vector3 Local(int i) => new Vector3(left+(i%width)*Cell,floor,first+(i/width)*Cell);
        bool IsFree(int i)
        {
            if(clear[i]!=0)return clear[i]>0;
            Vector3 feet=origin.TransformPoint(Local(i));
            bool supported=false;
            int count=Physics.RaycastNonAlloc(feet+Vector3.up*.15f,Vector3.down,ground,.35f,
                Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            for(int j=0;j<count;j++)
            {
                var hit=ground[j];
                if(hit.collider==body || hit.normal.y<.65f || (train&&!hit.transform.IsChildOf(origin)))continue;
                supported=true;
                break;
            }
            bool fits=supported;
            if(fits)
            {
                int hits=Physics.OverlapCapsuleNonAlloc(feet+Vector3.up*(radius+.065f),feet+Vector3.up*(height-radius-.03f),
                    radius-.016f,occupants,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                for(int j=0;j<hits;j++)
                {
                    var hit=occupants[j];
                    if(hit==body || hit.transform.IsChildOf(body.transform))continue;
                    var hinge=hit.GetComponentInParent<PassDoor>();
                    if(hinge && hinge.Interior && !hinge.TargetOpen)continue;
                    var slider=hit.GetComponentInParent<Car07SlidingDoor>();
                    if(slider && slider.TargetClosed)continue;
                    fits=false; break;
                }
            }
            clear[i]=(sbyte)(fits?1:-1);
            return fits;
        }

        internal List<Vector3> Path(Func<Vector3,bool> goal)
        {
            Vector3 start=origin.InverseTransformPoint(body.transform.position);
            int sx=Mathf.Clamp(Mathf.RoundToInt((start.x-left)/Cell),0,width-1);
            int sz=Mathf.Clamp(Mathf.RoundToInt((start.z-first)/Cell),0,length-1);
            int seed=-1;
            float nearest=float.PositiveInfinity;
            for(int z=Mathf.Max(0,sz-3);z<=Mathf.Min(length-1,sz+3);z++)
            for(int x=Mathf.Max(0,sx-3);x<=Mathf.Min(width-1,sx+3);x++)
            {
                int i=z*width+x;
                float d=(Local(i)-start).sqrMagnitude;
                if(d<nearest && IsFree(i)){seed=i;nearest=d;}
            }
            if(seed<0)throw new InvalidOperationException($"No walkable start near {start:F2} in {origin.name}");
            previous[seed]=-1; frontier.Enqueue(seed);
            while(frontier.Count>0)
            {
                int i=frontier.Dequeue();
                if(goal(origin.TransformPoint(Local(i))))
                {
                    var path=new List<Vector3>();
                    for(int n=i;n>=0;n=previous[n])path.Add(Local(n));
                    path.Reverse();
                    // The driver can hold Shift along straight stretches and slow
                    // before a corner. Keep every change of grid heading.
                    var turns=new List<Vector3>{path[0]};
                    for(int k=1;k<path.Count-1;k++)
                    {
                        var arriving=path[k]-path[k-1];
                        var leaving=path[k+1]-path[k];
                        if((arriving-leaving).sqrMagnitude>.0001f)turns.Add(path[k]);
                    }
                    if(path.Count>1)turns.Add(path[path.Count-1]);
                    return turns;
                }
                int x=i%width,z=i/width;
                foreach(var step in Directions)
                {
                    int a=x+step.x,b=z+step.y;
                    if(a<0||b<0||a>=width||b>=length)continue;
                    int next=b*width+a;
                    if(previous[next]!=-2 || !IsFree(next))continue;
                    if(step.x!=0 && step.y!=0 && (!IsFree(z*width+a)||!IsFree(b*width+x)))continue;
                    previous[next]=i;frontier.Enqueue(next);
                }
            }
            throw new InvalidOperationException($"Unreachable on {origin.name} from {start:F2} ({Array.FindAll(previous,n=>n!=-2).Length} visited cells)");
        }
    }
}
