from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

out = Path(__file__).resolve().parents[3] / "Assets/Transperceneige/Pass/Station/CottageEnamel.png"
image = Image.new("RGB", (1024, 512), "#253b37")
d = ImageDraw.Draw(image)
regular = "C:/Windows/Fonts/CAMBRIA.TTC"
bold = "C:/Windows/Fonts/CAMBRIAB.TTF"
ivory = "#eee5c9"
for y in (0, 256):
    d.rounded_rectangle((10,y+11,1013,y+244),radius=22,outline="#aa9974",width=4)
    d.rounded_rectangle((19,y+20,1004,y+235),radius=17,outline="#c6b892",width=2)
    for x in (37,986):
        for offset in (36,218):
            d.ellipse((x-4,y+offset-4,x+4,y+offset+4), fill="#a99c77")
d.text((512, 100), "KEEPER'S COTTAGE", anchor="mm", font=ImageFont.truetype(bold,90), fill=ivory)
d.line((256,166,768,166), fill="#aa9974", width=2)
d.text((512,198), "GREYWATER VIADUCT", anchor="mm", font=ImageFont.truetype(regular,38), fill=ivory)
d.text((512,391), "ENTRANCE", anchor="mm", font=ImageFont.truetype(bold,121), fill=ivory)
image.save(out, optimize=True)
