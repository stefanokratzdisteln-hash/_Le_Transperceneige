"""Prepare the detective's shaped, posed anatomy from the pinned CC0 MPFB base.

The resulting NPZ is included for offline Blender rebuilds. Use this program only
when editing the anatomical proportions or replacing the original shape targets.
"""
from concurrent.futures import ThreadPoolExecutor
import gzip,json,tempfile
from pathlib import Path
import numpy as np
from scipy.spatial.transform import Rotation
import requests

HERE=Path(__file__).resolve().parent
CACHE=Path(tempfile.gettempdir())/"HolmesArtbench"
URL="https://raw.githubusercontent.com/makehumancommunity/mpfb2/6ff1eb358f6c1959ced77e79e6dd2406d9a71f49/src/mpfb/data/"
SHAPES={"macrodetails/caucasian-male-young":.72,"macrodetails/caucasian-male-old":.28,
        "nose/nose-hump-incr":.51,"nose/nose-scale-depth-incr":.60,"nose/nose-point-down":.25,
        "nose/nose-point-width-decr":.25,"head/head-scale-horiz-decr":.19,"chin/chin-prominent-incr":.25,
        "cheek/l-cheek-bones-incr":.42,"cheek/r-cheek-bones-incr":.42,
        "cheek/l-cheek-volume-decr":.37,"cheek/r-cheek-volume-decr":.37,
        "eyes/l-eye-scale-incr":.28,"eyes/r-eye-scale-incr":.28,"eyebrows/eyebrows-trans-forward":.30}


def fetch(name):
    CACHE.mkdir(exist_ok=True)
    p=CACHE/Path(name).name
    if not p.is_file():
        response=requests.get(URL+name,timeout=30);response.raise_for_status();p.write_bytes(response.content)
    return p


def matrix_between(a,b,at,destination):
    a=a/np.linalg.norm(a);b=b/np.linalg.norm(b);v=np.cross(a,b)
    rot=Rotation.from_rotvec(v*np.arctan2(np.linalg.norm(v),np.dot(a,b))/max(np.linalg.norm(v),1e-10)).as_matrix()
    return rot,destination-rot@at


