"""Winterbourne's handbound library volumes. Pillow and NumPy; no external photographs.

3072-square albedo atlas, top to bottom: 16x2 spine tiles, then 8x4
matching boards across the first 3/4 of the width, then 2x4 page cuts.
"""
from pathlib import Path
import math
import random
from functools import lru_cache

import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageFilter

OUT = Path(__file__).resolve().parents[3] / 'Assets/Transperceneige/Pass/Station/LibraryBindings.png'
# Real libraries accumulate plain cloth, half calf, paper labels and repaired bindings in uneven sets.
# style, faded base colour, title or shelf label. A few numbers reflect volumes from one set.
VOLUMES = (
    ('half',  (96,63,42), 'THE BORDER|COUNTIES'),
    ('blind', (74,69,49), 'FIELD NOTES'),
    ('cloth', (76,82,84), 'WATER|AND FELL'),
    ('plain', (85,64,48), ''),
    ('half',  (78,55,46), 'COUNTY|SURVEY'),
    ('label', (86,79,60), 'Journal'),
    ('linen', (132,116,90), ''),
    ('vertical',(67,71,62),'NIGHT DUTIES'),
    ('calf',  (108,71,50), 'LETTERS|FROM YORK'),
    ('plain', (71,67,59), ''),
    ('cloth', (91,69,58), 'COASTAL|WALKS'),
    ('half',  (83,54,50), 'NORTHERN|ANNUAL'),
    ('label', (74,85,83), 'Expenses'),
    ('calf',  (112,83,57),'PENNINE|WAYS'),
    ('linen', (100,99,81), ''),
    ('vertical',(71,74,76),'THE GARDENER'),
    ('half',  (86,59,41), 'MILL AND|MOUNTAIN'),
    ('blind', (75,64,57), ''),
    ('paper', (147,130,100), 'FELLBRIDGE|ALMANAC'),
    ('plain', (73,75,68), ''),
    ('calf',  (92,64,46), 'WINTER|CHRONICLE'),
    ('cloth', (69,76,89), 'THE|RIVER WYE'),
    ('label', (95,77,58), 'Parcels'),
    ('linen', (91,80,78), ''),
    ('half',  (96,58,46), 'COUNTY|RECORDS'),
    ('half',  (91,60,47), 'COUNTY|RECORDS'),
    ('half',  (98,64,49), 'COUNTY|RECORDS'),
    ('vertical',(76,75,62),'THE NORTH ROAD'),
    ('plain', (96,85,64), ''),
    ('paper', (123,115,96), 'RULES|OF THE LINE'),
    ('blind', (65,66,68), 'LINES'),
    ('label', (99,88,68), 'Stores'),
)


