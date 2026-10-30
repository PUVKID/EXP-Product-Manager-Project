$ErrorActionPreference='Stop'
$base='D:\2026\EXP 프로젝트(번장 상품 등록 매크로)'
$work=Join-Path $base '작업파일\17차_UI수정'
$src=Join-Path $work 'source'
$old=Join-Path $base '16차\EXP_v2.2.7'
$out=Join-Path $base '17차'
if(Test-Path -LiteralPath $out){throw '17차 already exists; refusing to overwrite'}
$release=Join-Path $out 'EXP_v2.2.8'
New-Item -ItemType Directory -Path $release -Force|Out-Null
foreach($name in @('1_고객배포','2_윤석님용')){Copy-Item -LiteralPath (Join-Path $old $name) -Destination $release -Recurse}
Copy-Item -LiteralPath (Join-Path $old 'EXP.sln') -Destination $release
$customer=Join-Path $release '1_고객배포';$owner=Join-Path $release '2_윤석님용'
$previous=[IO.Path]::GetFullPath((Join-Path $customer 'EXP_Setup_2.2.7.exe'))
if(!$previous.StartsWith($out+'\')){throw 'Unexpected delete target'}
Remove-Item -LiteralPath $previous
$setup=Join-Path $src 'dist\installer\EXP_Setup_2.2.8.exe'
Copy-Item -LiteralPath $setup -Destination $customer
Copy-Item -LiteralPath $setup -Destination $out
foreach($name in @('EXP-Master.exe','EXP-Reset.exe')){Copy-Item -LiteralPath (Join-Path $src ('dist\'+$name)) -Destination $owner -Force;Copy-Item -LiteralPath (Join-Path $src ('dist\'+$name)) -Destination $out}
Copy-Item -LiteralPath (Join-Path $src 'dist\EXP.exe') -Destination (Join-Path $owner '설치없이확인\EXP.exe') -Force
& robocopy $src (Join-Path $release '3_소스코드') /E /XD dist bin obj /NFL /NDL /NJH /NJS /NP|Out-Null
if($LASTEXITCODE -ge 8){throw 'Source copy failed'}
$utf=[Text.UTF8Encoding]::new($true)
$guide=Join-Path $customer '시작하기.txt'
$text=[IO.File]::ReadAllText($guide).Replace('2.2.7','2.2.8')+"`r`n배송비 별도 상품은 상품 편집 아래의 배송 설정에서 일반택배 요금(100~30,000원)을 입력하세요.`r`n반값택배 가능 여부, GS25/CU 선택, 반값택배 요금과 다음 등록시에도 사용 옵션도 상품별로 저장됩니다.`r`n기존에 배송비 별도로 저장한 상품은 요금을 입력한 후 등록하세요. 포함 상품은 별도 금액을 입력하지 않습니다.`r`n"
[IO.File]::WriteAllText($guide,$text,$utf)
[IO.File]::WriteAllText((Join-Path $release 'SECURITY.md'),[IO.File]::ReadAllText((Join-Path $old 'SECURITY.md')).Replace('2.2.7','2.2.8'),$utf)
$readme=@'
EXP 2.2.8 · 17차

설치: EXP_Setup_2.2.8.exe
고객 전달용: EXP_v2.2.8_고객배포.zip
관리자: EXP-Master.exe / EXP-Reset.exe

수정 사항
1. 카테고리·상품 상태 선택칸의 글자 위치와 여백을 수정했습니다. 직접 한글 입력과 목록 선택을 유지합니다.
2. 라이선스 화면은 배율·창 너비에 맞춰 배치됩니다. 입력칸, 인증 버튼, 안내 문구와 카드가 겹치지 않습니다.
3. 상품 편집 아래에 배송 설정을 추가했습니다.
   - 배송비 포함 / 별도
   - 일반택배 요금 100~30,000원
   - 반값택배 불가능 / 가능, 둘 다 가능 / GS25 만 / CU만, 반값택배 요금
   - 다음 등록시에도 사용
4. 실제 등록 시 상품의 배송 설정을 입력하고 적용 여부를 확인합니다.
   사이트가 허용하지 않는 배송 요금·조건이면 등록을 중단합니다.
5. 반복 횟수가 커져도 실행 상태 표시와 배지가 겹치지 않습니다. 회차는 진행 정보에 표시합니다.
6. 사용 설명서에 배송 설정 안내를 추가했습니다.

기존 EXP를 종료하고 설치 파일로 업데이트하세요. 인증 초기화는 필요하지 않습니다.
이번 작업에서 설치된 앱이나 실제 인증·사용량·상품 데이터를 초기화하지 않았습니다.
배송비 별도로 저장한 기존 상품은 배송 요금을 한 번 입력해야 합니다.

나눔고딕 Bold와 라이트·다크를 유지합니다.
계정 변경 허용과 Classic 25 / Studio 50 / Pro 100의 하루 총량, 추가 등록권 정책은 유지합니다.
Master, Reset, 소스코드 및 전체 ZIP은 관리자용입니다. 고객에게는 고객배포 ZIP을 전달하세요.

검증: 100·125·150·175·200% 배율 재현, 배송 입력 테스트, 저장·취소, 기존 등록 정책 검사.
실제 다른 PC에서의 재확인과 실제 상품 게시까지 수행한 것은 아닙니다.
문의: expmasterxx00@gmail.com
'@
foreach($dir in @($out,$release)){[IO.File]::WriteAllText((Join-Path $dir '먼저읽기.txt'),$readme,$utf)}
$reports=Join-Path $release '4_검증결과';New-Item -ItemType Directory -Path $reports|Out-Null
foreach($name in @('dpi1.txt','dpi1_25.txt','dpi1_5.txt','dpi1_75.txt','dpi2.txt','ShippingFormTests.txt','ReleaseTests.txt','test-build.txt','installer-build.txt','reset-build.txt')){Copy-Item -LiteralPath (Join-Path $work $name) -Destination $reports}
foreach($scale in @('1','1_5','2')){Copy-Item -LiteralPath (Join-Path $work ('preview\'+$scale)) -Destination (Join-Path $reports ('화면_'+$scale)) -Recurse}
$report=@'
17차 / EXP 2.2.8 검증 결과 · 2026-10-13

카테고리 문제: editable ComboBox의 네이티브 입력창이 둥근 표면과 별도로 배치되던 문제.
글자 영역에 좌우 여백과 세로 가운데 정렬을 적용하고 출력/렌더 경로도 동일하게 처리했습니다.
라이선스·자동 등록 화면: 고정 좌표와 배율 적용을 혼합하지 않고, 실제 배율/텍스트 높이/창 너비를 기준으로 재배치합니다.
상태 배지 영역을 따로 확보하고 긴 회차 및 안내 문구는 필요한 높이를 갖습니다.

DpiLayoutTests: 배율 1, 1.25, 1.5, 1.75, 2 각각 1,137 통과. 합계 5,685 통과.
라이트·다크, 넓은/좁은 창, 스크롤 후 크기 변경, 컨트롤 겹침/카드 경계,
콤보 입력창의 실제 좌표·드롭다운 선택, 큰 반복 횟수, 상품 배송 설정 저장/취소를 확인했습니다.
이 PC에서 배율과 글꼴 크기를 재현한 테스트이며 다른 하드웨어에서 실행한 것은 아닙니다.

ShippingFormTests: 16 통과. 상품 JSON의 기존 형식 호환 및 암호화 저장 왕복,
일반택배 100/30,000 경계, 반값택배 3가지 편의점, 가능/불가능, 포함/별도 전환,
기본값 기억 옵션, 사이트의 placeholder 요금 범위, 반값택배 잠금 상황을 검사했습니다.
실제 Bot.ConfigureShipping을 별도 익명 Chrome과 로컬 HTML 재현 화면에서 실행했습니다.
공개 번개장터 웹 코드의 현재 입력 구조를 근거로 했으며 실제 계정에 상품을 게시하지 않았습니다.
참고 구조: https://m.bunjang.co.kr/static/js/ProductDetailAddressField.CgBh5oXM.chunk.js
배송 요금 설정 API는 비인증 요청에 401을 반환해 더 접근하지 않았습니다.
반값택배의 실제 허용 요금은 등록 화면에 표시된 범위를 읽어 검사합니다.

ReleaseTests: 50 통과, 0 실패. 기존 등급별 총량, 추가 등록권, 날짜 초기화 및 예약/서명 정책 검사.
테스트 저장소와 제품 인증 저장소는 분리했습니다. 사용자의 실제 인증이나 상품 목록을 수정하지 않았습니다.
사용 설명서 및 고객 시작 안내를 갱신했습니다.
설치 EXE / Master / Reset 빌드 완료. 고객 ZIP에는 설치 EXE와 안내/라이선스 문서만 포함합니다.
Authenticode 배포자 서명은 없습니다.
'@
[IO.File]::WriteAllText((Join-Path $reports '17차_검증결과.txt'),$report,$utf)
$hashes=[ordered]@{}
foreach($file in Get-ChildItem -LiteralPath $release -File -Recurse|Sort-Object FullName){$key=$file.FullName.Substring($release.Length+1).Replace('\','/');$hashes[$key]=(Get-FileHash -LiteralPath $file.FullName).Hash.ToLowerInvariant()}
[IO.File]::WriteAllText((Join-Path $release 'SHA256.json'),($hashes|ConvertTo-Json -Depth 3),$utf)
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($customer,(Join-Path $out 'EXP_v2.2.8_고객배포.zip'))
[IO.Compression.ZipFile]::CreateFromDirectory($release,(Join-Path $out 'EXP_v2.2.8_17차_전체.zip'))
foreach($pair in @(@{zip='EXP_v2.2.8_고객배포.zip';folder=$customer},@{zip='EXP_v2.2.8_17차_전체.zip';folder=$release})){
    $zip=[IO.Compression.ZipFile]::OpenRead((Join-Path $out $pair.zip))
    try{foreach($entry in $zip.Entries){if(!$entry.Name){continue};$stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create();try{$actual=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$sha.Dispose()};if($actual -ne (Get-FileHash -LiteralPath (Join-Path $pair.folder $entry.FullName)).Hash){throw ('ZIP mismatch '+$entry.FullName)}};Write-Output ($pair.zip+': '+$zip.Entries.Count+' files verified')}finally{$zip.Dispose()}
}
Write-Output ('Manifest: '+$hashes.Count+' files')
