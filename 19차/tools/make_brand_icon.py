"""첫 번째 흑백 시안의 EXP 글자 표식을 다중 해상도 아이콘으로 생성합니다."""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root = Path(__file__).resolve().parents[1]
image = Image.new("RGBA", (1024, 1024), (0, 0, 0, 0))
draw = ImageDraw.Draw(image)
draw.rounded_rectangle((32, 32, 992, 992), radius=192, fill="#FFFFFF", outline="#B8B8B8", width=12)
font = ImageFont.truetype(str(root / "소스코드/resources/fonts/NanumGothicBold.ttf"), 310)
draw.text((512, 512), "EXP", font=font, fill="#101010", anchor="mm", stroke_width=3)
image = image.resize((256, 256), Image.Resampling.LANCZOS)
resources = root / "소스코드" / "resources"
image.save(resources / "app.ico", sizes=[(16,16),(24,24),(32,32),(48,48),(64,64),(128,128),(256,256)])
image.save(resources / "app.png")
print("EXP 흑백 아이콘 7개 해상도 생성 완료")
