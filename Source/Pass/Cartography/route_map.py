"""Draw the Winterbourne Pass passenger route plate. Requires Pillow and NumPy.

Chainages, vehicle lengths, platform, river centre and viaduct bays follow the
Unity sources. Relief uses a deterministic cartographic approximation of the
ride's terrain envelopes; neither contours nor the profile are survey elevations.
"""
from functools import lru_cache
from math import cos, pi, sin
from pathlib import Path
from random import Random

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[3]
DEST = ROOT / 'Previews/Pass/RouteMap.png'
W, H = 2800, 1740
PAPER, WHITE, INK, GREEN = '#eee9d9', '#f7f3e7', '#263832', '#1e3a35'
MUTED, RULE, GOLD, RED, WATER = '#51645a', '#aaa68a', '#c4a369', '#924e3f', '#6b9697'
EDGE = 66


@lru_cache(None)
def font(role, size):
    names = {
        'head': ['C:/Windows/Fonts/BASKVILL.TTF','/System/Library/Fonts/Supplemental/Baskerville.ttc','DejaVuSerif.ttf'],
        'roman':['C:/Windows/Fonts/GARABD.TTF','/usr/share/fonts/truetype/dejavu/DejaVuSerif.ttf','DejaVuSerif.ttf'],
        'sans':['C:/Windows/Fonts/GIL_____.TTF','/System/Library/Fonts/Supplemental/GillSans.ttc','DejaVuSans.ttf'],
        'num':['C:/Windows/Fonts/bahnschrift.ttf','DejaVuSans.ttf'],
        'cn':['C:/Windows/Fonts/msyh.ttc','/System/Library/Fonts/PingFang.ttc','/usr/share/fonts/opentype/noto/NotoSansCJK-Regular.ttc'],
        'cnb':['C:/Windows/Fonts/msyhbd.ttc','/System/Library/Fonts/PingFang.ttc','/usr/share/fonts/opentype/noto/NotoSansCJK-Bold.ttc'],
    }
    for file in names[role]:
        try: return ImageFont.truetype(file,size)
        except OSError: pass
    raise OSError(f'No {role} font available for map lettering')


def label(draw,xy,message,role,size,colour=INK,anchor='lm',**kw):
    draw.text(xy,message,font=font(role,size),fill=colour,anchor=anchor,**kw)


def tracked(draw,x,y,letters,size,spacing,colour):
    f=font('sans',size)
    for char in letters:
        draw.text((x,y),char,font=f,fill=colour,anchor='lm')
        x+=f.getlength(char)+spacing


def smooth(a,b,v):
    t=np.clip((v-a)/(b-a),0,1)
    return t*t*(3-2*t)


@lru_cache(None)
def noise_field(seed):
    return np.random.default_rng(seed).uniform(-1,1,(257,257)).astype('float32')


def noise(x,s,scale_x,scale_s,seed):
    x=x*scale_x+79;s=s*scale_s+37
    i=np.floor(x).astype(int)%256;j=np.floor(s).astype(int)%256
    u=smooth(0,1,x-np.floor(x));v=smooth(0,1,s-np.floor(s))
    grid=noise_field(seed)
    return (grid[i,j]*(1-u)+grid[i+1,j]*u)*(1-v)+(grid[i,j+1]*(1-u)+grid[i+1,j+1]*u)*v


def river_x(s):
    return -25+12*np.sin((s-1410)*.014)+6*np.sin(s*.035)


