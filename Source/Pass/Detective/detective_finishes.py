"""Original tailoring, hair and skin finishes for the Winterbourne detective.

The facial colour plate uses the CC0 Mindfront Aksel complexion, confined to
the visible face and neck UVs; see Credits.md in this folder.
"""
from pathlib import Path
import json,re,uuid,tempfile
import numpy as np
from PIL import Image, ImageFilter, ImageDraw
ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/"Assets/Transperceneige/Pass/Detective"
STATION=ROOT/"Assets/Transperceneige/Pass/Station"
SPEC={
"Holmes moor tweed":("ffffff",.0,.80,"Moor herringbone.png"),"Holmes peat cloth":("544b40",.0,.82,None),
"Holmes waistcoat":("4a5046",.01,.77,None),"Holmes boot leather":("3c2b25",.035,.42,None),
"Holmes piping":("3c342e",.01,.66,None),"Holmes horn":("4a3424",.08,.30,None),
"Holmes aged brass":("b59455",.72,.27,None),"Holmes linen":("d4c9b0",0,.68,None),
"Holmes tie":("533532",.035,.50,None),"Holmes trouser twill":("ffffff",.0,.77,"Charcoal stripe.png"),
"Holmes complexion":("ffffff",.0,.49,"North Fell complexion.png"),"Holmes kid suede":("b1a596",.0,.84,None),
"Holmes suede welt":("9d9080",.0,.84,None),"Holmes black hair":("2c2624",.04,.50,None),
"Holmes hair highlights":("453932",.04,.47,None),"Holmes eyelashes":("332623",.0,.58,None),
"Holmes sclera":("c2b5a8",.02,.32,None),"Holmes iris":("ffffff",.035,.25,"Grey hazel iris.png"),
"Holmes catchlight":("f0e7d7",.01,.17,None),"Holmes lining":("55403a",.02,.55,None)}
def guid(n):return uuid.uuid5(uuid.NAMESPACE_URL,"winterbourne/detective/"+n).hex
def meta(source,dest,token):
    if not dest.is_file():
        dest.write_text(re.sub(r'^guid: \w+','guid: '+guid(token),source.read_text(encoding="utf8"),flags=re.M,count=1),encoding="utf8")
def main():
    OUT.mkdir(parents=True,exist_ok=True);meta(STATION.with_suffix(".meta"),OUT.with_suffix(".meta"),"folder")
    h=768;v,u=np.mgrid[0:h,0:h];rng=np.random.default_rng(1889)
    # Twill direction reverses every eight picks. Three muted yarn colours
    # cross beneath the finer salt-and-pepper fibres.
    band=(u//32)%2
    ridge=np.cos(2*np.pi*(u+(band*2-1)*v)/7.3)
    weft=np.cos(2*np.pi*v/3.9)
    warp=np.sin(2*np.pi*u/4.1)
    arr=np.array([78,73,64])+((ridge*.45+weft*.27+warp*.20+rng.normal(0,.35,(h,h)))*18)[...,None]
    light=((u%352)<6)|((v%352)<6);dark=((u%352)<11)&((u%352)>7)
    arr[light]+=np.array([9,8,4]);arr[dark]-=6
    # Long russet and sage overchecks, subdued in the naturally cool coach.
    arr[((u+63)%352)<3]+=np.array([12,6,2]);arr[((v+73)%352)<4]+=np.array([3,7,4])
    Image.fromarray(np.uint8(np.clip(arr,0,255))).save(OUT/"Moor herringbone.png",optimize=True)
    ribs=np.cos(2*np.pi*(u+v)/5.1)*2+rng.normal(0,2,(h,h));a=np.array([55,55,53])+ribs[...,None]
    a+=(np.maximum(0,np.cos(2*np.pi*u/58))**40)[...,None]*[10,10,8]
    Image.fromarray(np.uint8(np.clip(a,0,255))).save(OUT/"Charcoal stripe.png",optimize=True)
    w=256;yy,xx=np.mgrid[0:w,0:w];r=np.hypot(xx-128,yy-128);theta=np.arctan2(yy-128,xx-128)
    rays=np.sin(87*theta+.8*np.sin(r/7)+.8*np.sin(27*theta))+np.cos(151*theta+r*.1)*.45
    a=np.array([89,100,84])+rays[...,None]*[10,11,9]+(np.cos(r/11)*.4)[...,None]*[6,5,4]
    ring=np.clip(1-(r-102)/18,.18,1);a*=ring[...,None];a+=np.exp(-((r-51)/16)**2)[...,None]*[24,12,2]
    a[r<46]=[25,26,23];Image.fromarray(np.uint8(np.clip(a,0,255))).save(OUT/"Grey hazel iris.png")
    base=Path(tempfile.gettempdir())/"HolmesArtbench/skin-original.png"
    skin=OUT/"North Fell complexion.png"
    if base.exists():
        original=Image.open(base).convert("RGB").filter(ImageFilter.GaussianBlur(.55))
        rgb=np.asarray(original).astype('float32')
        rgb=rgb*.81+np.array([45,37,32]);rgb=np.uint8(np.clip(rgb,0,255))
        data=np.load(Path(__file__).resolve().parent/'Holmes anatomy.npz')
        mask=Image.new('L',original.size);pen=ImageDraw.Draw(mask);size=mask.width-1
        for tri in data['uvtris'][data['mats']==0]:
            uv=data['uv'][tri];pen.polygon([(int(x*size),int((1-y)*size)) for x,y in uv],fill=255)
        mask=mask.filter(ImageFilter.MaxFilter(15))
        result=Image.new('RGB',original.size,(181,152,140));result.paste(Image.fromarray(rgb),(0,0),mask)
        result=result.resize((1536,1536),Image.Resampling.LANCZOS);result.save(skin,optimize=True)
    if not skin.exists():raise ValueError('Face plate required; see Credits.md')
    for name,(colour,metal,rough,texture) in SPEC.items():
        sample=STATION/'Station timetable ink.mat';text=sample.read_text(encoding='utf8')
        text=text.replace('m_Name: '+sample.stem+'\n','m_Name: '+name+'\n')
        reference='{fileID: 0}'
        if texture:
            meta(STATION/'StationHouseEnamel.png.meta',(OUT/texture).with_suffix('.png.meta'),texture)
            reference='{fileID: 2800000, guid: '+guid(texture)+', type: 3}'
        text=re.sub(r'\{fileID: 2800000, guid: \w+, type: 3\}',reference,text)
        color='{r: %.6f, g: %.6f, b: %.6f, a: 1}'%tuple(int(colour[i:i+2],16)/255 for i in (0,2,4))
        for token,value in (('_BaseColor',color),('_Color',color),('_Metallic',str(metal)),('_Smoothness',str(1-rough))):
            text=re.sub(r'(- '+token+r': )[^\r\n]*',r'\g<1>'+value,text)
        path=OUT/(name+'.mat');path.write_text(text,encoding='utf8')
        meta(sample.with_suffix('.mat.meta'),path.with_suffix('.mat.meta'),path.name)

if __name__=="__main__":main()
