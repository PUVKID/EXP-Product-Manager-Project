"""EXP 전용 베벨 E 메시를 제작하고 실제 원근·Z버퍼로 PNG 프레임을 굽습니다."""
from pathlib import Path
import math,json,struct,hashlib,time
import numpy as np
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'소스코드'/'resources'/'brand'
OUT.mkdir(parents=True,exist_ok=True)
SIZE,COUNT,COLS=192,48,8
outline=np.array([[-.72,-.88],[.72,-.88],[.72,-.53],[-.25,-.53],[-.25,-.17],[.49,-.17],[.49,.17],[-.25,.17],[-.25,.53],[.72,.53],[.72,.88],[-.72,.88]],float)
# 외곽을 중심 쪽으로 조금 줄여 전면과 측면 사이에 실제 경사면을 만듭니다.
inner=outline*.94
triangles=[]
def add(a,b,c):
 a,b,c=map(lambda p:np.asarray(p,float),(a,b,c));n=np.cross(b-a,c-a);n/=np.linalg.norm(n);triangles.append((np.array([a,b,c]),n))
def ears(poly):
 ids=list(range(len(poly)));result=[]
 def cross(a,b,c):return np.cross(b-a,c-a).item()
 while len(ids)>3:
  for k in range(len(ids)):
   a,b,c=ids[k-1],ids[k],ids[(k+1)%len(ids)]
   if cross(poly[a],poly[b],poly[c])<=0:continue
   if any(cross(poly[a],poly[b],poly[j])>=-1e-8 and cross(poly[b],poly[c],poly[j])>=-1e-8 and cross(poly[c],poly[a],poly[j])>=-1e-8 for j in ids if j not in (a,b,c)):continue
   result.append((a,b,c));ids.pop(k);break
  else:raise ValueError('메시 전면 삼각분할 실패')
 result.append(tuple(ids));return result
for a,b,c in ears(inner):
 add([*inner[a],.25],[*inner[b],.25],[*inner[c],.25]);add([*inner[c],-.25],[*inner[b],-.25],[*inner[a],-.25])
for i in range(len(outline)):
 j=(i+1)%len(outline)
 for z,zinner in [(.19,.25),(-.19,-.25)]:
  a=[*outline[i],z];b=[*outline[j],z];c=[*inner[j],zinner];d=[*inner[i],zinner]
  if z>0:add(a,b,c);add(a,c,d)
  else:add(c,b,a);add(d,c,a)
 a=[*outline[i],-.19];b=[*outline[j],-.19];c=[*outline[j],.19];d=[*outline[i],.19];add(a,b,c);add(a,c,d)
def rotation(t):
 x=math.radians(-12+5*math.sin(t));y=math.radians(-19+9*math.sin(t+.5));z=math.radians(-8+3*math.cos(t))
 rx=np.array([[1,0,0],[0,math.cos(x),-math.sin(x)],[0,math.sin(x),math.cos(x)]]);ry=np.array([[math.cos(y),0,math.sin(y)],[0,1,0],[-math.sin(y),0,math.cos(y)]]);rz=np.array([[math.cos(z),-math.sin(z),0],[math.sin(z),math.cos(z),0],[0,0,1]])
 return rz@ry@rx
def render(t,size=SIZE):
 R=rotation(t);canvas=np.zeros((size,size,4),np.uint8);depth=np.full((size,size),np.inf);light=np.array([-.5,.7,1.2]);light/=np.linalg.norm(light)
 for vertices,normal in triangles:
  world=vertices@R.T;world[:,1]+=.045*math.sin(t);n=normal@R.T
  if n[2]<=0:continue
  z=4-world[:,2];xy=world[:,:2]/z[:,None]*(size*1.55);xy[:,0]+=size/2;xy[:,1]=size/2-xy[:,1]
  minx,maxx=max(0,math.floor(xy[:,0].min())),min(size-1,math.ceil(xy[:,0].max()));miny,maxy=max(0,math.floor(xy[:,1].min())),min(size-1,math.ceil(xy[:,1].max()))
  if minx>maxx or miny>maxy:continue
  X,Y=np.meshgrid(np.arange(minx,maxx+1)+.5,np.arange(miny,maxy+1)+.5);a,b,c=xy
  d=(b[1]-c[1])*(a[0]-c[0])+(c[0]-b[0])*(a[1]-c[1])
  if abs(d)<1e-8:continue
  u=((b[1]-c[1])*(X-c[0])+(c[0]-b[0])*(Y-c[1]))/d;v=((c[1]-a[1])*(X-c[0])+(a[0]-c[0])*(Y-c[1]))/d;w=1-u-v
  distance=u*z[0]+v*z[1]+w*z[2];region=depth[miny:maxy+1,minx:maxx+1];mask=(u>=0)&(v>=0)&(w>=0)&(distance<region)
  if not mask.any():continue
  position=u[:,:,None]*world[0]+v[:,:,None]*world[1]+w[:,:,None]*world[2];view=np.array([0.,0.,4.])-position;view/=np.linalg.norm(view,axis=2)[:,:,None]
  half=view+light;half/=np.linalg.norm(half,axis=2)[:,:,None];spec=np.maximum(0,np.sum(half*n,axis=2))**24
  stripe=np.exp(-((position[:,:,0]+position[:,:,1]*.42+.2)/.28)**2)
  shade=np.clip(.21+.49*max(0,np.dot(n,light))+.27*spec+.14*stripe,.10,1.)
  gray=(np.power(shade,1/2.2)*255).astype(np.uint8);target=canvas[miny:maxy+1,minx:maxx+1];target[mask,:3]=gray[mask,None];target[mask,3]=255;region[mask]=distance[mask]
 return Image.fromarray(canvas)
