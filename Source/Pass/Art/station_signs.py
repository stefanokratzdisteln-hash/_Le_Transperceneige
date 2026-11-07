"""Bake original station enamel lettering for URP from locally available serif type."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[3]
LABELS = [
    ('FELLBRIDGE', 'cream', 86), ('WAITING ROOM', 'cream', 82),
    ('NORTH FELL RAILWAY', 'cream', 76), ('PARCELS', 'cream', 86),
    ('WAY OUT', 'cream', 82), ('LAST BOARDING', 'amber', 77),
    ('EMERGENCY EXIT', 'amber', 75), ('PARACHUTE', 'bag', 90),
]
canvas = Image.new('RGB',(1024,1024))
draw = ImageDraw.Draw(canvas)
for i,(label,style,size) in enumerate(LABELS):
    font = None
    for face in ('C:/Windows/Fonts/GARABD.TTF','/System/Library/Fonts/Supplemental/Georgia Bold.ttf',
                 '/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf'):
        if Path(face).exists():
            font = ImageFont.truetype(face,size)
            break
    if font is None:
        font = ImageFont.truetype('DejaVuSerif.ttf',size)
    back,fg = {'cream':('#304c44','#ede2bc'),'amber':('#6a382e','#f3d9a2'),
               'bag':('#dfcda3','#4c3b31')}[style]
    y=i*128
    draw.rectangle((0,y,1023,y+127),fill=back)
    draw.rounded_rectangle((8,y+7,1015,y+120),radius=7,outline=fg,width=3)
    draw.rectangle((16,y+13,1007,y+114),outline=fg,width=1)
    # Use measured bounding box; never wrap or clip printed station names.
    while draw.textlength(label,font=font)>930:
        font = font.font_variant(size=font.size-2)
    draw.text((512,y+63),label,font=font,fill=fg,anchor='mm',stroke_width=0)
    for x in (28,996):
        draw.ellipse((x-4,y+60,x+4,y+68),fill=fg)
dest=ROOT/'Assets/Transperceneige/Pass/Station/FellbridgeEnamel.png'
dest.parent.mkdir(parents=True,exist_ok=True)
canvas.save(dest,optimize=True)
