"""Paint the station house's letterpress timetable and railway enamel signs."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

OUT = Path(__file__).resolve().parents[3] / "Assets/Transperceneige/Pass/Station"


def face(size, bold=False):
    choices = (["C:/Windows/Fonts/GARABD.TTF", "DejaVuSerif-Bold.ttf"] if bold else
               ["C:/Windows/Fonts/GARA.TTF", "DejaVuSerif.ttf"])
    for name in choices:
        try:
            return ImageFont.truetype(name, size)
        except OSError:
            pass
    raise RuntimeError("A serif typeface is required for the station lettering")


def enamel():
    labels = ["FELLBRIDGE", "BOOKING HALL", "LUGGAGE OFFICE", "STATION MASTER",
              "TICKETS", "TO THE TRAINS", "TELEGRAPH", "NORTH FELL RAILWAY"]
    im = Image.new("RGB", (1024, 1024), "#193e36")
    draw = ImageDraw.Draw(im)
    for index, label in enumerate(labels):
        top = index * 128
        ink = "#f7e6b6"
        draw.rounded_rectangle((5, top + 5, 1018, top + 122), radius=8, outline=ink, width=3)
        draw.line((18, top + 14, 1005, top + 14), fill="#b99d68", width=1)
        font = face(86, True)
        while draw.textlength(label, font=font) > 934:
            font = font.font_variant(size=font.size-1)
        draw.text((512, top + 66), label, font=font, fill=ink, anchor="mm")
        for x in [22, 1001]:
            draw.ellipse((x-3, top+61, x+3, top+67), fill="#b99855")
    im.save(OUT / "StationHouseEnamel.png", optimize=True)


def timetable():
    im = Image.new("RGB", (640, 840), "#e2d8ba")
    d = ImageDraw.Draw(im)
    ink, red = "#263c39", "#763b2f"
    d.rectangle((8, 8, 631, 831), outline=ink, width=5)
    d.rectangle((18, 18, 621, 821), outline=ink, width=1)
    def title(y, text, size, colour=ink, bold=False):
        d.text((320,y),text,anchor="mm",font=face(size,bold),fill=colour)
    title(57,"NORTH  FELL  RAILWAY",24,bold=True)
    title(135,"FELLBRIDGE",73,bold=True)
    title(198,"Winter train service",34,red,True)
    d.line((43,239,597,239),fill=ink,width=3)
    d.text((53,259),"DEPARTURES",font=face(23,True),fill=ink)
    d.text((510,259),"TIME",font=face(23,True),fill=ink)
    for i,(place,clock) in enumerate([("Carlisle via Fellgate","6.20"),("Wether Fell","8.45"),
        ("London (Express)","11.10"),("Glenbank Junction","1.25"),("Carlisle","3.40"),
        ("Fellgate & North","5.50"),("London Night Express","9.15")]):
        y=331+i*43
        d.text((50,y),place,anchor="lm",font=face(27),fill=ink)
        d.text((583,y),clock,anchor="rm",font=face(26,True),fill=ink)
        d.line((50,y+21,583,y+21),fill="#a69f86")
    title(692,"Parcels received at the booking office.",23)
    title(737,"WINTER   1935 – 1936",29,red,True)
    title(790,"By order of the Station Master",19)
    im.save(OUT / "FellbridgeTimetable.png",optimize=True)


if __name__ == "__main__":
    enamel()
    timetable()