def relief(x,s):
    d=np.abs(x)
    coarse=noise(x,s,.016,.009,73)*.5+.5
    fine=noise(x,s,.078,.057,53)*.5+.5
    shoulder=np.where(x<0,-7.4,9.3)+9*(2*coarse-1)+2.1*fine
    station=1-smooth(0,34,np.maximum(920-s,s-1110))
    h=np.where(x<0,smooth(13,58,d),smooth(8,39,d))*shoulder*(1-station*(1-smooth(13,48,d)))
    h+=smooth(42,125,d)*(15+23*coarse+9*np.sin(s*.009+x*.022))
    ridge=170+28*np.sin(s*.0081+np.where(x<0,0,2.3))
    h+=np.exp(-((d-ridge)/55)**2)*(22+33*(1-np.abs(noise(x,s,.019,.012,17))))
    crest=430+70*np.sin(s*.0042+np.where(x<0,.6,2.5))
    h+=np.exp(-((d-crest)/133)**2)*(55+92*(1-np.abs(noise(x,s,.009,.008,31)))+43*(1-np.abs(noise(x,s,.020,.019,77))))
    bore=smooth(433,504,s)*(1-smooth(757,826,s))
    h+=bore*smooth(4.4,20,d)*(17+19*coarse)*np.exp(-d/87)
    # Hatch the physical cover of the tunnel so the underground track reads clearly.
    h+=bore*28*np.exp(-(d/47)**2)
    gorge=smooth(1395,1488,s)*(1-smooth(2152,2211,s))
    bank=np.where(x<0,87+17*np.sin(s*.013),84+26*np.sin(s*.0092+2))
    floor=-69-11*coarse+3*fine
    river=river_x(s)
    bed=-82.4+(floor+82.4)*smooth(10,24,np.abs(x-river))
    trough=bed+(h+10*coarse-bed)*smooth(bank*.44,bank,d)
    return h+(trough-h)*gorge*(1-smooth(120,205,d))


def terrain(w,h,scale,lo_s):
    s=np.arange(w,dtype='float32')[None,:]/scale+lo_s
    x=(np.arange(h,dtype='float32')[:,None]-h*.50)/scale
    alt=relief(x,s)
    dy,dx=np.gradient(alt)
    shade=np.clip(.76*dy-.57*dx,-4,4)
    higher=smooth(-55,120,alt)
    low=np.array([162,182,171]); high=np.array([231,230,204])
    rgb=low+(high-low)*higher[...,None]+shade[...,None]*6
    contour=np.abs(alt/7.-np.round(alt/7.))*7
    # Fixed-width contour strokes, softened according to the local relief gradient.
    slope=np.hypot(dx,dy)
    line=np.clip(1-contour/np.maximum(.07,.7*slope),0,1)*smooth(.10,.9,slope)
    coarse=np.abs(alt/35-np.round(alt/35))*35
    index=np.clip(1-coarse/np.maximum(.07,slope*1.05),0,1)*smooth(.16,.88,slope)
    rgb-=line[...,None]*21+index[...,None]*18
    grain=np.random.default_rng(8).normal(0,1.20,(h,w))
    rgb+=grain[...,None]
    return Image.fromarray(np.clip(rgb,0,255).astype('uint8'))


def trunk(draw,xx,yy,r):
    ink='#546d5a'
    draw.line((xx,yy-r,xx,yy+r),fill=ink,width=max(1,int(r/4)))
    for j in (0,.48):
        y=yy-r*.62+j*r
        draw.line([(xx-r*.72,y+r*.58),(xx,y),(xx+r*.72,y+r*.58)],fill=ink,width=max(1,int(r/4)))


def dashed(d,a,b,y,colour,width,on=18,off=12):
    x=a
    while x<b:
        d.line((x,y,min(b,x+on),y),fill=colour,width=width)
        x+=on+off