start=time.perf_counter();sheet=Image.new('RGBA',(SIZE*COLS,SIZE*math.ceil(COUNT/COLS)))
for index in range(COUNT):sheet.alpha_composite(render(math.tau*index/COUNT),(index%COLS*SIZE,index//COLS*SIZE))
sheet.save(OUT/'exp-prism-sheet.png',optimize=True)
still=render(0,768).resize((384,384),Image.Resampling.LANCZOS);still.save(OUT/'exp-prism-still.png',optimize=True)
# GLB는 원본 메시와 법선을 보존하는 제작 자산입니다. 앱에는 PNG만 포함합니다.
positions=np.vstack([vertices for vertices,n in triangles]).astype('<f4')*.04;normals=np.vstack([np.tile(n,(3,1)) for vertices,n in triangles]).astype('<f4');blob=positions.tobytes()+normals.tobytes()
gltf={'asset':{'version':'2.0','generator':'EXP 자체 메시 제작기'},'scene':0,'scenes':[{'nodes':[0]}],'nodes':[{'name':'EXP_Bevel_E','mesh':0}],'meshes':[{'name':'EXP_Bevel_E','primitives':[{'attributes':{'POSITION':0,'NORMAL':1},'material':0,'mode':4}]}],'materials':[{'name':'EXP_Silver','pbrMetallicRoughness':{'baseColorFactor':[.8,.8,.8,1],'metallicFactor':.75,'roughnessFactor':.3}}],'buffers':[{'byteLength':len(blob)}],'bufferViews':[{'buffer':0,'byteOffset':0,'byteLength':positions.nbytes},{'buffer':0,'byteOffset':positions.nbytes,'byteLength':normals.nbytes}],'accessors':[{'bufferView':0,'componentType':5126,'count':len(positions),'type':'VEC3','min':positions.min(axis=0).tolist(),'max':positions.max(axis=0).tolist()},{'bufferView':1,'componentType':5126,'count':len(normals),'type':'VEC3'}]}
data=json.dumps(gltf,separators=(',',':')).encode();data+=b' '*((-len(data))%4);blob+=b'\0'*((-len(blob))%4);glb=struct.pack('<III',0x46546c67,2,12+8+len(data)+8+len(blob))+struct.pack('<II',len(data),0x4E4F534A)+data+struct.pack('<II',len(blob),0x004E4942)+blob
(OUT/'exp-prism-source.glb').write_bytes(glb)
icon=Image.new('RGBA',(512,512));draw=ImageDraw.Draw(icon);draw.rounded_rectangle((12,12,500,500),radius=112,fill=(18,18,18,255));badge=still.resize((492,492),Image.Resampling.LANCZOS);icon.alpha_composite(badge,(10,10));icon.save(OUT.parent/'app.png');icon.save(OUT.parent/'app.ico',sizes=[(s,s) for s in [16,24,32,48,64,128,256]])
metadata={'schema_version':1,'width':SIZE,'height':SIZE,'columns':COLS,'frames':COUNT,'fps':12,'period_seconds':4,'mesh':'EXP_Bevel_E','triangles':len(triangles),'units':'원본 GLB 미터; E 높이0.07m','pivot':[0,0,0],'renderer':'NumPy 삼각형 원근 투영·픽셀 Z버퍼·면 법선 Lambert/Blinn-Phong 조명','motion':'X/Y/Z 고유 축의 작은 주기적 회전과 Y 부유, 4초주기','alpha':'straight RGBA, alpha0 RGB0','runtime':'PNG 시트 공유 1개, GPU/웹서버 없음','external_assets':False,'elapsed_seconds':time.perf_counter()-start,'sheet_bytes':(OUT/'exp-prism-sheet.png').stat().st_size,'sheet_sha256':hashlib.sha256((OUT/'exp-prism-sheet.png').read_bytes()).hexdigest(),'glb_sha256':hashlib.sha256(glb).hexdigest(),'limitation':'경로추적/물리정확 금속 반사 아님; 실제 기하와 깊이로 구운 스타일 렌더'}
(OUT/'manifest.json').write_text(json.dumps(metadata,ensure_ascii=False,indent=2)+'\n',encoding='utf-8');(OUT/'LICENSE.txt').write_text('EXP를 위해 직접 작성한 메시·조명·모션입니다. 외부 모델/텍스처/HDRI가 없습니다.\n프로젝트 소유자의 사용·수정·재배포 용도입니다.\n',encoding='utf-8');print(json.dumps(metadata,ensure_ascii=False))
