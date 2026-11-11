"""Typeset the 21 actual Blender portraits with the names and prices used in play."""
import re
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[3]
PASS=ROOT/"Assets/Transperceneige/Pass"
GRAND=("nautilus_cup","falcon_dagger","gilt_ephemeris","touring_violin","mystery_clock")
STANDARD=("rose_egg","emerald_tiara","singing_bird_box","pocket_watch",
          "astronomical_globe","astrolabe","crystal_cup","silver_nef",
          "portrait_snuffbox","intaglio_seal","cameo","pearl_aigrette",
          "cloisonne_vase","jewelled_fan","diptych_dial","scent_flacon")
GOLD="#cba86e"; PAPER="#f2e6ca"; INK="#c9c2aa"


def font(name,size):
    fallback="DejaVuSerif.ttf" if name in ("gara.ttf","garait.ttf") else "NotoSerifCJK-Regular.ttc"
    for path in (Path("C:/Windows/Fonts")/name,Path(fallback)):
        try:return ImageFont.truetype(str(path),size)
        except OSError:pass
    raise RuntimeError("Missing typeface "+name)


def fit_label(draw,centre,y,text,typeface,max_size,width,colour):
    size=max_size
    while size>14 and draw.textlength(text,font=font(typeface,size))>width-10:size-=1
    draw.text((centre,y),text,font=font(typeface,size),fill=colour,anchor="mt")


def paint_row(plate,draw,keys,y,cell_height,names,prices,grand=False):
    cell=(plate.width-120)/len(keys)
    draw.line((60,y,plate.width-60,y),fill="#817554")
    portrait_height=203 if grand else 200
    for i,key in enumerate(keys):
        left=60+cell*i;centre=int(left+cell/2)
        if i:draw.line((left,y+10,left,y+cell_height-9),fill="#425350")
        with Image.open(PASS/"Resources/WinterbourneLootIcons"/(key+".png")) as original:
            icon=original.convert("RGBA")
        icon=icon.crop(icon.getchannel("A").getbbox())
        icon.thumbnail((int(cell)-39,portrait_height),Image.Resampling.LANCZOS)
        plate.paste(icon,(centre-icon.width//2,y+12+(portrait_height-icon.height)//2),icon)
        english,chinese=names[key]
        fit_label(draw,centre,y+228,chinese,"NotoSerifSC-VF.ttf",29,cell,PAPER)
        fit_label(draw,centre,y+272,english,"garait.ttf",24,cell,INK)
        fit_label(draw,centre,y+307,f"{prices[key]:,}","gara.ttf",26,cell,GOLD)


def make():
    table=(PASS/"Language/Resources/WinterbourneText.txt").read_text(encoding="utf-8-sig")
    names={key[5:]:(english,chinese) for key,english,chinese in
           (line.split("\t") for line in table.splitlines() if line.startswith("item."))}
    registry=(PASS/"PassLootCatalogue.cs").read_text(encoding="utf8")
    prices={key:int(value) for key,value in re.findall(r'\{\s*"(\w+)"\s*,\s*(\d+)\s*\}',registry)}
    if len(set(GRAND+STANDARD))!=21 or not all(key in prices and key in names for key in GRAND+STANDARD):
        raise ValueError("The bilingual register must match all 21 playable items.")
    width,height=1664,2128
    plate=Image.new("RGB",(width,height),(21,40,40))
    draw=ImageDraw.Draw(plate)
    draw.rectangle((24,24,width-25,height-25),outline="#7c7460",width=2)
    draw.text((62,47),"NORTH FELL RAILWAY",font=font("gara.ttf",33),fill=GOLD)
    draw.text((width-62,52),"WINTER  1935–36",font=font("gara.ttf",27),fill=GOLD,anchor="ra")
    draw.text((62,97),"随车珍藏",font=font("NotoSerifSC-VF.ttf",78),fill=PAPER)
    draw.text((width-61,143),"REGISTER OF VALUABLES",font=font("gara.ttf",52),fill=PAPER,anchor="ra")
    draw.text((62,215),"特等托运",font=font("NotoSerifSC-VF.ttf",27),fill=GOLD)
    draw.text((width-62,225),"REGISTERED TREASURES",font=font("gara.ttf",25),fill=GOLD,anchor="ra")
    paint_row(plate,draw,GRAND,263,342,names,prices,True)
    draw.text((62,621),"随车藏品",font=font("NotoSerifSC-VF.ttf",27),fill=GOLD)
    draw.text((width-62,632),"PASSENGER CONSIGNMENTS",font=font("gara.ttf",25),fill=GOLD,anchor="ra")
    for row in range(4):
        paint_row(plate,draw,STANDARD[row*4:row*4+4],675+row*339,339,names,prices)
    draw.line((60,2034,width-60,2034),fill="#817554")
    draw.text((62,2058),"游戏估值  /  APPRAISAL POINTS",font=font("NotoSerifSC-VF.ttf",21),fill=INK)
    draw.text((width-62,2063),"WINTERBOURNE EXPRESS",font=font("gara.ttf",22),fill=GOLD,anchor="ra")
    plate.save(ROOT/"Previews/Pass/AntiqueRegister.jpg",quality=94,optimize=True,subsampling=0)


if __name__=="__main__":make()
