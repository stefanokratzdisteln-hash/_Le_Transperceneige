"""Tileable engraved and engine-turned surfaces used by the two travelling masterpieces."""
from pathlib import Path
import numpy as np
from PIL import Image

OUT = Path(__file__).resolve().parents[3] / "Assets/Transperceneige/Pass/Valuables/Optics"


def tiles():
    size = 512
    row, col = np.mgrid[:size,:size]
    u, v = col / size, row / size
    rng = np.random.default_rng(1901)
    tau = 2 * np.pi
    hand = rng.normal(0, 1, (size, size))
    mottled = 0.30 * np.cos(tau*(2*u+v)) + 0.27*np.sin(tau*(3*v-2*u)) + .18*np.cos(tau*(7*u+4*v))
    scratches = np.maximum(0, np.sin(tau*(112*v+0.7*np.sin(tau*u))))**26
    gold = np.clip((1 + 0.021*hand + .035*mottled - .028*scratches)[...,None] * [211,163,85],0,255)
    def image(name, data):
        Image.fromarray(np.rint(data).astype('uint8')).save(OUT/(name+".png"),optimize=True)
    def surface(name, rgb, height, metal, polish, depth):
        image(name+" body",np.clip(rgb,0,255))
        x=(np.roll(height,-1,1)-np.roll(height,1,1))*depth
        y=(np.roll(height,-1,0)-np.roll(height,1,0))*depth
        n=np.dstack((-x,y,np.ones_like(x)))
        n/=np.linalg.norm(n,axis=2)[:,:,None]
        image(name+" relief",(n*.5+.5)*255)
        channel=lambda a:np.broadcast_to(a,(size,size))
        image(name+" polish",np.clip(np.dstack([channel(metal),channel(255),channel(0),channel(polish)]),0,255))
    surface("Burnished vermeil",gold,hand*.05-scratches*.31,242,192+hand*7+14*mottled-scratches*45,.52)
    # Each pair of opposed sine cuts is a set of scallops in a chased silver ground.
    # The cut ground is visible through the dye; the burnished top glaze has separate gloss.
    for name,dye,base in (("Geneva blue",[15,39,55],[21,55,66]),("Imperial mulberry",[61,19,34],[76,35,37])):
        line_a=np.sin(tau*(17*v+1.65*np.cos(tau*9*u)))
        line_b=np.sin(tau*(17*v-1.65*np.cos(tau*9*u)))
        groove=(.5+.5*np.maximum(line_a,line_b))**11
        caustic=np.clip(.55+groove*.37+.10*np.sin(tau*9*u)+mottled*.24,0,1)
        rgb=np.array(dye)+np.array(base)*caustic[:,:,None]+hand[:,:,None]*1.2
        surface(name,rgb,groove+.13*mottled,.07*255,190+caustic*28+hand*2,.95)



