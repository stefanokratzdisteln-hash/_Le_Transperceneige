"""Original stationer's stock for the winter 1935-36 Fellbridge station rooms.

Cells are drawn at the aspect ratio of the actual sheet or plate. They are packed
into equal UV rectangles, then returned to their printed proportions in Unity.
"""
from pathlib import Path
import math
from PIL import Image, ImageDraw, ImageFont
import numpy as np

DEST = Path(__file__).resolve().parents[3] / "Assets/Transperceneige/Pass/Station/NightDutyPrints.png"
INK, RED, GREEN, STOCK = "#272e29", "#822d2c", "#26483f", "#ddd2b4"
BORD = "#b5a17d"


def typeface(size, weight=False, italic=False):
    name = "GARAIT" if italic else "GARABD" if weight else "GARA"
    fallback = "DejaVuSerif-Bold.ttf" if weight else "DejaVuSerif.ttf"
    try:
        return ImageFont.truetype("C:/Windows/Fonts/" + name + ".TTF", size)
    except OSError:
        return ImageFont.truetype(fallback, size)


def sheet(w, h, color=STOCK):
    im = Image.new("RGB", (w, h), color)
    d = ImageDraw.Draw(im)
    d.rectangle((3, 3, w-4, h-4), outline=BORD, width=2)
    return im, d


def mid(d, x, y, text, size, color=INK, bold=False, italic=False):
    d.text((x,y), text, font=typeface(size, bold, italic), fill=color, anchor="mm")


def title(d, w, text):
    mid(d, w/2, 34, "NORTH   FELL   RAILWAY", 24, GREEN, True)
    d.line((25, 62, w-25, 62), fill=GREEN, width=3)
    mid(d, w/2, 96, text, 38, INK, True)


def ticket():
    w,h=570,300
    im,d=sheet(w,h,"#cfb18c")
    d.rectangle((14,13,w-15,h-14),outline=INK,width=2)
    mid(d,285,36,"NORTH  FELL  RAILWAY",23,INK,True)
    mid(d,285,89,"FELLBRIDGE",53,INK,True)
    mid(d,285,132,"TO",22)
    mid(d,285,173,"CARLISLE",48,INK,True)
    d.line((26,210,w-26,210),fill=INK,width=2)
    mid(d,150,233,"THIRD   •   SINGLE",21)
    mid(d,414,234,"No.  04186",23,RED,True)
    mid(d,285,267,"18  DEC  1935",19,INK,True)
    return im


def ledger():
    w,h=650,490
    im,d=sheet(w,h)
    title(d,w,"DAILY ISSUE BOOK")
    for x in (30,112,337,474,622): d.line((x,160,x,438),fill="#a2847b",width=2)
    for i in range(9): d.line((30,159+i*35,622,159+i*35),fill="#859693",width=2)
    for x,t in [(69,"Train"),(225,"Destination"),(407,"First No."),(548,"Last No.")]: mid(d,x,142,t,21)
    for i,(train,place,first,last) in enumerate([("6.20","Carlisle","04160","04169"),("8.45","Wether Fell","01104","01120"),("3.40","Carlisle","04170","04185"),("9.15","London","02515","02517")]):
        for x,v in zip((69,225,407,548),(train,place,first,last)): mid(d,x,179+i*35,v,22,"#444948",italic=True)
    mid(d,325,462,"Fellbridge    •    Booking Clerk’s Return",20,RED)
    return im


def plate(t):
    im=Image.new("RGB",(650,122),"#242c27"); d=ImageDraw.Draw(im)
    d.rounded_rectangle((6,5,643,117),radius=6,outline="#c2a579",width=4)
    mid(d,325,60,t,42,"#eee3c8",True)
    return im


def tag():
    im,d=sheet(510,278,"#ccae84")
    mid(d,255,34,"NORTH FELL RAILWAY",23,INK,True)
    d.line((25,58,485,58),fill=INK,width=2)
    mid(d,255,112,"CARLISLE",56,INK,True)
    mid(d,255,170,"VIA   FELLS   •   No. 16",23)
    mid(d,255,228,"Passengers’ Luggage",25,RED,italic=True)
    return im


