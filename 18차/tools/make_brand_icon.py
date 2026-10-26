"""기존 EXP 세 줄 표식을 새 코랄 팔레트의 다중 해상도 아이콘으로 생성합니다."""
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[1]
image = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
draw = ImageDraw.Draw(image)
draw.rounded_rectangle((32, 32, 992, 992), radius=254, fill="#F47765")
for y, width in ((290, 440), (480, 330), (670, 440)):
    draw.rounded_rectangle((292, y, 292 + width, y + 70), radius=35, fill="#18243A")
image = image.resize((256, 256), Image.Resampling.LANCZOS)
resources = root / "소스코드" / "resources"
image.save(resources / "app.ico", sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
image.save(resources / "app.png")
print("EXP 코랄 아이콘 7개 해상도 생성 완료")