def miniature():
    from random import Random
    from math import sin,cos
    from PIL import ImageDraw,ImageFilter
    n=896;rng=Random(1851)
    image=Image.new("RGB",(n,n))
    draw=ImageDraw.Draw(image)
    for y in range(n):
        t=min(1,y/560)
        draw.line((0,y,n,y),fill=tuple(round(a*(1-t)+b*t) for a,b in zip((41,64,75),(218,190,143))))
    for i in range(230):
        x=rng.randrange(n);y=rng.randrange(120,420)
        w=rng.randrange(12,99)
        draw.arc((x-w,y-7,x+w,y+7),195,328,fill=(178,160,136),width=rng.randrange(1,4))
    draw.ellipse((551,288,594,331),fill=(241,219,159))
    def ridge(line,shadow,snow):
        draw.polygon(line+[(n,650),(0,650)],fill=shadow)
        for i,((x0,y0),(x1,y1)) in enumerate(zip(line,line[1:])):
            if y1>y0:
                draw.polygon([(x0,y0),(x1,y1),(x0+(x1-x0)*.28,y0+57),(x0+(x1-x0)*.32,y0+24)],fill=snow)
            for j in range(27):
                t=rng.random(); x=x0+t*(x1-x0); y=y0+t*(y1-y0)
                length=rng.randrange(5,33)
                draw.line((x,y+2,x+length*.63,y+length*.84),fill=tuple(int((a+b)/2) for a,b in zip(shadow,snow)),width=1)
    ridge([(0,441),(91,386),(133,405),(200,356),(236,371),(298,306),(345,337),(412,234),(501,334),(558,311),(683,389),(723,357),(832,402),(896,387)],(93,111,120),(214,206,185))
    ridge([(0,436),(114,391),(207,445),(289,403),(343,426),(455,377),(520,412),(671,357),(814,428),(896,403)],(85,100,105),(153,155,145))
    for y in range(503,n):
        t=(y-503)/(n-503)
        draw.line((0,y,n,y),fill=tuple(round(a*(1-t)+b*t) for a,b in zip((135,148,142),(49,79,84))))
    for i in range(4200):
        x=rng.randrange(n); y=rng.randrange(504,n); length=rng.randrange(1,24)
        reflection=max(0,1-abs(x-(572+14*sin(y/37)))/(16+(y-500)*.16))
        glow=int(8+reflection*52+rng.random()*12)
        c=image.getpixel((x,y));draw.line((x,y,x+length,y),fill=tuple(min(255,a+glow) for a in c))
    draw.polygon([(0,483),(168,450),(244,432),(283,451),(386,482),(506,492),(562,506),(422,510),(252,498),(119,513),(0,518)],fill=(77,88,76))
    for x,y,w,h in ((213,401,46,37),(246,398,27,33),(269,407,30,29),(194,404,17,28)):
        draw.rectangle((x,y,x+w,y+h),fill=(171,153,115),outline=(92,86,70),width=2)
        draw.polygon([(x-5,y),(x+w//2,y-w*.39),(x+w+4,y)],fill=(99,89,78))
        for k in range(3):
            draw.rectangle((x+4+k*w/3,y+9,x+7+k*w/3,y+16),fill=(69,69,59))
    draw.rectangle((208,380,222,419),fill=(162,148,109));draw.polygon([(207,380),(215,350),(225,380)],fill=(80,88,84))
    for i in range(145):
        x=rng.randrange(-15,185);y=rng.randrange(446,493);h=rng.randrange(10,39)
        draw.polygon([(x,y-h),(x-h*.20,y-h*.45),(x-h*.10,y-h*.46),(x-h*.31,y),(x+h*.28,y),(x+h*.13,y-h*.54),(x+h*.19,y-h*.47)],fill=(61,77,69))
    draw.polygon([(655,896),(670,810),(722,773),(749,754),(803,721),(896,690),(896,896)],fill=(81,81,62))
    for i in range(52):
        x=rng.randrange(766,950);h=rng.randrange(25,145);y=int(750-.43*(x-800))+rng.randrange(27)
        draw.line((x,y,x,y-h),fill=(58,63,54),width=2)
        for j in range(4,19):
            b=j/19;yy=y-h+h*b;w=h*b*.19
            draw.polygon([(x,yy-h*.12),(x-w,yy+7),(x,yy+1),(x+w*.84,yy+5)],fill=(51+rng.randrange(8),75+rng.randrange(7),64))
    for j in range(2):
        x=504+j*59;y=606-j*27
        draw.polygon([(x-17,y),(x+19,y),(x+13,y+5),(x-11,y+5)],fill=(75,64,53))
        draw.line((x,y-53+j*6,x,y),fill=(79,71,62),width=2)
        draw.polygon([(x-1,y-47+j*6),(x-2,y-5),(x-14,y-5)],fill=(219,199,156))
    arr=np.asarray(image,dtype=float)
    grain=np.random.default_rng(42).normal(0,2.5,(n,n,1))
    Image.fromarray(np.clip(arr+grain,0,255).astype('uint8')).save(OUT/'Lake at dusk.png',optimize=True)

def moonlit_viaduct():
    from random import Random
    from math import sin,cos,pi
    from PIL import ImageDraw
    size=768
    rng=Random(1935)
    yy,xx=np.mgrid[:size,:size]
    t=np.clip(yy/510,0,1)[...,None]
    glow=np.exp(-((xx-490)**2+(yy-224)**2)/(2*96**2))[...,None]
    sky=np.array([16,34,55])*(1-t)+np.array([111,144,153])*t+glow*np.array([62,64,50])
    img=Image.fromarray(np.uint8(sky))
    d=ImageDraw.Draw(img)
    for i in range(100):
        x=rng.randrange(65,730);y=rng.randrange(60,327);r=rng.choice((1,1,1,2))
        d.ellipse((x-r,y-r,x+r,y+r),fill=(174,191,184))
    d.ellipse((450,177,524,251),fill=(232,222,175))
    for i in range(22):
        x=rng.randrange(462,509);y=rng.randrange(190,236);r=rng.randrange(1,4)
        d.ellipse((x-r,y-r,x+r,y+r),fill=(218,210,166))
    profile=[(0,408),(55,378),(113,390),(182,329),(226,361),(284,288),(316,309),(366,222),(414,298),(444,278),(500,338),(546,314),(626,365),(706,292),(768,343)]
    d.polygon(profile+[(768,557),(0,548)],fill=(57,79,92))
    for (x0,y0),(x1,y1) in zip(profile,profile[1:]):
        if y1>y0:
            d.polygon([(x0,y0),(x1,y1),(x0+.55*(x1-x0),y0+.72*(y1-y0)),(x0+.32*(x1-x0),y0+.35*(y1-y0)),(x0+.12*(x1-x0),y0+.44*(y1-y0))],fill=(177,188,180))
    d.polygon([(0,401),(94,383),(185,463),(282,430),(395,511),(524,440),(623,396),(713,429),(768,398),(768,602),(0,609)],fill=(48,69,77))
    for i in range(170):
        x=rng.randrange(size);y=int(431+65*sin(x/size*pi))+rng.randrange(0,34);h=rng.randrange(5,17)
        d.polygon([(x,y-h),(x-h*.24,y),(x+h*.3,y)],fill=(37,53,62))
    d.polygon([(0,576),(125,586),(279,574),(359,562),(432,568),(523,561),(610,548),(768,545),(768,768),(0,768)],fill=(76,102,112))
    for i in range(1100):
        x=rng.randrange(size);y=rng.randrange(580,768);length=rng.randrange(3,19)
        d.line((x,y,x+length,y),fill=(105,131,133) if i%5 else (162,171,157))
    # The tributary is crossed on five semicircular masonry spans with their
    # stone voussoirs painted separately and a crest of snow along the parapet.
    for i in range(7):
        x=-33+i*124;base=398-int(x*.08)
        d.polygon([(x,base+10),(x+124,base),(x+124,base+39),(x,base+49)],fill=(108,114,108))
        for j in range(23):
            a=j*pi/23;b=(j+1)*pi/23
            corners=[(x+62+rr*cos(t),base+94-rr*sin(t)-5*(1+cos(t))) for rr,t in ((50,a),(66,a),(66,b),(50,b))]
            d.polygon(corners,fill=(116+j%4*7,124+j%3*5,119+j%4*4),outline=(69,85,86))
        d.polygon([(x-10,base+31),(x+10,base+29),(x+8,base+173),(x-13,base+177)],fill=(81,93,94))
        d.line((x-11,base+33,x-13,base+174),fill=(169,172,153),width=3)
        for j in range(7): d.line((x-10,base+46+j*17,x+8,base+43+j*17),fill=(66,77,80),width=1)
    d.line((0,403,768,341),fill=(203,206,188),width=7)
    d.line((0,415,768,353),fill=(61,75,78),width=5)
    a=np.asarray(img)+np.random.default_rng(1936).normal(0,2,(size,size,1))
    Image.fromarray(np.clip(a,0,255).astype('uint8')).save(OUT/'Greywater at night.png',optimize=True)

if __name__=="__main__":
    OUT.mkdir(parents=True,exist_ok=True)
    tiles()
    miniature()
    moonlit_viaduct()