def map_panel(page,d):
    xl,xr=EDGE,W-EDGE
    top,bottom=282,886
    w,h=xr-xl,bottom-top
    lo,hi=-88,2256
    sc=w/(hi-lo)
    def px(s): return xl+(s-lo)*sc
    def py(x): return top+h/2+x*sc
    page.paste(terrain(w,h,sc,lo),(xl,top))
    d.rectangle((xl,top,xr,bottom),outline=MUTED,width=2)
    # Pass-era map grids are deliberately subtle and evenly spaced in map metres.
    for s in range(0,2251,250):
        d.line((px(s),top,px(s),bottom),fill='#a4b1a0',width=1)
        d.line((px(s),top,px(s),top+12),fill=MUTED,width=2)
    for x in range(-200,201,100):
        d.line((xl,py(x),xl+12,py(x)),fill=MUTED,width=2)
        d.line((xr-12,py(x),xr,py(x)),fill=MUTED,width=2)
    rng=Random(911)
    for centre_s,radius,side in ((35,125,1),(237,158,-1),(378,113,1),(476,84,-1),
                                  (758,113,1),(863,62,-1),(1143,117,1),(1301,92,-1),
                                  (1520,138,-1),(1773,181,1),(1991,96,-1)):
        for _ in range(64):
            a=rng.uniform(-1,1); b=rng.uniform(0,1)
            if a*a+(.5-b)**2*2>1:continue
            s=centre_s+a*radius; x=side*(62+b*69+17*sin(s*.025))
            if lo+5<s<hi-5: trunk(d,px(s),py(x),rng.uniform(3,6))
    # River only in the gorge, as in the Unity geometry, tapering underneath each bank.
    water_secs=np.arange(1420,2199,2)
    offsets=river_x(water_secs)
    widths=8.0*(smooth(1408,1489,water_secs)*(1-smooth(2148,2192,water_secs)))
    up=[(px(s),py(v-b)) for s,v,b in zip(water_secs,offsets,widths)]
    down=[(px(s),py(v+b)) for s,v,b in zip(water_secs,offsets,widths)]
    d.polygon(up+down[::-1],fill=WATER)
    d.line([(px(s),py(v)) for s,v in zip(water_secs,offsets)],fill='#d1e4df',width=2)
    y=py(0)
    d.line((px(0),y,px(480),y),fill=WHITE,width=12)
    d.line((px(0),y,px(480),y),fill=INK,width=6)
    dashed(d,px(480),px(780),y,'#63584d',6,12,9)
    for s in (480,780): d.line((px(s),y-12,px(s),y+12),fill=INK,width=5)
    d.line((px(780),y,px(1400),y),fill=WHITE,width=12)
    d.line((px(780),y,px(1400),y),fill=INK,width=6)
    # Long viaduct and its regularly spaced 40 m bays; the missing bay is truly blank.
    for a,b in ((1400,2120),(2150,2210)):
        for dy in (-4,4):d.line((px(a),y+dy,px(b),y+dy),fill=INK,width=3)
    for s in list(range(1400,2121,40))+[2150,2190,2210]:
        d.line((px(s),y-10,px(s),y+10),fill=INK,width=3)
    d.line((px(2121),y-3,px(2124),y+5),fill=RED,width=5)
    d.line((px(2146),y-4,px(2150),y+4),fill=RED,width=5)
    d.ellipse((px(2137)-3,y+11,px(2137)+3,y+17),fill=RED)
    d.rounded_rectangle((px(920),y-18,px(1110),y-8),radius=2,fill='#bc9659',outline='#68573b',width=2)
    d.line((px(80),y,px(110),y),fill=RED,width=5)
    d.polygon([(px(112),y),(px(99),y-8),(px(99),y+8)],fill=RED)
    # Landmark annotation cards sit apart from the rails, never hiding a junction or gap.
    def note(s,dx,dy,eng,cn,accent=INK):
        xx=px(s);yy=y+dy
        cow=font('cnb',27).getlength(cn)
        width=max(font('sans',20).getlength(eng),cow)+27
        x0=xx+dx-width/2
        x0=max(xl+8,min(x0,xr-width-8))
        by=yy+57 if dy<0 else yy
        d.line([(xx,y-10 if dy<0 else y+10),(xx,by+(-by+y)*.30),
                (x0+width/2,by)],fill=accent,width=2)
        d.rounded_rectangle((x0,yy,x0+width,yy+57),radius=4,fill='#f0eddf',outline='#b9b7a2')
        label(d,(x0+width/2,yy+18),cn,'cnb',25,accent,'mm')
        label(d,(x0+width/2,yy+43),eng,'sans',19,MUTED,'mm')
    note(0,53,73,'0 m   /   DEPARTURE','雪岭山地')
    note(480,-15,-128,'480 m   /   PORTAL','入隧道')
    note(780,0,60,'780 m   /   EXIT PORTAL','出隧道')
    note(1095,-42,-152,'1,095 m   /   FELLBRIDGE','费尔桥站')
    note(1400,0,84,'1,400 m   /   BRIDGEHEAD','峡谷桥头')
    note(1820,0,-99,"1,820 m   /   KEEPER'S COTTAGE",'桥下小屋 · 游戏起点',GREEN)
    note(2120,-2,-133,'2,120 m   /   SEVERED SPAN','爆破断口',RED)
    label(d,(px(246),py(180)),'雪松林','cn',21,'#4c6556','mm')
    label(d,(px(1635),py(-72)),'冰封河道','cn',22,'#416b71','mm',stroke_width=2,stroke_fill='#d1d8c7')
    label(d,(px(1778),py(100)),'高 架 桥   ·   18 跨','cn',24,INK,'mm',stroke_width=2,stroke_fill='#d4dbc8')
    # Correct scale refers to the main panel only.
    ax,ay=xl+29,bottom-34
    d.rounded_rectangle((ax-12,ay-35,ax+250*sc+25,ay+20),radius=3,fill='#edead9')
    for i in range(5):
        d.rectangle((ax+i*50*sc,ay,ax+(i+1)*50*sc,ay+7),fill=INK if i%2==0 else WHITE,outline=INK)
    label(d,(ax,ay-15),'0','num',17)
    label(d,(ax+250*sc,ay-15),'250 m','num',17,anchor='rm')
    label(d,(xr-20,bottom-25),'沿线里程与桥跨按比例；等高线、林地为地貌示意。','cn',19,INK,'rm',
          stroke_width=3,stroke_fill='#d4daca')