def parcelsbill():
    im,d=sheet(600,436)
    title(d,600,"PARCEL WAY-BILL")
    for i,(a,b) in enumerate([("From","Fellbridge"),("Consignee","Glenbank Station"),("Description","One case of books"),("Weight","24 lb"),("Date","18 December 1935")]):
        y=153+i*47
        d.text((37,y-9),a,font=typeface(23,True),fill=RED)
        d.text((224,y-10),b,font=typeface(26,italic=True),fill=INK)
        d.line((35,y+26,565,y+26),fill=BORD,width=2)
    return im


def route():
    im,d=sheet(550,675)
    title(d,550,"THE NORTH FELL LINE")
    points=[(117,529),(171,464),(231,422),(186,342),(261,291),(299,225),(370,161)]
    d.line(points,fill=RED,width=7)
    for i,((x,y),name) in enumerate(zip(points,["London","Fellgate","Wether Fell","Glenbank","Fellbridge","High Fell","Carlisle"])):
        d.ellipse((x-7,y-7,x+7,y+7),fill=STOCK,outline=RED,width=3)
        d.text((x+13,y-10),name,fill=INK,font=typeface(21,i==4))
    mid(d,275,590,"Principal passenger stations",21,italic=True)
    d.line((30,621,520,621),fill=GREEN,width=2)
    mid(d,275,644,"Printed for the Company, 1935",15,GREEN)
    return im


def notice(first,second,body,footer):
    im,d=sheet(570,720);d.rectangle((15,15,554,705),outline=GREEN,width=4)
    mid(d,285,69,"NORTH  FELL  RAILWAY",29,GREEN,True)
    d.line((40,123,529,123),fill=RED,width=4)
    mid(d,285,202,first,59,INK,True);mid(d,285,271,second,45,INK,True)
    for i,line in enumerate(body):mid(d,285,367+i*51,line,25)
    d.line((98,568,472,568),fill=RED,width=2)
    mid(d,285,626,footer,26,RED,italic=True)
    return im


def clockface():
    im,d=sheet(580,580,"#dfd9c5"); o=290
    for i in range(60):
        a=2*math.pi*i/60
        end=271;start=248 if i%5==0 else 261
        d.line([(o+math.sin(a)*r,o-math.cos(a)*r) for r in (start,end)],fill=INK,width=6 if i%5==0 else 3)
    for i in range(1,13):
        a=2*math.pi*i/12
        mid(d,o+math.sin(a)*214,o-math.cos(a)*214,str(i),56,INK,True)
    mid(d,o,225,"N. F. R.",26,GREEN,True)
    mid(d,o,356,"FELLBRIDGE",17)
    return im


def limewash():
    # Fine aggregate under hand-brushed distemper. The low contrast keeps the wall
    # legible in grazing lamplight without high-frequency shimmer.
    rng = np.random.default_rng(1935)
    size = 512
    def grain(cells):
        values = rng.normal(128, 28, (cells,cells)).clip(0,255).astype("uint8")
        tiled = np.tile(values,(3,3))
        large = Image.fromarray(tiled).resize((size*3,size*3),Image.Resampling.BICUBIC)
        return np.asarray(large)[size:2*size,size:2*size].astype(float)-128
    shade = grain(9)*.19 + grain(40)*.10 + grain(100)*.08 + rng.normal(0,1.7,(size,size))
    rgb = np.array([238,235,229])[None,None,:]+shade[:,:,None]
    Image.fromarray(rgb.clip(0,255).astype("uint8")).save(DEST.with_name("DistemperedPlaster.png"),optimize=True)


def main():
    images=[ticket(),ledger(),plate("CARLISLE"),plate("LONDON"),plate("FELLGATE"),plate("WETHER FELL"),
            tag(),parcelsbill(),plate("PAID  •  PARCELS"),route(),
            notice("STATION","DUTIES",["Goods and parcels","Close the daily returns","before the night mail."],"By Order"),
            notice("WAITING","ROOM",["Passengers are requested","to show their tickets","at the barrier."],"Winter Service, 1935"),
            clockface(),notice("PARCELS","BY RAIL",["Please address all parcels","and tie every label","before despatch."],"Booking Office"),
            plate("OUTWARD"),plate("INWARD")]
    atlas=Image.new("RGB",(2048,2048))
    for i,im in enumerate(images):
        cell=im.resize((512,512),Image.Resampling.LANCZOS)
        atlas.paste(cell,((i%4)*512,(i//4)*512))
    atlas.save(DEST,optimize=True)
    limewash()


if __name__=="__main__":
    main()
