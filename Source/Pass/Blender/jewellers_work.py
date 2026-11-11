"""Ateliers of Geneva and St Petersburg. Original miniature working parts in metres."""
from math import cos,sin,pi,tau,atan2,sqrt
from pathlib import Path
import re
import bpy
from mathutils import Vector,Matrix
import station_heritage as s

GOLD="Cabinet chased gold"
DARK="Cabinet antique gold"
BLUE="Geneva flinque enamel"
ROSE="Mulberry flinque enamel"
GEM="Cabinet rose cut diamond"
GARNET="Cabinet garnet"
FEATHER="Kingfisher cerulean"
BREAST="Kingfisher rust"
VELVET="Cabinet velvet"
PAINT="Lake enamel miniature"
NIGHT="Greywater enamel miniature"

def write_texture_meta(path,label,normal=False,linear=False):
    template=s.STATION/"StationHouseEnamel.png.meta"
    s.copied_meta(template,path.with_suffix(".png.meta"),"cabinet texture "+label)
    meta=path.with_suffix(".png.meta")
    text=meta.read_text()
    if linear or normal:text=re.sub(r'sRGBTexture: 1','sRGBTexture: 0',text)
    if normal:text=text.replace("  textureType: 0","  textureType: 1")
    text=text.replace("aniso: 1","aniso: 4").replace("textureCompression: 1","textureCompression: 0")
    meta.write_text(text,encoding="utf8")

def palette():
    old=s.OUT;opt=old/"Optics";opt.mkdir(parents=True,exist_ok=True);s.OUT=opt
    s.copied_meta(s.STATION.with_suffix(".meta"),opt.with_suffix(".meta"),"cabinet finishes")
    try:
        for name,tint,metal,rough,pattern,coat in [
                (GOLD,"cc9a50",.95,.23,"Burnished vermeil",0),(DARK,"83663b",.88,.35,None,0),
                (BLUE,"173745",.10,.22,"Geneva blue",.89),(ROSE,"823646",.10,.22,"Imperial mulberry",.89),
                (GEM,"d3e3e2",.16,.055,None,.9),(GARNET,"7c1634",.15,.085,None,.82),
                (FEATHER,"145d71",.24,.33,None,.36),(BREAST,"aa6434",.14,.40,None,.20),
                (VELVET,"4c2636",.03,.83,None,0),(PAINT,"ffffff",.06,.29,"miniature",.8),
                (NIGHT,"ffffff",.06,.29,"night",.8)]:
            dest=s.finish_material(name,"ffffff" if pattern else tint,metal,rough)
            data=dest.read_text()
            rules={}
            if coat:
                data=data.replace("933532a4fcc9baf4fa0491de14d08ed7","ee7e4c9a5f6364b688a332c67fc32cca")
                data=data.replace("- _ClearCoatMask: 0",f"- _ClearCoat: 1\n    - _ClearCoatMask: {coat}")
                data=data.replace("- _ClearCoatSmoothness: 0","- _ClearCoatSmoothness: 0.92")
            if pattern:
                rules={"_BaseMap": ("Greywater at night.png" if pattern=="night" else "Lake at dusk.png") if pattern in ("miniature","night") else pattern+" body.png"}
                if pattern not in ("miniature","night"):
                    rules.update(_BumpMap=pattern+" relief.png",_MetallicGlossMap=pattern+" polish.png")
                    data=re.sub(r"(- _BumpScale: ).*",r"\g<1>"+(".20" if name==GOLD else ".38"),data)
                    data=re.sub(r"(- _Smoothness: ).*",r"\g<1>1",data)
                for prop,filename in rules.items():
                    tex=opt/filename
                    write_texture_meta(tex,filename,prop=="_BumpMap",prop=="_MetallicGlossMap")
                    guid=s.read_guid(tex.with_suffix(".png.meta"))
                    data=re.sub(r"(- "+prop+r":\n\s+m_Texture: )\{[^}]*\}",r"\g<1>{fileID: 2800000, guid: "+guid+", type: 3}",data)
            keywords=(["_CLEARCOAT"] if coat else [])+(["_METALLICSPECGLOSSMAP","_NORMALMAP"] if len(rules)>1 else [])
            if keywords:data=data.replace("m_ValidKeywords: []","m_ValidKeywords:\n"+"\n".join("  - "+key for key in sorted(keywords)))
            dest.write_text(data,encoding="utf8")
            s.mat(name,tint,metal,rough,dest,opt/rules["_BaseMap"] if rules else None)
            mat=s.materials[name];node=mat.node_tree.nodes.get("Principled BSDF");node.inputs["Coat Weight"].default_value=coat
            node.inputs["Coat Roughness"].default_value=.08
            if len(rules)>1:
                tex=mat.node_tree.nodes.new("ShaderNodeTexImage")
                image=bpy.data.images.load(str(opt/rules["_BumpMap"]),check_existing=True)
                image.colorspace_settings.name="Non-Color";image.pack();tex.image=image
                bump=mat.node_tree.nodes.new("ShaderNodeNormalMap");bump.inputs["Strength"].default_value=.36
                mat.node_tree.links.new(tex.outputs["Color"],bump.inputs["Color"]);mat.node_tree.links.new(bump.outputs["Normal"],node.inputs["Normal"])
            if name in (GEM,GARNET):
                node.inputs["IOR"].default_value=2.1 if name==GEM else 1.75
                node.inputs["Transmission Weight"].default_value=.6
    finally:s.OUT=old