def legend(d):
    y=917;x=EDGE
    for kind,title in (('track','地面线路'),('tunnel','隧道'),('bridge','钢梁高架桥'),
                       ('station','站台'),('river','冰河'),('break','断轨与残墩')):
        if kind=='track':d.line((x,y,x+58,y),fill=INK,width=5)
        if kind=='tunnel':dashed(d,x,x+58,y,INK,4,10,6)
        if kind=='bridge':
            for yy in (-3,3):d.line((x,y+yy,x+58,y+yy),fill=INK,width=2)
            for dd in (4,24,44):d.line((x+dd,y-8,x+dd,y+8),fill=INK,width=2)
        if kind=='station':d.rectangle((x,y-5,x+58,y+5),fill=GOLD,outline=MUTED)
        if kind=='river':d.line([(x,y+3),(x+17,y-2),(x+37,y+1),(x+58,y-4)],fill=WATER,width=5)
        if kind=='break':
            d.line([(x,y),(x+19,y),(x+25,y+5)],fill=RED,width=4)
            d.line([(x+36,y-5),(x+42,y),(x+58,y)],fill=RED,width=4)
        label(d,(x+71,y),title,'cn',21)
        x+=max(220,104+font('cn',21).getlength(title))
    label(d,(W-EDGE,y),'列车行进方向  →      图上方为卧铺窗侧（世界 −X）','cn',20,MUTED,'rm')


def card(d,x,y,w,title,code):
    d.rounded_rectangle((x,y,x+w,y+457),radius=5,fill=WHITE,outline=RULE,width=2)
    tracked(d,x+28,y+31,code,19,2.5,MUTED)
    label(d,(x+27,y+76),title,'cnb',33)
    d.line((x+27,y+108,x+w-27,y+108),fill=RULE,width=1)


