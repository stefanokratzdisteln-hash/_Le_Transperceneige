"""Compose the North Fell formation plate from its nine rendered cutaways (Pillow)."""
import argparse
from functools import lru_cache
from math import cos, pi, sin
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont, ImageOps


ROOT = Path(__file__).resolve().parents[3]
PREVIEWS = ROOT / "Previews"
# Coupling pitches are measured in centimetres, so the running total is exact.
STOCK = (
    ("Pacific locomotive", "No. 601 North Star  ·  4–6–2 express engine", 1380),
    ("Corridor tender", "Coal, water and a through passage for crew", 840),
    ("Railway stores", "Linen press, lamp room and workshop", 1966),
    ("Compartment carriage", "24 seats  ·  Four six-seat compartments", 1966),
    ("Open saloon", "32 seats  ·  Open seating with a centre aisle", 1966),
    ("Restaurant carriage", "18 dining places  ·  Galley service", 1966),
    ("Winterbourne", "4 single berths  ·  Private sleeping compartments", 1966),
    ("Sleeping carriage", "16 berths  ·  Two-tier sleeping compartments", 1966),
    ("Guard’s brake van", "Guard’s office, luggage and parcels weighing", 1966),
)
PAPER, GREEN, INK = "#f1ecde", "#1e3734", "#242e2b"
GOLD, RULE, RUBY, GREY = "#c9a56a", "#b8ac8c", "#764337", "#48554f"
WIDTH, HEIGHT = 3000, 2230
MARGIN, GAP, COL = 90, 36, 916
PHOTO_H = 426


@lru_cache(maxsize=None)
def typeface(role, size):
    choices = {
        "display": ("C:/Windows/Fonts/BASKVILL.TTF", "/System/Library/Fonts/Supplemental/Baskerville.ttc",
                    "/usr/share/fonts/truetype/liberation2/LiberationSerif-Regular.ttf", "DejaVuSerif.ttf"),
        "roman": ("C:/Windows/Fonts/BELLB.TTF", "/System/Library/Fonts/Supplemental/Baskerville.ttc",
                  "/usr/share/fonts/truetype/liberation2/LiberationSerif-Bold.ttf", "DejaVuSerif-Bold.ttf"),
        "sans": ("C:/Windows/Fonts/GIL_____.TTF", "/System/Library/Fonts/Supplemental/GillSans.ttc",
                 "/usr/share/fonts/truetype/liberation2/LiberationSans-Regular.ttf", "DejaVuSans.ttf"),
    }
    for filename in choices[role]:
        try:
            return ImageFont.truetype(filename, size)
        except OSError:
            pass
    raise RuntimeError(f"No font available for {role}")


def tracked(draw, point, words, size, gap, colour, right=False):
    font = typeface("sans", size)
    widths = [font.getlength(c) for c in words]
    x, y = point
    if right:
        x -= sum(widths) + gap * (len(words) - 1)
    for c, width in zip(words, widths):
        draw.text((x, y), c, font=font, fill=colour, anchor="ls")
        x += width + gap


def insignia(draw, x, y):
    # The masthead wheel is an original device, engraved with eight fine spokes.
    for radius, weight in ((48,4),(40,2),(9,3)):
        draw.ellipse((x-radius,y-radius,x+radius,y+radius),outline=GOLD,width=weight)
    for i in range(8):
        a = i*pi/4
        draw.line((x+10*cos(a),y+10*sin(a),x+39*cos(a),y+39*sin(a)),fill=GOLD,width=3)
    for sign in (-1,1):
        for dy, length in ((-9,77),(0,68),(9,59)):
            draw.line((x+sign*49,y+dy,x+sign*length,y+dy), fill=GOLD, width=3)


