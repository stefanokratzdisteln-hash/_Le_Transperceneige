"""Original enamel miniatures and engraved dials for the 1935 consignments."""
from pathlib import Path
from math import cos, sin, pi
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[3]
OUT=ROOT/"Assets/Transperceneige/Pass/Valuables/Enamel miniatures.png"
SZ=768
TILES=("watch","portrait","map","sundial")


def font(n):
    for f in ("C:/Windows/Fonts/GARA.TTF","DejaVuSerif.ttf"):
        try: return ImageFont.truetype(f,n)
        except OSError: pass
    return ImageFont.load_default()


def tile(key):
    from random import Random
    rng=Random(1893)
    paper=key in ("watch","sundial")
    im=Image.new("RGB",(SZ,SZ),"#e3d4a9" if paper else "#162e43")
    d=ImageDraw.Draw(im)
    def polar(a,r): return (384+r*cos(a),384+r*sin(a))
    def rim(r,ink,w=2): d.ellipse((384-r,384-r,384+r,384+r),outline=ink,width=w)
    gold="#ccb077"; ink="#332d25"
    for i in range(12500):
        x,y=rng.randrange(SZ),rng.randrange(SZ)
        if paper: d.point((x,y), fill=rng.choice(["#dccda1","#e6dab8"]))
        else: d.point((x,y), fill=rng.choice(["#203b4e","#2c445a"]))
    if key=="watch":
        rim(362,gold,9);rim(346,ink,3);rim(336,gold,3);rim(181,gold,3)
        roman="XII I II III IV V VI VII VIII IX X XI".split()
        for i in range(60):
            a=i*2*pi/60-pi/2
            d.line([polar(a,342),polar(a,325 if i%5 else 315)],fill=ink,width=3 if i%5 else 6)
        for i,s in enumerate(roman):
            d.text(polar(i*2*pi/12-pi/2,271),s,anchor="mm",font=font(53),fill=ink)
        for cy,label in ((263,"LUNE"),(495,"SECONDES")):
            d.ellipse((313,cy-63,455,cy+63),fill="#1d4058" if cy<300 else "#ddcfaa",outline=gold,width=5)
            if cy<300:
                d.ellipse((363,cy-31,406,cy+12),fill="#e8c375");d.ellipse((375,cy-35,412,cy+3),fill="#1d4058")
                for j in range(10):
                    x=rng.randrange(330,435);y=rng.randrange(cy-36,cy+25)
                    d.line((x-2,y,x+2,y),fill=gold,width=2);d.line((x,y-2,x,y+2),fill=gold,width=2)
            else:
                d.line((384,cy,406,cy-33),fill=ink,width=3)
            d.text((384,cy+32),label,anchor="mm",fill=gold if cy<300 else ink,font=font(14))
        d.text((384,378),"WINTERBOURNE",anchor="mm",fill=ink,font=font(26))
    elif key=="portrait":
        for k in range(7):
            r=342+k*3
            d.line([polar(a*.008,r+4*sin(a*.008*63)) for a in range(787)],fill=gold,width=2)
        # A fictional sitter in three-quarter profile, powdered curls, velvet and lace.
        d.polygon([(183,624),(220,539),(302,500),(357,459),(417,468),(489,504),(565,526),(608,627)],
                  fill="#394b57",outline="#aaa388",width=5)
        d.polygon([(311,488),(347,434),(351,396),(420,399),(426,475),(460,501),(394,567)],fill="#d0a281")
        d.polygon([(284,299),(307,221),(369,189),(441,205),(468,241),(467,287),(483,319),(507,344),
                   (505,352),(479,359),(475,377),(484,384),(477,393),(463,398),(459,422),(439,443),
                   (407,446),(378,430),(340,400),(311,375)],fill="#dfb792",outline="#9e765e",width=3)
        for i in range(125):
            a=rng.uniform(pi*.72,pi*1.95);r=rng.uniform(70,112)
            x=365+r*cos(a);y=283+r*sin(a)
            d.arc((x-18,y-16,x+19,y+22),72,338,fill=rng.choice(["#b1a18d","#d9c7a4","#7a7168"]),width=6)
        d.arc((421,288,466,310),194,334,fill="#665648",width=6)
        d.line([(437,315),(451,310),(461,316)],fill="#584a3f",width=4)
        d.ellipse((449,311,454,318),fill="#34434b")
        d.line([(464,380),(479,381)],fill="#9a6255",width=3)
        for k in range(19):
            x=313+k*8; y=476+int(51*sin(k*pi/18))
            d.ellipse((x,y,x+15,y+17),fill="#e8dbbd",outline="#a39277",width=2)
        for k in range(6): d.arc((193+k*4,480+k*5,582-k*4,658),5,175,fill="#987c58",width=2)
    elif key=="map":
        for r in range(25,350,23):rim(r,"#456270",2)
        for i in range(36): d.line([polar(i*pi/18,34),polar(i*pi/18,344)],fill="#48636b",width=2)
        for i in range(85):
            a=rng.random()*2*pi;r=rng.uniform(34,315);x,y=polar(a,r)
            rad=rng.choice([1,2,3]);d.ellipse((x-rad,y-rad,x+rad,y+rad),fill="#ebd9a1")
        for i,s in enumerate(("ORI","LEO","LIB","CYG","GEM","TAU")): d.text(polar(i*pi/3,218),s,anchor="mm",fill=gold,font=font(24))
        rim(346,gold,5);rim(358,gold,3)
    else:
        for r in range(90,340,23): d.arc((384-r,400-r,384+r,400+r),180,360,fill="#71432a",width=3)
        for i in range(13):
            a=pi+i*pi/12;d.line([(384,400),(384+321*cos(a),400+321*sin(a))],fill=ink,width=3)
            d.text((384+282*cos(a),400+282*sin(a)),str((i+6)%12 or 12),fill=ink,font=font(20),anchor="mm")
        d.text((384,495),"HOROLOGIUM",fill=ink,font=font(43),anchor="mm")
        d.text((384,554),"N O R I M B E R G A",fill=ink,font=font(24),anchor="mm")
    return im


if __name__=="__main__":
    atlas=Image.new("RGB",(SZ*2,SZ*2))
    for i,key in enumerate(TILES):atlas.paste(tile(key),((i%2)*SZ,(i//2)*SZ))
    OUT.parent.mkdir(parents=True,exist_ok=True)
    atlas.save(OUT,optimize=True)
