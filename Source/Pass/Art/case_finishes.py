"""Surface swatches for the railway parcel trunk and the mineral-faced strongbox."""
from math import sin,pi
from pathlib import Path
from random import Random
from PIL import Image,ImageDraw,ImageFilter,ImageOps

root=Path(__file__).resolve().parents[3]/"Assets/Transperceneige/Pass"
out=root/"Cases"
out.mkdir(exist_ok=True)
wood=Image.open(root/"Station/WeatheredWalnut.png").convert("L").resize((512,512),Image.Resampling.LANCZOS)
wood=ImageOps.autocontrast(wood,cutoff=1)
ImageOps.colorize(wood,(96,49,35),(198,132,88),mid=(158,87,58)).save(out/"Oxblood trunk boards.png",optimize=True)
r=Random(1935)
cloud=Image.new('L',(19,19));cloud.putdata([r.randint(105,155) for _ in range(19*19)])
cloud=cloud.resize((512,512),Image.Resampling.BICUBIC).filter(ImageFilter.GaussianBlur(4))
veins=Image.new('L',(512,512));v=ImageDraw.Draw(veins)
for k in range(18):
    x=r.randrange(-230,590);y=r.randrange(-80,540);slope=r.uniform(-.18,.46)
    pts=[]
    for j in range(r.randrange(28,64)):
        x+=r.uniform(4,9);y+=5*slope+r.uniform(-2.4,2.0)
        pts.append((x,y+6*sin(j*.12+k)))
    v.line(pts,fill=r.randrange(35,80),width=r.choice((1,1,2,3)))
net=veins.filter(ImageFilter.GaussianBlur(1.15))
mineral=Image.new('RGB',(512,512)); px=[]
for a,b in zip(cloud.getdata(),net.getdata()):
    n=(a-128)*.24-b*.37+r.uniform(-1.5,1.5)
    px.append(tuple(int(c+n) for c in (224,213,186)))
mineral.putdata(px);mineral.save(out/"Marbled cream panels.png",optimize=True)

# Suede turns softly at grazing angles. Both edge seams run lengthwise;
# no ring wraps around a finger joint. The palm and working tips are slightly worn.
import numpy as np
rr=np.random.default_rng(1891)
yy,xx=np.mgrid[:512,:512]
u=(xx+.5)/512;v=1-(yy+.5)/512
palm=(1-np.sin(2*pi*u))*.5
irregular=np.array(cloud,dtype=float)-128
noise=rr.normal(0,1.7,(512,512))
nap=rr.random((512,512))>.96
seams=np.maximum.reduce([np.exp(-((u-c)/.0038)**2) for c in (.032,.469)])
thread=seams*(.23+.77*(np.sin(v*2*pi*39)>.10))
shade=-13*palm-5*np.exp(-((v-.97)/.087)**2)-6*thread+irregular*.084+noise+1.5*nap
rgb=np.stack([np.clip(base+shade,0,255).astype("uint8") for base in (177,165,150)],axis=2)
Image.fromarray(rgb).save(out/"Greige suede.png",optimize=True)
fibre=rr.normal(0,1,(512,512))
du=(np.roll(fibre,1,1)-np.roll(fibre,-1,1))*.027
dv=(np.roll(fibre,1,0)-np.roll(fibre,-1,0))*.043
normal=np.stack((du,dv,np.ones_like(du)),axis=2);normal/=np.linalg.norm(normal,axis=2)[:,:,None]
Image.fromarray(((normal*.5+.5)*255).astype("uint8")).save(out/"Suede nap.png",optimize=True)