def bezier(m,a,b,c,d,r,n=26):
    a,b,c,d=map(Vector,(a,b,c,d))
    s.tube(m,[a*(1-t)**3+b*(3*(1-t)**2*t)+c*(3*(1-t)*t*t)+d*t**3 for t in (i/n for i in range(n+1))],r,8)

def ring(m,p,rx,ry,r,plane='xy',n=72):
    v=Vector(p)
    s.tube(m,[v+Vector((rx*cos(i*tau/n),ry*sin(i*tau/n) if plane=="xy" else 0,ry*sin(i*tau/n) if plane!="xy" else 0)) for i in range(n+1)],r,8)

def pearl(p,r,m="Pearl"):
    s.ellipsoid(m,p,(r,r,r),12)

def plate(m,points,uv=None):
    s.face(points,m,False,uv or [(0,0),(1,0),(1,1),(0,1)])

def ovalplate(m,c,u,v,n=72):
    c,u,v=map(Vector,(c,u,v))
    for i in range(n):
        a,b=i*tau/n,(i+1)*tau/n
        s.face([c,c+u*cos(a)+v*sin(a),c+u*cos(b)+v*sin(b)],m,False,
               [(0.5,.5),(.5+.496*cos(a),.5+.496*sin(a)),(.5+.496*cos(b),.5+.496*sin(b))])

def rosecut(p,r,material=GEM):
    x,y,z=p;n=10
    def layer(rr,dz,offset):
        return [Vector((x+rr*cos((i+offset)*tau/n),y+rr*sin((i+offset)*tau/n),z+dz)) for i in range(n)]
    pavilion=Vector((x,y,z-r*.32));crown=Vector((x-r*.02,y+r*.02,z+.79*r))
    foot,breaks,upper=layer(r,0,0),layer(r*.74,r*.39,.5),layer(r*.38,r*.66,0)
    for i in range(n):
        j=(i+1)%n
        s.face([pavilion,foot[j],foot[i]],material)
        for lo,hi in ((foot,breaks),(breaks,upper)):
            s.face([lo[i],lo[j],hi[i]],material);s.face([lo[j],hi[j],hi[i]],material)
        s.face([upper[i],upper[j],crown],material)
    ring(DARK,(x,y,z-r*.06),r*1.03,r*1.03,max(.00026,r*.11),n=40)
    for i in range(4):
        t=(i+.5)*tau/4;pearl((x+r*.94*cos(t),y+r*.94*sin(t),z+r*.13),r*.12,GOLD)