def noise(r, w, h, pitch):
    coarse = r.normal(128,38,(max(3,h//pitch+2),max(3,w//pitch+2))).clip(0,255).astype('uint8')
    field = Image.fromarray(coarse).resize((w,h),Image.Resampling.BICUBIC)
    return (np.asarray(field,dtype='float32')-128)/38


def as_image(rgb): return Image.fromarray(np.uint8(np.clip(rgb,0,255)))


def material(w,h,colour,kind,seed):
    r=np.random.default_rng(seed)
    y,x=np.mgrid[:h,:w].astype('float32')
    cloud=noise(r,w,h,65); fleck=noise(r,w,h,7); medium=noise(r,w,h,21)
    l=3*cloud+2.3*medium+1.8*fleck
    if kind=='skin':
        # Creased and polished islands of goatskin. Distances arise from irregular cell nuclei.
        u=x/6.7+.21*medium+.12*fleck; v=y/4.3+.17*medium+x*.039
        fi=np.floor(u); fj=np.floor(v)
        ix=fi.astype(int)+3; iy=fj.astype(int)+3
        grid=r.uniform(.09,.91,(iy.max()+2,ix.max()+2,2))
        near=np.full_like(x,20);second=near.copy()
        for dy in (-1,0,1):
            for dx in (-1,0,1):
                cell=grid[iy+dy,ix+dx]
                distance=(dx+cell[:,:,0]-(u-fi))**2+(dy+cell[:,:,1]-(v-fj))**2
                second=np.minimum(second,np.maximum(near,distance));near=np.minimum(near,distance)
        grain=np.minimum((np.sqrt(second)-np.sqrt(near))*3.5,1)
        l+=5.6*(np.roll(grain,1,axis=0)-grain)-3.4*(1-grain)
    elif kind=='cloth':
        warp=np.cos(x*1.42+.48*medium+.17*fleck)
        weft=np.cos(y*1.66+.50*medium+x*.045)
        l+=2.8*warp+2.5*weft+3.4*warp*weft+1.3*np.sin(x*.66+y*.57)
    else:
        l+=1.3*np.sin(x*1.6)+r.normal(0,1.2,(h,w))
    l+=r.normal(0,1.1,(h,w))
    return as_image(np.array(colour)+l[:,:,None]*[1.03,1,.87])


def stamp(image, mask, tone, seed, fade=.32, emboss=True):
    r=np.random.default_rng(seed)
    w,h=image.size
    cloud=noise(r,w,h,9)+.64*noise(r,w,h,35)
    a=np.asarray(mask,dtype=float)/255
    # Edges of old foil vary; the underlying impression survives where the foil did not.
    coverage=np.clip(1-fade*.42 + cloud*fade*.52+r.normal(0,.16,(h,w)),0,1)
    if emboss:
        dark=Image.fromarray(np.uint8(a*112))
        canvas=Image.new('L',(w,h)); canvas.paste(dark,(1,2));image.paste((40,32,27),(0,0,w,h),canvas)
    gold=np.array(tone)+(3.5*cloud+r.normal(0,4,(h,w)))[:,:,None]
    image.paste(as_image(gold),(0,0),Image.fromarray(np.uint8(np.clip(a*coverage*255,0,255))))


@lru_cache(None)
def font(size, italic=False, bold=False):
    fonts=(['GARAIT.TTF','timesi.ttf','DejaVuSerif-Italic.ttf'] if italic else
           ['GARABD.TTF','timesbd.ttf','DejaVuSerif-Bold.ttf'] if bold else
           ['GARA.TTF','times.ttf','DejaVuSerif.ttf'])
    for f in fonts:
        for path in (Path('C:/Windows/Fonts')/f,Path(f)):
            try: return ImageFont.truetype(str(path),size)
            except OSError: pass
    raise RuntimeError('A serif face is required for the spine art.')


def centre_text(d,x,y,lines,size,max_width,italic=False,bold=False):
    f=font(size,italic,bold)
    lines=lines.split('|')
    while any(d.textlength(line,font=f)>max_width for line in lines):
        size-=1;f=font(size,italic,bold)
    top=y-(len(lines)-1)*size*.66
    for i,line in enumerate(lines):
        d.text((x,top+i*size*1.25),line,anchor='mm',font=f,fill=255)


def abrasion(im,seed,spine,condition):
    w,h=im.size;r=np.random.default_rng(seed);rr=random.Random(seed)
    y,x=np.mgrid[:h,:w]
    edge=np.minimum.reduce([x,w-1-x,y,h-1-y])
    body=noise(r,w,h,30)*.65+noise(r,w,h,5)*.27+r.normal(0,.22,(h,w))
    margin=4+12*condition
    exposed=np.clip((margin-edge)/margin + body-.48,0,1)
    if spine:
        exposed=np.maximum(exposed,np.clip((margin-np.minimum(y,h-1-y))/margin +body-.12,0,1))
    mask=Image.fromarray(np.uint8(exposed*(23+82*condition)))
    im.paste((157,133,102),(0,0,w,h),mask)
    scr=Image.new('L',(w,h));d=ImageDraw.Draw(scr)
    for _ in range(w*h//650):
        xx,yy=rr.randrange(w),rr.randrange(h)
        if yy>30 and yy<h-30 and 12<xx<w-12 and rr.random()<.82:continue
        n=rr.randint(2,14); dx=rr.choice([-1,1])*rr.random()*.9
        pts=[(xx+dx*j+rr.random()*.9,yy+j) for j in range(n)]
        d.line(pts,fill=rr.randrange(18,72),width=1)
    im.paste((189,161,122),(0,0,w,h),scr)


def spine(idx):
    style,col,words=VOLUMES[idx];w,h=192,768;seed=6000+idx
    rr=random.Random(seed);r=np.random.default_rng(seed); y,x=np.mgrid[:h,:w]
    skin=style in ('half','calf','plain','blind')
    base=material(w,h,col,'skin' if skin else 'paper' if style=='paper' else 'cloth',seed)
    # Shallow rounded back, sloping shoulders and compressed headcaps.
    t=(x+.5)/w
    roll=5*np.sin(np.pi*t)**.78-12*(1-np.sin(np.pi*t)**.38)
    roll+=3*np.exp(-((t-.16)/.055)**2)-5*np.exp(-((t-.96)/.025)**2)
    lines=[]
    if style in ('calf','half'):
        count=rr.choice([3,4,5]);lines=[int((n+.67)*h/(count+.55)) for n in range(count)]
    elif style in ('plain','blind') and idx%3!=0:
        lines=[55+rr.randrange(20),h-75+rr.randrange(16)]
    for b in lines:
        arc=b+2.2*np.sin(np.pi*t)+1.3*np.sin(5*t)
        diff=y-arc
        roll+=-8*np.exp(-((diff-6)/3.2)**2)+10*np.exp(-((diff+3)/3.7)**2)+3.9*np.exp(-(diff/7)**2)
    roll-=8*np.exp(-((y-7)/2.3)**2)+6*np.exp(-((h-y-10)/2.7)**2)
    im=as_image(np.asarray(base,dtype=float)+roll[:,:,None])
    die=Image.new('L',(w,h));d=ImageDraw.Draw(die)
    if style in ('calf','half'):
        # Some have a contrasting leather lettering piece; others are blocked directly on the back.
        top=(lines[0]+12) if lines else 99; bot=(lines[1]-12) if len(lines)>1 else 237
        if idx%3!=0:
            rect=[12,top,w-13,bot];tone=np.clip(np.array(col)*rr.uniform(.47,.76)+[5,2,0],0,255)
            lab=material(rect[2]-rect[0]+1,rect[3]-rect[1]+1,tone,'skin',seed+791)
            im.paste(lab,(rect[0],rect[1]));ImageDraw.Draw(im).rectangle(rect,outline=tuple(np.uint8(np.maximum(0,tone-7))),width=2)
        centre_text(d,w/2,(top+bot)/2,words,26,w-32,bold=idx%2==0)
        if idx%4!=0:
            for b in lines:
                d.line([(13,b+7),(w-14,b+7)],fill=rr.randrange(100,205),width=1)
            for yy in (top+3,bot-4): d.line([(17,yy),(w-18,yy)],fill=144,width=1)
        if idx in (24,25,26): centre_text(d,w/2,lines[-1]-28,['I','II','III'][idx-24],23,w-34)
        elif idx%2==0: centre_text(d,w/2,h-52,'NORTH FELL',12,w-30)
        stamp(im,die,rr.choice([(169,135,82),(155,122,77),(166,139,97)]),seed+24,.68)
    elif style=='label':
        cx=rr.randrange(-7,8);start=rr.randint(130,214);height=rr.randint(88,125)
        # Pasted label cut by hand, with soot at its edges and faded iron-gall ink.
        bounds=(21+cx,start,w-26+cx,start+height)
        label=material(bounds[2]-bounds[0],height,(157+rr.randrange(13),145,117),'paper',seed+520)
        patch=Image.new('L',label.size);pd=ImageDraw.Draw(patch)
        lw,lh=label.size;pd.polygon([(1,3),(lw-6,0),(lw-1,5),(lw-3,lh-3),(4,lh-1),(0,lh-5)],fill=247)
        im.paste(label,(bounds[0],start),patch);pen=ImageDraw.Draw(im)
        pen.line([(bounds[0]+7,start+height*.71),(bounds[2]-7,start+height*.71)],fill=(126,109,83),width=1)
        centre_text(d,w/2+cx,start+height*.48,words,24,w-60,italic=True)
        stamp(im,die,(63,58,49),seed+89,.46,False)
    elif style=='vertical':
        strip=Image.new('L',(h,w));sd=ImageDraw.Draw(strip)
        centre_text(sd,h*.50,w/2,words,26,h*.66)
        die=strip.transpose(Image.Transpose.ROTATE_270)
        stamp(im,die,(161,143,110) if idx%2 else (47,44,38),seed+87,.62,False)
    elif style in ('cloth','paper','blind'):
        tone=(46,44,39) if style=='paper' else tuple(np.maximum(0,np.asarray(col)-19)) if style=='blind' else (157,149,123)
        if words:
            centre_text(d,w/2,rr.randint(158,218),words,29,w-27,bold=style=='cloth')
            if style=='paper':d.line([(21,269),(w-22,269)],fill=146,width=1)
        if style=='blind':
            for k in range(2):
                d.line([(15,45+3*k),(w-16,45+3*k)],fill=120,width=1)
                d.line([(15,h-54+3*k),(w-16,h-54+3*k)],fill=120,width=1)
        stamp(im,die,tone,seed+48,.63,style=='blind')
    if idx%8 in (1,4) and style!='label':
        # A bookbinder's tiny old shelf mark, never repeated on every binding.
        pen=ImageDraw.Draw(im); yy=rr.randrange(h-119,h-92)
        rect=(61,yy,131,yy+27);pen.polygon([(61,yy+2),(128,yy),(131,yy+24),(63,yy+27)],fill=(151,139,113))
        pen.text((96,yy+13),str(17+idx*3),font=font(15,True),fill=(75,71,60),anchor='mm')
    abrasion(im,seed+36,True,(.74,.30,.18,.88,.45,.24,.78,.46)[idx%8])
    return im


def cover(idx):
    style,col,_=VOLUMES[idx];w,h=288,384;seed=8000+idx
    rr=random.Random(seed);r=np.random.default_rng(seed);y,x=np.mgrid[:h,:w]
    kind='skin' if style in ('calf','half','blind','plain') else 'paper' if style=='paper' else 'cloth'
    im=material(w,h,col,kind,seed)
    if style=='half':
        # Water-marbling on paper sides, quarter calf hinge and matching corner pieces.
        field=noise(r,w,h,46)
        phase=x/6+y/12+field*12+noise(r,w,h,22)*4
        curl=np.sin(phase+3*np.sin(y/22+field*3))
        secondary=np.sin(phase*1.6+.7*np.cos(x/13))
        tone=np.array(rr.choice([(97,87,65),(78,79,67),(98,78,65),(81,77,72)]))
        dye=np.asarray(material(w,h,tone,'paper',seed+1),dtype=float)
        dye+=np.exp(-((curl-.23)/.25)**2)[:,:,None]*[27,20,10]
        dye-=np.exp(-((secondary+.53)/.17)**2)[:,:,None]*[18,15,12]
        mask=Image.new('L',(w,h));m=ImageDraw.Draw(mask)
        m.polygon([(59,0),(w-37,0),(w,37),(w,h-42),(w-42,h),(59,h)],fill=255)
        im.paste(as_image(dye),(0,0),mask)
    elif style=='paper':
        edge=Image.new('RGBA',(w,h));e=ImageDraw.Draw(edge)
        e.rectangle((19,21,w-18,h-20),outline=(73,63,51,108),width=1)
        e.rectangle((23,26,w-22,h-25),outline=(77,69,55,79),width=1)
        im=Image.alpha_composite(im.convert('RGBA'),edge).convert('RGB')
    elif style in ('calf','blind') or idx%4==2:
        die=Image.new('L',(w,h));d=ImageDraw.Draw(die)
        for k in (0,4): d.rectangle((23+k,17+k,w-17-k,h-17-k),outline=142,width=1)
        stamp(im,die,np.maximum(0,np.array(col)-17),seed+21,.24)
    crease= -11*np.exp(-((x-13)/3)**2)+6*np.exp(-((x-18)/2.5)**2)
    im=as_image(np.asarray(im,dtype=float)+crease[:,:,None])
    abrasion(im,seed+29,False,(.74,.30,.18,.88,.45,.24,.78,.46)[idx%8])
    return im


def foreedge(idx):
    w=h=384;r=np.random.default_rng(330+idx);rr=random.Random(931+idx)
    y,x=np.mgrid[:h,:w]; fx=x/(w-1)
    base=[(167,153,126),(187,177,149),(157,145,118),(176,160,131),(146,138,120),(183,171,143),(171,158,132),(165,145,117)][idx]
    im=np.asarray(material(w,h,base,'paper',410+idx),dtype=float)
    # Individual page runs slope gently and form signatures of eight or sixteen leaves.
    waves=.68*np.sin(fx*5+idx)+.51*fx*np.cos(y/37+idx)
    coord=y+waves
    lines=r.normal(0,6,h+4);sections=r.integers(8,19,size=45).cumsum()
    for n in sections[sections<h]:lines[n]-=rr.uniform(6,15)
    l=np.interp(np.clip(coord,0,h+2),np.arange(len(lines)),lines)
    l+=2.5*np.sin(np.pi*fx)-6*np.exp(-np.minimum(x,w-1-x)/11)
    if idx==4 or idx==7:
        # Speckled edges: diluted ochre rubbed onto the closed paper block.
        nibs=np.maximum(0,r.random((h,w))-.942)*235
        im-=nibs[:,:,None]*np.array([1,1.3,1.6])
    return as_image(im+l[:,:,None])


def main():
    atlas=Image.new('RGB',(3072,3072))
    for idx in range(32):
        atlas.paste(spine(idx),((idx%16)*192,(idx//16)*768))
        atlas.paste(cover(idx),((idx%8)*288,1536+(idx//8)*384))
    for idx in range(8):
        atlas.paste(foreedge(idx),(2304+(idx%2)*384,1536+(idx//2)*384))
    atlas.save(OUT,optimize=True)
    print(OUT,atlas.size,OUT.stat().st_size)


if __name__=='__main__':main()