def main():
    wanted=["3dobjs/base.obj","rigs/standard/rig.mixamo_unity.json","rigs/standard/weights.mixamo_unity.json"]
    wanted+=["targets/"+shape+".target.gz" for shape in SHAPES]
    with ThreadPoolExecutor(max_workers=9) as pool: list(pool.map(fetch,wanted))
    verts=[];uvs=[];groups={};faces=[];tex=[];gid=""
    for line in fetch(wanted[0]).read_text().splitlines():
        if line.startswith("v "):verts.append([float(x) for x in line.split()[1:4]])
        elif line.startswith("vt "):uvs.append([float(x) for x in line.split()[1:3]])
        elif line.startswith("g "):gid=line.split()[1];groups.setdefault(gid,set())
        elif line.startswith("f "):
            entries=[s.split("/") for s in line.split()[1:]]
            inds=[int(e[0])-1 for e in entries];groups[gid].update(inds)
            if gid=="body":
                faces.append(inds);tex.append([int(e[1])-1 for e in entries])
    vv=np.array(verts)
    for shape,weight in SHAPES.items():
        delta=np.loadtxt(gzip.open(fetch("targets/"+shape+".target.gz")))
        vv[delta[:,0].astype(int)]+=weight*delta[:,1:]
    raw=vv[:,[0,2,1]]*.1
    raw[:,1]*=-1
    raw[:,2]-=raw[list(groups["body"]),2].min()
    crown=raw[list(groups["body"]),2].max()
    # Expand the head around the collar, leave the body narrow and the chin well defined.
    alpha=np.clip((raw[:,2]-1.405)/.105,0,1)
    raw[:,0]*=.89+.36*alpha
    raw[:,1]*=.91+.31*alpha
    z=raw[:,2].copy()
    neck=1.44
    raw[:,2]=np.minimum(z,neck)*.962+np.maximum(0,z-neck)*((1.78-neck*.962)/(crown-neck))
    def center(name):return raw[list(groups[name])].mean(0)
    old=json.loads(fetch(wanted[1]).read_text())
    weights=json.loads(fetch(wanted[2]).read_text())["weights"]
    def eval_end(val):
        if val.get("strategy")=="CUBE":return center(val["cube_name"])
        inds=val.get("vertex_indices") or ([val["vertex_index"]] if "vertex_index" in val else None)
        if inds:return raw[inds].mean(0)
        return np.array(val["default_position"])+[0,0,.8]
    joints={key.split(':')[1]:(eval_end(d['head']),eval_end(d['tail']),d['parent'].split(':')[-1]) for key,d in old.items() if isinstance(d,dict) and 'head' in d}
    transforms={}
    def transform(name):
        if name in transforms:return transforms[name]
        head,tail,parent=joints[name]
        rot,offset=transform(parent) if parent else (np.eye(3),np.zeros(3))
        posed=rot@head+offset
        side=1 if name.startswith("Left") else -1
        label=name.removeprefix("Left").removeprefix("Right")
        intended={"Arm":np.array([side*.070,.012,-.27]),"ForeArm":np.array([side*.025,-.069,-.25]),
                  "UpLeg":np.array([side*.040,-.017,-.41]),"Leg":np.array([side*.010,.018,-.40])}
        if label in intended:
            rot,offset=matrix_between(tail-head,intended[label],head,posed)
        elif label=="Hand":
            # Lower the relaxed fingers, with the palms facing the outside seam.
            rot,offset=matrix_between(tail-head,np.array([side*.008,-.018,-.090]),head,posed)
        transforms[name]=rot,offset
        return rot,offset
    new=np.zeros_like(raw);ws=np.zeros(len(raw))
    bone_weights={}
    def rename(n):
        return n.replace("Spine2","Chest").replace("Spine1","SpineMid").replace("ForeArm","LowerArm") if not n.endswith("Arm") or n.endswith("ForeArm") else n.replace("Arm","UpperArm")
    def canonical(n):
        return rename(n).replace("LeftUpLeg","LeftUpperLeg").replace("RightUpLeg","RightUpperLeg").replace("LeftLeg","LeftLowerLeg").replace("RightLeg","RightLowerLeg")
    for name in joints:
        rot,offset=transform(name)
        wt=np.asarray(weights.get("mixamorig:"+name,[]))
        if not len(wt):continue
        ids=wt[:,0].astype(int);val=wt[:,1]
        pos=raw[ids]@rot.T+offset
        new[ids]+=pos*val[:,None];ws[ids]+=val
        bone_weights[canonical(name)]=wt.tolist()
    valid=ws>0;new[valid]/=ws[valid,None];new[~valid]=raw[~valid]
    data={}
    for name,(head,tail,parent) in joints.items():
        rot,off=transform(name)
        data[canonical(name)]=[(rot@head+off).tolist(),(rot@tail+off).tolist(),canonical(parent)]
    for key in ('LeftEye','RightEye','Head','Neck','Chest','Hips','LeftUpperArm','LeftLowerArm','LeftHand','LeftUpperLeg','LeftLowerLeg','LeftFoot'):
        print(key,np.round(data[key][0],3),np.round(data[key][1],3))
    print('body bottom',new[list(groups['body']),2].min())
    # Trim unused UV space from the shipped skin by retaining only these visible
    # polygons as the derived performer: head/neck and fully articulated hands.
    kept=[];texpoly=[];regions=[]
    for inds,uv in zip(faces,tex):
        centre=raw[inds].mean(0)
        if centre[2]>1.395 and abs(centre[0])<.16: region=0
        elif abs(centre[0])>.348 and centre[2]<1.075:region=1
        else:continue
        kept.append(inds);texpoly.append(uv);regions.append(region)
    used=np.unique(np.concatenate(kept))
    remap={int(old):i for i,old in enumerate(used)}
    compact_weights={k:[[remap[int(index)],w] for index,w in vals if int(index) in remap] for k,vals in bone_weights.items()}
    # Object arrays are unnecessary: the basemesh topology is quad-based.
    topo=[];tcoords=[];mats=[]
    for f,uv,reg in zip(kept,texpoly,regions):
        for i in range(1,len(f)-1):
            topo.append([remap[f[0]],remap[f[i]],remap[f[i+1]]]);tcoords.append([uv[0],uv[i],uv[i+1]]);mats.append(reg)
    np.savez_compressed(HERE/'Holmes anatomy.npz',coords=new[used].astype('float32'),tris=np.asarray(topo,dtype='int32'),
        uv=np.asarray(uvs,dtype='float32'),uvtris=np.asarray(tcoords,dtype='int32'),mats=np.array(mats,dtype='uint8'),
        joints=json.dumps(data),weights=json.dumps(compact_weights))
    # Used for the original cap, eyelids, hairline and sideburn fit.
    print('head bounds',raw[list(groups['body'])][raw[list(groups['body']),2]>1.51].min(0).round(3),
          raw[list(groups['body'])][raw[list(groups['body']),2]>1.51].max(0).round(3))
if __name__=="__main__":main()