def station_detail(d,x,y,w):
    card(d,x,y,w,'Fellbridge 站  ·  停车与追车','PLATFORM PLAN')
    left,right=x+43,x+w-41
    def sx(s):return left+(s-915)*(right-left)/(1116-915)
    py=y+220
    d.rectangle((sx(920),py-59,sx(1110),py-10),fill='#d6c6a2',outline='#8f8165',width=2)
    d.line((sx(920),py-12,sx(1110),py-12),fill='#FFF7D9',width=5)
    for s,l in ((985,13),(1053,8.8)):
        d.rectangle((sx(s-l/2),py-48,sx(s+l/2),py-30),fill='#738278',outline=GREEN)
    d.rectangle((sx(995),py-54,sx(1015),py-27),fill=GOLD,outline=GREEN,width=2)
    label(d,((sx(995)+sx(1015))/2,py-40),'站长楼','cnb',16,INK,'mm')
    label(d,(sx(948),py-40),'190 m 站台','cn',20,GREEN,'mm')
    for s,t in ((920,'920'),(1095,'1095'),(1110,'1110 m')):
        d.line((sx(s),py-78,sx(s),py-61),fill=MUTED,width=2)
        label(d,(sx(s),py-91),t,'num',18,MUTED,'mm')
    d.line((left,py+9,right,py+9),fill=MUTED,width=3)
    pitches=[13.8,8.4]+[19.66]*7
    p=1095; mid={}
    for i,length in enumerate(pitches,1):
        a,b=sx(p-length),sx(p);mid[i]=p-length/2
        d.rounded_rectangle((a+1,py+1,b-1,py+28),radius=3,
                            fill=GREEN if i<=2 else '#6a7666',outline=WHITE,width=1)
        label(d,((a+b)/2,py+15),f'{i:02d}','num',16,'#fff4d6','mm')
        p-=length
    for i in range(3,10):
        offsets = (-8.04, 0, 8.04) if i in (3,9) else (-8.04,8.04)
        for off in offsets:
            a=sx(mid[i]+off)
            d.polygon([(a,py+1),(a-5,py-8),(a+5,py-8)],fill=GREEN)
    grip=sx(mid[9]-8.04)
    d.line((grip,py-8,grip,py+64,grip+21,py+64),fill=RED,width=3)
    d.ellipse((grip-5,py-12,grip+5,py-2),fill=RED)
    label(d,(grip+29,py+65),'09 侧门：最后登车踏板','cn',20,RED)
    label(d,(x+27,y+352),'停站 2 分钟，余 15 秒鸣哨。站长楼一楼有传送门。','cnb',25)
    label(d,(x+27,y+397),'进屋后列车继续计时。末车驶离前可返回站台，按 E 开门、Shift 追车、空格跳上。','cn',23,MUTED)