def header(draw, total):
    draw.rectangle((0,0,WIDTH,243),fill=GREEN)
    draw.line((MARGIN,22,WIDTH-MARGIN,22),fill=GOLD,width=2)
    draw.line((MARGIN,229,WIDTH-MARGIN,229),fill=GOLD,width=2)
    insignia(draw,170,124)
    tracked(draw,(282,68),"NORTH FELL RAILWAY",29,6,GOLD)
    draw.text((276,159),"Rolling stock & formation",font=typeface("display",99),fill=PAPER,anchor="ls")
    draw.text((282,204),"One Pacific locomotive, one tender and seven carriages",
              font=typeface("sans",32),fill=PAPER,anchor="ls")
    draw.line((2271,54,2271,202),fill=GOLD,width=2)
    tracked(draw,(WIDTH-MARGIN,80),"COUPLED LENGTH",25,4.1,GOLD,right=True)
    draw.text((WIDTH-MARGIN,170),f"{total/100:.2f} m",font=typeface("display",87),
              fill=PAPER,anchor="rs")


def tile(image, draw, number, name, caption, pitch):
    x = MARGIN + ((number-1)%3)*(COL+GAP)
    y = 318 + ((number-1)//3)*560
    draw.text((x,y+36),f"{number:02}",font=typeface("display",52),fill=RUBY,anchor="ls")
    draw.line((x+79,y+1,x+79,y+40),fill=RULE,width=2)
    draw.text((x+101,y+36),name,font=typeface("roman",48),fill=INK,anchor="ls")
    path = (PREVIEWS/"Car07_Cutaway.png" if number==7
            else PREVIEWS/"Formation"/f"Car{number:02}_Cutaway.png")
    with Image.open(path) as source:
        # Keep both couplers in frame. On the high tender, preserve the top rim.
        crop = ImageOps.fit(source.convert("RGB"), (COL, PHOTO_H), method=Image.Resampling.LANCZOS,
                            centering=(.5, .25 if number==2 else .50))
    top = y + 56
    image.paste(crop,(x,top))
    draw.rectangle((x,top,x+COL-1,top+PHOTO_H-1),outline=RULE,width=2)
    draw.text((x,top+465),caption,font=typeface("sans",30),fill=INK,anchor="ls")
    draw.text((x+COL,top+465),f"{pitch/100:.2f} m",font=typeface("sans",28),
              fill=GREY,anchor="rs")


def running_order(draw, total):
    y=2018
    draw.line((MARGIN,y,WIDTH-MARGIN,y),fill=GREEN,width=3)
    tracked(draw,(MARGIN,y+44),"COUPLED FORMATION",27,3.6,GREEN)
    draw.text((WIDTH-MARGIN,y+44),"Coupling pitches drawn to scale",
              font=typeface("sans",28),fill=GREY,anchor="rs")
    span=WIDTH-2*MARGIN
    draw.line((MARGIN,y+101,WIDTH-MARGIN,y+101),fill=GOLD,width=7)
    pos=0
    for i,(_,_,pitch) in enumerate(STOCK,1):
        a=MARGIN+round(span*pos/total)
        pos+=pitch
        b=MARGIN+round(span*pos/total)
        colour=GREEN if i<=2 else RUBY
        draw.rectangle((a+5,y+75,b-5,y+126),fill=colour)
        draw.text(((a+b)/2,y+100),f"{i:02}",font=typeface("sans",32),fill=PAPER,anchor="mm")
    tracked(draw,(MARGIN,y+174),"HEAD",22,2.9,GREY)
    tracked(draw,(WIDTH-MARGIN,y+174),"TAIL",22,2.9,GREY,right=True)


def compose(output):
    image=Image.new("RGB",(WIDTH,HEIGHT),PAPER)
    draw=ImageDraw.Draw(image)
    total=sum(pitch for _,_,pitch in STOCK)
    header(draw,total)
    tracked(draw,(MARGIN,287),"VEHICLES IN RUNNING ORDER",25,3.6,GREEN)
    tracked(draw,(WIDTH-MARGIN,287),"CUTAWAYS AT INDIVIDUAL SCALES",22,2.7,GREY,right=True)
    for i,entry in enumerate(STOCK,1):
        tile(image,draw,i,*entry)
    running_order(draw,total)
    output.parent.mkdir(parents=True,exist_ok=True)
    temporary=output.with_name(output.name+".tmp")
    image.save(temporary,format="PNG",compress_level=7,dpi=(240,240))
    temporary.replace(output)


if __name__=="__main__":
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output",type=Path,default=PREVIEWS/"Formation"/"Formation.png")
    compose(parser.parse_args().output)