def feather(a,b,width,camber,material):
    a,b=map(Vector,(a,b));axis=b-a
    perp=Vector((-axis.y,axis.x,0)).normalized()
    last=None
    for j in range(9):
        t=j/8;w=width*(.025+sin(pi*t)**.74)
        mid=a+axis*t+Vector((0,0,camber*sin(pi*t)))
        row=[mid-perp*w,mid+Vector((0,0,.00049)),mid+perp*w]
        if last:
            for q in (0,1):s.face([last[q],row[q],row[q+1],last[q+1]],material,True)
        last=row
    s.tube(DARK,[a,a+axis*.48+Vector((0,0,camber)),b],.00015,6)

def gallery():
    s.part("Guilloche bombe case, beaded rails and winding slide")
    s.box(DARK,(0,0,.019),(.125,.077,.038),.006)
    for z,w in ((.003,.126),(.007,.124),(.032,.124),(.038,.126)):
        s.box(GOLD,(0,0,z),(w,.078,.0023),.0045)
    # Thin, translucent panes; engravings follow separate UV islands.
    for side in (-1,1):
        y=side*.039
        plate(BLUE,[(-.054,y,.008),(.054,y,.008),(.054,y,.031),(-.054,y,.031)][::(-side)])
        x=side*.0626
        plate(BLUE,[(x,-.030,.008),(x,.030,.008),(x,.030,.031),(x,-.030,.031)][::side])
        for h in (-1,1):
            s.box(GOLD,(h*.0577,y*.975,.020),(.0027,.002,.026),.0007)
    for side in (-1,1):
        for j in range(3):
            x=side*(.009+j*.015)
            bezier(GOLD,(x,-.0398,.010),(x+side*.011,-.04,.012),(x+side*.014,-.04,.025),(x+side*.002,-.04,.029),.00050)
            bezier(GOLD,(x+side*.010,-.04,.021),(x+side*.002,-.04,.027),(x-side*.004,-.04,.021),(x+side*.002,-.04,.017),.00036)
        for j in range(11):
            pearl((side*(.003+j*.0051),-.0397,.0064),.00075,GOLD)
    for x in (-.045,-.024,.024,.045):
        for j in range(4):
            a=(j+.5)*pi/2
            bud=(x+.0024*cos(a),-.0402,.014+.0024*sin(a))
            s.ellipsoid(GARNET,bud,(.0015,.0008,.0017),12)
        pearl((x,-.0412,.014),.0010,"Pearl")
    s.box(DARK,(0,-.0396,.0197),(.018,.0025,.0066),.002)
    s.box(GOLD,(.004,-.041,.0197),(.010,.0024,.005),.0014)
    s.box(BLUE,(0,0,.0386),(.119,.070,.003),.005)
    plate(BLUE,[(-.054,-.031,.0402),(.054,-.031,.0402),(.054,.031,.0402),(-.054,.031,.0402)])
    for a in (-1,1):
        for b in (-1,1):
            rosecut((a*.048,b*.025,.0414),.0029)
            bezier(GOLD,(a*.052,b*.024,.0417),(a*.055,b*.005,.0419),(a*.037,b*.009,.0419),(a*.035,b*.001,.0419),.00055)
    s.part("Pierced musical grille and three-knuckle oval hinge")
    ovalplate(VELVET,(0,-.002,.0407),(.0315,0,0),(0,.024,0))
    for scale in (1,1.08):
        ring(GOLD,(0,-.002,.0416),.0315*scale,.024*scale,.0011)
    for i in range(12):
        t=i*tau/12
        def pt(a,rr,zz):return (.031*cos(a)*rr,-.002+.024*sin(a)*rr,zz)
        bezier(GOLD,pt(t,.18,.0423),pt(t+.42,.42,.0426),pt(t+.62,.67,.0425),pt(t+.10,.98,.0421),.0008,16)
    for k in (-1,0,1):
        s.rod(GOLD,(k*.008-.0034,.0238,.0434),(k*.008+.0034,.0238,.0434),.0023,sides=18)
    s.rod(DARK,(-.013,.0238,.0434),(.013,.0238,.0434),.0008)
    s.part("Painted lake miniature set inside the raised bird cover")
    c=Vector((0,.029,.065));u=Vector((.032,0,0));v=Vector((0,.0054,.0225))
    ovalplate(PAINT,c,u*.92,v*.90)
    for scale,thick,metal in ((.97,.0013,GOLD),(1.04,.00145,DARK),(1.065,.00065,GOLD)):
        s.tube(metal,[c+scale*(u*cos(i*tau/80)+v*sin(i*tau/80)) for i in range(81)],thick,10)
    for j in range(26):
        t=j*tau/26;pearl(c+1.005*(u*cos(t)+v*sin(t)),.0007,GOLD)
    # Azure kingfisher from a tapered series of sections; wings and primaries overlap.
    s.part("Enamel kingfisher with separate primaries, tail, claws and beak")
    for x in (-.010,-.002):
        s.rod(DARK,(x,-.001,.046),(x+.001,0,.0525),.0008)
        for h in (-1,1):bezier(GOLD,(x,-.001,.046),(x+.001,h*.002,.044),(x+.003,h*.0035,.043),(x+.004,h*.003,.0433),.00048,10)
    profile=[(-.027,.058,.0017,.002),(-.021,.060,.005,.0048),(-.011,.062,.008,.0086),(-.002,.066,.0075,.009),
             (.003,.071,.0066,.0077),(.006,.075,.0061,.0061),(.010,.076,.0053,.0049),(.014,.075,.0036,.0031),(.016,.075,.001,.0013)]
    last=None;n=24
    for x,z,ry,rz in profile:
        row=[(x,ry*cos(i*tau/n),z+rz*sin(i*tau/n)) for i in range(n)]
        if last:
            for i in range(n):
                j=(i+1)%n
                m=BREAST if sin((i+.5)*tau/n)<-.15 else FEATHER
                s.face([last[i],last[j],row[j],row[i]],m,True)
        last=row
    s.rod("Blued",(.013,0,.0756),(.030,-.0002,.0759),.0022,0,sides=8)
    s.rod(DARK,(.014,0,.074),(.027,0,.0729),.0012,0,sides=8)
    for h in (-1,1):
        pearl((.0102,h*.0048,.0773),.0015,"Ink");pearl((.0108,h*.0058,.0778),.00034,"Pearl")
        for i in range(6):
            feather((.0008-i*.0009,h*(.003+i*.0006),.0705-i*.00055),
                    (-.023-i*.0014,h*(.005+i*.00065),.060+i*.00015),.00165,.0012,FEATHER if i%2 else BLUE)
        for j in range(4):
            feather((-.018,h*.001,.062),(-.038-j*.0013,h*(.001+j*.0014),.060+j*.0004),.00145,.0007,BLUE)
        for j in range(5):
            pearl((.003-j*.0017,h*.0067,.0703),.00055,"Pearl")
