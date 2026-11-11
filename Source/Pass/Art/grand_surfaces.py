"""Paint original repeatable surfaces for the Winterbourne grand consignments.

Run before grand_treasures.py. The wood, watered steel, striped shell and foil
lettering are parameterized rather than embedded photographs of museum objects.
"""
from pathlib import Path
import math
import numpy as np
from PIL import Image, ImageDraw, ImageFont

OUT = Path(__file__).resolve().parents[3] / "Assets/Transperceneige/Pass/Valuables/Grand finishes"
SIZE = 768


def image(name, base, variation, contrasts):
    colour = np.asarray(base, dtype=float)[None, None, :] + variation[..., None] * np.asarray(contrasts)
    Image.fromarray(np.uint8(np.clip(colour, 0, 255))).save(OUT / (name + ".png"), optimize=True)


def font(pixels):
    for candidate in ("C:/Windows/Fonts/gara.ttf", "DejaVuSerif.ttf"):
        try:
            return ImageFont.truetype(candidate, pixels)
        except OSError:
            pass
    return ImageFont.load_default()


def astronomical_cover():
    im = Image.new("RGB", (SIZE, 960), "#243b43")
    pen = ImageDraw.Draw(im)
    rng = np.random.default_rng(1883)
    for _ in range(24000):
        x, y = rng.integers([0, 0], [SIZE, 960])
        pen.line((x,y,x+1,y-2), fill=rng.choice(["#233841","#263e47","#20383e"]))
    for inset, weight in ((18, 5), (31, 2), (46, 4), (60, 1), (77, 2)):
        pen.rectangle((inset, inset, SIZE-inset,960-inset), outline="#bba16e", width=weight)
    for left in (0, 1):
        for lower in (0, 1):
            x = 98 if not left else 670
            y = 104 if not lower else 856
            for j in range(3):
                pen.arc((x-31-j*7,y-32-j*7,x+31+j*7,y+32+j*7), 15+left*90, 290+left*75, fill="#b39a67",width=2)
    cx, cy = 384, 455
    for radius in (87,110,142,182,199,210):
        pen.ellipse((cx-radius,cy-radius,cx+radius,cy+radius),outline="#bca26f",width=2)
    for i in range(180):
        a = math.tau*i/180
        r = 199 if i%5 else 190
        pen.line((cx+r*math.cos(a),cy+r*math.sin(a),cx+209*math.cos(a),cy+209*math.sin(a)),fill="#d5b978",width=2)
    stars=[(int(rng.normal(cx,110)),int(rng.normal(cy,104))) for _ in range(90)]
    for i,(x,y) in enumerate(stars):
        if (x-cx)**2+(y-cy)**2>175**2: continue
        if i%5==0 and i and abs(x-stars[i-1][0])<90 and abs(y-stars[i-1][1])<90:
            pen.line((x,y,*stars[i-1]),fill="#776e55",width=1)
        r=2 if i%7 else 4
        pen.line((x-r,y,x+r,y),fill="#e3ce83",width=1)
        pen.line((x,y-r,x,y+r),fill="#e3ce83",width=1)
    for text,y,size in (("EPHEMERIS",171,61),("A S T R O N O M I C A",239,22),("TABULAE BOREALES",734,35),("ANNO  M·D·LXXIIII",800,21)):
        pen.text((384,y),text,fill="#e7cc8c",font=font(size),anchor="mm",stroke_width=0)
    im.save(OUT/"Tooled celestial calf.png",optimize=True)


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    v,u=np.mgrid[0:SIZE,0:SIZE]/SIZE
    rng=np.random.default_rng(1773)
    noise=rng.normal(0,.05,(SIZE,SIZE))
    grain=np.sin(math.tau*(76*v+1.1*np.sin(math.tau*u)+.55*np.sin(math.tau*(v*3+u)))) 
    flame=np.sin(math.tau*(7*u+.45*np.cos(math.tau*v+2*u)+.21*np.sin(math.tau*(2*v+u))))
    pores=(1+np.cos(math.tau*210*(v+.005*np.sin(6*u))))**9/512
    variation=.35*grain+.53*flame+.31*np.cos(math.tau*(17*v+u))+noise-1.3*pores
    image("Violin flamed maple", (152,86,39), variation, (43,32,22))
    grain=np.sin(math.tau*(79*v+.22*np.sin(5*u)))**7
    # The edge of the lower bouts and the corners lose their dark overvarnish
    # first. Wider reddish wear fields survive mip mapping in the inventory.
    honey=np.exp(-((u-.30)/.25)**2-((v-.20)/.14)**2)+.65*np.exp(-((u-.50)/.24)**2-((v-.85)/.14)**2)
    old_glaze=.34*np.cos(2.8*u+v*7)+.27*np.cos(24*v)+.25*np.sin(33*(u+v))+.20*np.sin(49*v)
    image("Varnished alpine spruce",(125,62,30), .68*grain+old_glaze+1.35*honey+noise,(37,27,14))
    strata=.5+.5*np.sin(math.tau*(28*u+3.2*np.cos(7*v)+1.18*np.sin(17*u-4*v)))
    nacre=.66*np.cos(7*v+14*u)+.31*np.sin(21*v+8*u)+noise
    bands=np.maximum(0,strata-.39)**1.1*2.5*(.40+.57*np.maximum(0,np.cos(math.tau*v)))
    base=np.zeros((SIZE,SIZE,3))+[225,213,189]
    shell=base+nacre[...,None]*[13,9,16]-bands[...,None]*[91,117,105]
    Image.fromarray(np.uint8(np.clip(shell,0,255))).save(OUT/"Chambered shell.png",optimize=True)
    phase=math.tau*(2.2*u+v+.38*np.sin(math.tau*v)+.31*np.sin(16*u))
    luster=np.stack([np.sin(phase+.3),np.sin(phase+2.1),np.sin(phase+3.7)],axis=2)
    nacre=np.array([214,209,197])+luster*[18,17,20]+(noise*11)[...,None]
    Image.fromarray(np.uint8(np.clip(nacre,0,255))).save(OUT/"Aperture nacre.png",optimize=True)
    water=np.sin(math.tau*(63*v+2.3*np.sin(12*u+8*v)+.85*np.sin(41*u-31*v)))
    image("Watered crucible steel",(128,133,135),.58*water+.23*np.sin(u*58+v*18)+noise,(33,32,30))
    astronomical_cover()


if __name__=="__main__":
    main()
