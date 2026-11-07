"""Original paper, enamel and ceramic artwork for the Fellbridge winter 1935 fittings."""
import math
from pathlib import Path
from random import Random

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "Assets/Transperceneige/Pass/Station/Heritage/Stationery.png"
REGIONS = {
    "ledger": (16, 16, 1216, 826), "timetable": (1232, 16, 2032, 826),
    "beam": (16, 842, 2032, 982), "tile": (16, 1000, 528, 1512),
    "press": (548, 1004, 1392, 1240), "scale": (548, 1256, 1392, 1492),
    "ticket": (1408, 1000, 2032, 1418), "filing": (16, 1530, 1216, 1820),
    "fire": (1232, 1530, 2032, 1810), "desk": (16, 1836, 1080, 2032),
}


def make():
    from PIL import Image, ImageDraw, ImageFont
    atlas = Image.new("RGB", (2048, 2048), "#c8c1a5")
    rng = Random(19351216)
    ink, warm, red = "#293336", "#eee0b8", "#83524b"

    def font(n, face="GARA.TTF"):
        options = [Path("C:/Windows/Fonts") / face, Path("DejaVuSerif.ttf")]
        for path in options:
            try:
                return ImageFont.truetype(str(path), n)
            except OSError:
                pass
        raise RuntimeError("A serif font is required")

    def sheet(key, colour):
        x1, y1, x2, y2 = REGIONS[key]
        return Image.new("RGB", (x2 - x1, y2 - y1), colour)

    def finish(key, image):
        tile = image.copy()
        # Small variations in printed paper stock; no photographs are reproduced.
        pixels = tile.load()
        for _ in range(tile.width * tile.height // 7):
            x, y = rng.randrange(tile.width), rng.randrange(tile.height)
            v = rng.randint(-9, 6)
            pixels[x, y] = tuple(max(0, min(255, c + v)) for c in pixels[x, y])
        atlas.paste(tile, REGIONS[key][:2])

    def centred(d, x, y, t, n, c=ink, face="GARA.TTF"):
        d.text((x, y), t, fill=c, font=font(n, face), anchor="mm", stroke_width=0)

    im = sheet("ledger", "#e6dabe"); d = ImageDraw.Draw(im)
    for x in range(584, 621):
        n = abs(x - 602) / 19
        d.line((x, 1, x, 807), fill=(int(181+46*n), int(164+51*n), int(135+49*n)))
    for p, heading in enumerate(["NIGHT DUTY REGISTER", "TRAINS  &  MESSAGES"]):
        x = p * 600
        centred(d,x+300,51,"NORTH FELL RAILWAY",26)
        centred(d,x+300,109,heading,40,ink,"GARABD.TTF")
        centred(d,x+300,161,"FELLBRIDGE   •   DECEMBER 1935",22,red)
        for col in (24, 90, 430, 575):
            d.line((x+col,206,x+col,747),fill="#a87e78",width=2)
        for j in range(12):
            y=208+j*47
            d.line((x+25,y,x+574,y),fill="#97a3a4",width=2)
        centred(d,x+55,227,"TIME",15); centred(d,x+257,227,"PARTICULARS",19)
        centred(d,x+498,227,"INITIALS",18)
        notes = (["Took over from Mr Hall","Parcels at platform door","Lamp oil in store","Mail bags ready","Await the down express"]
                 if p==0 else ["Down line reported clear","Fellgate advised","Carlisle bag received","Guard has signed"])
        for i,row in enumerate(notes):
            y=268+i*47
            centred(d,x+56,y,f"9.{5+i*6:02}",18)
            d.text((x+103,y),row,font=font(25,"segoesc.ttf"),fill="#374050",anchor="lm")
            centred(d,x+492,y,"J. W.",24,"#374050","segoesc.ttf")
        centred(d,x+300,777,str(118+p),18,red)
    finish("ledger",im)
    im=sheet("timetable",warm); d=ImageDraw.Draw(im)
    for gap in [10,18]: d.rectangle((gap,gap,im.width-gap-1,im.height-gap-1),outline=ink,width=3)
    centred(d,400,87,"NORTH  FELL  RAILWAY",45)
    centred(d,400,180,"WORKING",77,ink,"GARABD.TTF")
    centred(d,400,263,"TIME TABLE",78,ink,"GARABD.TTF")
    d.line((100,334,700,334),fill=red,width=4)
    centred(d,400,390,"NORTHERN DISTRICT",39)
    centred(d,400,454,"Fellbridge — Carlisle",43,ink,"GARAIT.TTF")
    centred(d,400,548,"WINTER  1935 – 1936",37,red)
    centred(d,400,666,"For the use of the Company's servants",24)
    centred(d,400,738,"TRAFFIC SUPERINTENDENT'S OFFICE",19)
    finish("timetable",im)
    im=sheet("beam","#bdac77");d=ImageDraw.Draw(im)
    d.rectangle((4,3,im.width-5,im.height-5),outline="#493d27",width=3)
    for i in range(113):
        x=54+int((im.width-111)*i/112)
        long=i%8==0
        d.line((x,5,x,57 if long else 33 if i%2==0 else 20),fill=ink,width=3 if long else 2)
        if long: centred(d,x,91,str(i),39)
    finish("beam",im)
    im=sheet("tile","#e0d5ad");d=ImageDraw.Draw(im)
    for k in [3,13,28]: d.rectangle((k,k,511-k,511-k),outline="#687b67",width=4)
    for turn in range(4):
        m=Image.new('RGBA',(512,512)); md=ImageDraw.Draw(m)
        md.arc((26,103,255,353),170,305,fill="#536e66",width=9)
        md.polygon([(78,192),(73,136),(100,158),(110,188)],fill="#778a6a")
        md.polygon([(107,157),(135,107),(135,142),(116,164)],fill="#56736a")
        for i in range(6):
            a=i*math.pi/3
            x=98+23*math.cos(a);y=95+23*math.sin(a)
            md.ellipse((x-18,y-14,x+18,y+14),fill="#8c5a46")
        md.ellipse((88,85,109,106),fill="#c7a565")
        m=m.rotate(turn*90)
        im.paste(m,(0,0),m)
    d=ImageDraw.Draw(im);d.polygon([(256,172),(340,256),(256,340),(172,256)],outline="#546b63",width=8)
    d.ellipse((232,232,280,280),fill="#736b4a")
    finish("tile",im)
    captions={"press":("NORTH FELL  RY.","DATE PRESS    No 17"),
              "scale":("NORTH FELL RAILWAY","PARCELS   •   POUNDS & QUARTERS"),
              "filing":("FELLBRIDGE","TRAFFIC  DEPARTMENT"),
              "fire":("N  ·  F  ·  R","FOUNDRY PATTERN  48"),"desk":("FELLBRIDGE STATION","DISTRICT TELEGRAPH  No 6")}
    for key,(title,small) in captions.items():
        im=sheet(key,"#303e37");d=ImageDraw.Draw(im);w,h=im.size
        for n in (6,15): d.rectangle((n,n,w-n-1,h-n-1),outline="#cab789",width=3)
        centred(d,w*.5,h*.39,title,int(h*.29),"#e1ca95","GARABD.TTF")
        centred(d,w*.5,h*.71,small,int(h*.14),"#ddd1aa")
        finish(key,im)
    im=sheet("ticket","#e6ca94");d=ImageDraw.Draw(im);w,h=im.size
    d.rectangle((7,5,w-9,h-6),outline=ink,width=4)
    centred(d,w/2,55,"NORTH FELL RAILWAY",41)
    centred(d,w/2,140,"FELLBRIDGE",67,ink,"GARABD.TTF");centred(d,w/2,191,"to",28)
    centred(d,w/2,250,"CARLISLE",66,ink,"GARABD.TTF")
    centred(d,193,350,"THIRD  SINGLE",31);centred(d,483,352,"03257",41,red)
    finish("ticket",im)
    OUT.parent.mkdir(parents=True, exist_ok=True)
    atlas.save(OUT,optimize=True)


if __name__ == "__main__":
    make()