def egg():
    c=.071;h=.060;r=.041
    def surface(a,lat):
        rad=r*cos(lat)*(1-.14*sin(lat))
        return (rad*cos(a),rad*sin(a),c+h*sin(lat))
    s.part("Scalloped vermeil foot, chased calyx and three acanthus scrolls")
    s.turned(DARK,(0,0,0),[(0,.024),(.002,.029),(.005,.030),(.008,.027),(.012,.020)],sides=64)
    ring(GOLD,(0,0,.004),.029,.029,.00155)
    for k in range(3):
        a=(k+.2)*tau/3
        def f(r,z,t=0):return (r*cos(a+t),r*sin(a+t),z)
        bezier(GOLD,f(.027,.004),f(.043,.002,.11),f(.025,.026,.10),f(.033,.041),.00175)
        for i in range(5):
            bezier(GOLD,f(.022+i*.002,.013+i*.003),f(.036,.010+i*.006,.30),
                   f(.034,.021+i*.005,.31),f(.029+i*.001,.028+i*.003,.17),.00053)
    for half in (-1,1):
        s.part("Mulberry guilloche lower bowl" if half<0 else "Raised egg lid, painted inner landscape and pearl seam")
        n=80;m=28
        for j in range(m):
            lo=(-1+j/m)*pi/2 if half<0 else j/m*pi/2
            hi=lo+pi/(2*m)
            for i in range(n):
                a,b=i*tau/n,(i+1)*tau/n
                v=(lo+pi/2)/pi;w=(hi+pi/2)/pi
                pts=[surface(a,lo),surface(b,lo),surface(b,hi),surface(a,hi)]
                uv=[(i/n*3,v*1.5),((i+1)/n*3,v*1.5),((i+1)/n*3,w*1.5),(i/n*3,w*1.5)]
                if half<0 and j==0:pts.pop(0);uv.pop(0)
                if half>0 and j==m-1:pts.pop(2);uv.pop(2)
                s.face(pts,ROSE,True,uv)
        for i in range(10):
            for sign in (-1,1):
                s.tube(GOLD,[surface(tau*i/10+sign*half*j*.015,half*(j+.4)*.032) for j in range(40)],.00048,8)
        for lat in (half*.012,half*.11,half*.80):
            radius,_,z=surface(0,lat)
            ring(GOLD,(0,0,z),radius,radius,.00114)
        for j in range(46):
            a=tau*j/46;x,y,z=surface(a,half*.045)
            pearl((x*1.018,y*1.018,z),.00096,"Pearl")
        if half>0:
            # This scene is fired on the inside of the cover; its original orientation is down.
            ovalplate(NIGHT,(0,0,c+.001),(r*.94,0,0),(0,-r*.94,0))
            for scale in (.90,.95):
                ring(GOLD,(0,0,c+.0004),r*scale,r*scale,.00065)
            rosecut((0,0,c+h-.0016),.0046)
            pivot=Vector((0,r,c));rotation=Matrix.Rotation(-1.80,3,"X")
            s.group.verts[:]=[pivot+rotation@(v-pivot) for v in s.group.verts]
    s.part("Rose-set bezels, hinge knuckles and release button")
    for i in range(5):
        a=(i+.65)*tau/5; x,y,z=surface(a,-.42)
        radial=Vector((cos(a),sin(a),0));along=Vector((-sin(a),cos(a),0));up=Vector((0,0,1))
        center=Vector((x,y,z))+radial*.0012
        for j in range(5):
            t=j*tau/5
            bud=center+(along*cos(t)+up*sin(t))*.0024
            s.ellipsoid(GOLD,bud,(.00175,.00175,.00175),12)
        pearl(center+radial*.0017,.0019,GARNET)
    for x in (-.013,-.005,.005,.013):
        s.rod(DARK,(x-.003,.0395,c),(x+.003,.0395,c),.0021,sides=20)
    s.rod(GOLD,(-.017,.0395,c),(.017,.0395,c),.00085)
    pearl((0,-.0406,c-.006),.0024,GARNET)
    s.part("Velvet-lined surprise and miniature three-arch stone viaduct")
    s.turned(VELVET,(0,0,c-.007),[(0,.038),(.005,.039)],sides=80)
    for k in range(2):
        yy=-.010+.011*k
        for x in (-.031,-.0105,.0105,.031):
            s.box(DARK,(x,yy,c+.0027),(.003,.003,.0103),.0005)
            s.box(GOLD,(x,yy,c+.0048),(.0043,.0035,.0015),.0003)
        for x in (-.0207,0,.0207):
            for j in range(12):
                a=j*pi/12;b=(j+1)*pi/12
                pts=[(x+rr*cos(t),yy-.0016,c+.0032+rr*.68*sin(t))
                    for rr,t in ((.0088,a),(.0109,a),(.0109,b),(.0088,b))]
                s.face(pts,GOLD if j%3 else DARK)
            s.box(DARK,(x,yy,c+.0120),(.0207,.0029,.0014),.00025)
        s.rod(GOLD,(-.033,yy,c+.013),(.033,yy,c+.013),.0005,sides=8)
    s.part("North Fell steam engine with pierced drivers, rods, tender and cab glazing")
    y=-.0047;z=c+.014
    for k in range(13):
        s.box("Walnut",(-.030+k*.0048,y,z),(.0017,.0107,.00075),.00012)
    for side in (-1,1):
        yy=y+side*.0045
        s.rod("Blued",(-.033,yy,z+.0004),(.033,yy,z+.0004),.00044,sides=8)
    # Side elevations read as a locomotive before the loupe-level detail is visible.
    s.box(DARK,(.005,y,z+.0065),(.041,.012,.0023),.00065)
    s.rod(GOLD,(-.006,y,z+.0135),(.023,y,z+.0135),.0051,sides=36)
    s.rod("Blued",(.0175,y,z+.0135),(.025,y,z+.0135),.0047,sides=32)
    for x in (-.004,.004,.012,.0218):
        s.rod(DARK,(x,y,z+.0135),(x+.0006,y,z+.0135),.00525,sides=30)
    s.turned("Blued",(.0208,y,z+.0177),[(0,.00175),(.0024,.00136),(.0041,.00165),(.0051,.0024)],sides=22)
    s.turned(GOLD,(.0075,y,z+.0176),[(0,.0019),(.0012,.0019),(.002,.0014),(.00255,.00045)],sides=22)
    s.box(BLUE,(-.0108,y,z+.0100),(.0105,.0117,.003),.0005)
    for yy in (y-.0057,y+.0057):
        for xx in (-.0152,-.0074):
            s.rod(GOLD,(xx,yy,z+.010),(xx,yy,z+.0226),.00065,sides=10)
        s.rod(DARK,(-.0133,yy,z+.0105),(-.0133,yy,z+.0218),.00035,sides=8)
        s.rod(GOLD,(.0005,yy,z+.016),(.018,yy,z+.016),.00030,sides=8)
    s.box("Blued",(-.0113,y,z+.0226),(.013,.016,.0015),.00052)
    s.box(GOLD,(-.0113,y,z+.0233),(.0131,.0161,.00055),.0002)
    s.box(DARK,(-.025,y,z+.0068),(.015,.0107,.002),.0005)
    s.box(BLUE,(-.025,y,z+.0101),(.0141,.0094,.0055),.00065)
    for j in range(9):
        s.ellipsoid("Ink",(-.030+j%3*.0044,y+(j//3-1)*.0029,z+.013),(.00235,.0016,.0017),12)
    for side in (-1,1):
        yy=y+side*.00575
        for j,x in enumerate((-.029,-.0225,-.012,-.004,.004,.012,.0205)):
            radius=.00305 if j in (2,3,4,5) else .00240
            hub=(x,yy,z+radius+.00095)
            ring("Blued",hub,radius,radius,.00048,'xz',32)
            for k in range(6):
                a=k*tau/6
                s.rod(GOLD,hub,(x+radius*cos(a),yy,hub[2]+radius*sin(a)),.00023,sides=6)
            pearl(hub,.00064,GOLD)
        for j in range(3):
            x=-.012+j*.008;s.rod("Silver",(x,yy+side*.00065,z+.0037),(x+.008,yy+side*.00065,z+.0041),.00034,sides=8)
        s.rod(DARK,(.012,yy,z+.0039),(.021,yy,z+.0072),.00043,sides=8)
    s.rod(GOLD,(.026,y,z+.012),(.027,y,z+.012),.0013,sides=14)
    pearl((.027,y,z+.012),.00079,"Pearl")
