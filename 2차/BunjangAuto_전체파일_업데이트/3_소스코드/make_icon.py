from PIL import Image, ImageDraw, ImageFont

size = 256
img = Image.new("RGBA", (size, size), (0,0,0,0))
d = ImageDraw.Draw(img)
cx = cy = size/2
r = size/2 - size*0.02
d.ellipse([cx-r, cy-r, cx+r, cy+r], fill=(16,17,21,255))
ring_w = size*0.045
d.ellipse([cx-r, cy-r, cx+r, cy+r], outline=(197,160,71,255), width=int(ring_w))
font = None
for path in ["/usr/share/fonts/truetype/dejavu/DejaVuSerif-Bold.ttf",
             "/usr/share/fonts/truetype/freefont/FreeSerifBold.ttf"]:
    try:
        font = ImageFont.truetype(path, int(size*0.56))
        break
    except Exception:
        pass
if font is None:
    font = ImageFont.load_default()
txt = "B"
bbox = d.textbbox((0,0), txt, font=font)
tw, th = bbox[2]-bbox[0], bbox[3]-bbox[1]
d.text((cx - tw/2 - bbox[0], cy - th/2 - bbox[1]), txt, font=font, fill=(226,193,115,255))

img.save("/home/claude/src/3_소스코드/app.ico",
         sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
print("ok")