def bridge_detail(d,x,y,w):
    card(d,x,y,w,'钢梁高架桥  ·  断跨与逃生','LONGITUDINAL SECTION')
    left,right=x+36,x+w-35
    def sx(s):return left+(s-1330)*(right-left)/(2220-1330)
    def hy(h):return y+177-h*1.48
    s=np.linspace(1330,2220,250)
    bed=relief(np.zeros_like(s),s)
    terrain_poly=[(sx(t),hy(min(float(v),18))) for t,v in zip(s,bed)]
    d.polygon([(left,y+305),*terrain_poly,(right,y+305)],fill='#c0c8b1')
    d.line(terrain_poly,fill='#82937f',width=2)
    for start,end in ((1400,2120),(2150,2210)):
        d.rectangle((sx(start),hy(0),sx(end),hy(-5)),fill=GREEN)
    for i in range(18):
        x0,x1=sx(1400+i*40),sx(1400+(i+1)*40)
        d.line([(x0,hy(-1)),((x0+x1)/2,hy(-7)),(x1,hy(-1))],fill='#6e7e6e',width=2)
    for t in [*range(1440,2121,40),2150,2190]:
        v=min(-9,float(relief(np.array(0.),np.array(float(t)))))
        xx=sx(t)
        d.polygon([(xx-3,hy(-4)),(xx+3,hy(-4)),(xx+5,hy(v)),(xx-5,hy(v))],fill='#687769')
    label(d,(sx(1600),hy(16)),'桥面 1400—2120 m','cn',20,MUTED,'mm')
    x0,x1=sx(2120),sx(2150)
    d.line((x0,hy(-.5),x0,hy(13),x1,hy(13),x1,hy(-.5)),fill=RED,width=2)
    label(d,((x0+x1)/2,hy(26)),'30 m 断口','cn',21,RED,'mm')
    # Open parachute symbol: illustrative descent, not a unique mandatory jump point.
    xx,yy=sx(1890),hy(-39)
    d.pieslice((xx-26,yy-19,xx+26,yy+20),180,360,fill='#e5b770',outline=RED,width=2)
    for aa in (-24,-9,10,24):d.line((xx+aa,yy+1,xx,yy+28),fill=RED,width=2)
    d.ellipse((xx-3,yy+27,xx+3,yy+33),fill=RED)
    for yy2 in (hy(-65),hy(-75)):
        d.line((sx(1600),yy2,sx(1614),yy2),fill=WATER,width=3)
    label(d,(x+27,y+352),'桥上约 120 秒；断口之前必须离车。','cnb',25)
    label(d,(x+27,y+397),'安全着陆后，走进落点旁的传送门返回桥下小屋。高度为示意。','cn',23,MUTED)


def main():
    page=Image.new('RGB',(W,H),PAPER)
    d=ImageDraw.Draw(page)
    d.rectangle((0,0,W,194),fill=GREEN)
    d.line((EDGE,19,W-EDGE,19),fill=GOLD,width=2)
    tracked(d,EDGE,48,'NORTH FELL RAILWAY',23,5,GOLD)
    label(d,(EDGE-2,122),'Winterbourne Pass','head',99,WHITE)
    label(d,(W-EDGE,84),'雪岭断桥线','cnb',54,WHITE,'rm')
    label(d,(W-EDGE,147),'全线示意图   /   2.12 公里','cn',26,'#dac99e','rm')
    label(d,(EDGE,238),'线路平面','cnb',36)
    tracked(d,EDGE+172,240,'ROUTE ALIGNMENT',23,4,MUTED)
    label(d,(W-EDGE,239),'所有运行里程以车头为准','cn',23,MUTED,'rm')
    map_panel(page,d);legend(d)
    gap=28;w=(W-2*EDGE-gap)/2
    station_detail(d,EDGE,974,w)
    bridge_detail(d,EDGE+w+gap,974,w)
    d.line((EDGE,1471,W-EDGE,1471),fill=RULE,width=2)
    label(d,(EDGE,1515),'行车节点','cnb',29)
    tracked(d,EDGE+151,1518,'RUNNING ORDER',20,3,MUTED)
    checkpoints=[('0','山地出发'),('480','入洞'),('780','出洞'),('1095','到站停车'),
                 ('1400','上桥警报'),('2120','车头到达断口')]
    a=EDGE+17;b=W-EDGE-230
    for i,(metres,caption) in enumerate(checkpoints):
        xx=a+(b-a)*i/5
        if i<5:d.line((xx+22,1568,xx+(b-a)/5-13,1568),fill=RULE,width=2)
        d.ellipse((xx-6,1562,xx+6,1574),fill=RED if i==5 else GREEN)
        label(d,(xx,1606),metres+' m','num',32,RED if i==5 else INK)
        label(d,(xx,1652),caption,'cn',24,MUTED)
    d.rectangle((0,H-22,W,H),fill=GREEN)
    DEST.parent.mkdir(parents=True,exist_ok=True)
    page.save(DEST,dpi=(240,240),optimize=True)
    print(DEST, page.size)


if __name__=='__main__':
    main()
