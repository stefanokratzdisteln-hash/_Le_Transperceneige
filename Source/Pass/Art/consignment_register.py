"""Print an inventory of the actual Blender portraits and the two game string/price tables."""
import re
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT=Path(__file__).resolve().parents[3]
PASS=ROOT/"Assets/Transperceneige/Pass"
rows=[line.split("\t") for line in (PASS/"Language/Resources/WinterbourneText.txt").read_text(encoding="utf-8-sig").splitlines()]
names={key[5:]:(en,zh) for key,en,zh in rows if key.startswith("item.")}
prices={key:int(value) for key,value in re.findall(r'\{"(\w+)",(\d+)\}',(PASS/"PassLootCatalogue.cs").read_text())}
order=("rose_egg","emerald_tiara","singing_bird_box","pocket_watch",
       "astronomical_globe","astrolabe","crystal_cup","silver_nef",
       "portrait_snuffbox","intaglio_seal","cameo","pearl_aigrette",
       "cloisonne_vase","jewelled_fan","diptych_dial","scent_flacon")


def font(name,size):
    fallback="DejaVuSerif.ttf" if name in ("gara.ttf","garait.ttf") else "NotoSerifCJK-Regular.ttc"
    for path in (Path("C:/Windows/Fonts")/name,Path(fallback)):
        try:return ImageFont.truetype(str(path),size)
        except OSError: pass
    raise RuntimeError("Missing typeface "+name)


def make():
    width,height=1664,1690
    plate=Image.new("RGB",(width,height),(21,40,40))
    ink=ImageDraw.Draw(plate)
    gold="#cba86e";paper="#f2e6ca";secondary="#c9c2aa"
    ink.rectangle((24,24,width-25,height-25),outline="#7c7460",width=2)
    ink.text((62,47),"NORTH FELL RAILWAY",font=font("gara.ttf",33),fill=gold)
    ink.text((width-62,52),"WINTER  1935–36",font=font("gara.ttf",27),fill=gold,anchor="ra")
    ink.text((62,97),"随车珍藏",font=font("NotoSerifSC-VF.ttf",78),fill=paper)
    ink.text((width-61,143),"REGISTER OF VALUABLES",font=font("gara.ttf",52),fill=paper,anchor="ra")
    x0,y0,cw,ch=60,226,386,340
    for i,key in enumerate(order):
        col,row=i%4,i//4;x=x0+col*cw;y=y0+row*ch
        if col:ink.line((x-1,y+10,x-1,y+ch-9),fill="#425350")
        if col==0:ink.line((60,y,width-60,y),fill="#817554")
        icon=Image.open(PASS/"Resources/WinterbourneLootIcons"/(key+".png")).convert("RGBA")
        icon=icon.crop(icon.getchannel("A").getbbox());icon.thumbnail((298,204),Image.Resampling.LANCZOS)
        plate.paste(icon,(x+(cw-icon.width)//2,y+17+(204-icon.height)//2),icon)
        en,zh=names[key]
        ink.text((x+cw//2,y+222),zh,font=font("NotoSerifSC-VF.ttf",29),fill=paper,anchor="mt")
        ink.text((x+cw//2,y+268),en,font=font("garait.ttf",23),fill=secondary,anchor="mt")
        ink.text((x+cw//2,y+305),f"{prices[key]:,}",font=font("gara.ttf",26),fill=gold,anchor="mt")
    ink.line((60,1592,width-60,1592),fill="#817554")
    ink.text((62,1616),"游戏估值  /  APPRAISAL POINTS",font=font("NotoSerifSC-VF.ttf",21),fill=secondary)
    ink.text((width-62,1621),"WINTERBOURNE EXPRESS",font=font("gara.ttf",22),fill=gold,anchor="ra")
    plate.save(ROOT/"Previews/Pass/AntiqueRegister.jpg",quality=94,optimize=True,subsampling=0)


if __name__=="__main__": make()
