$ErrorActionPreference='Stop'
$base='D:\2026\EXP 프로젝트(번장 상품 등록 매크로)'
$work=Join-Path $base '작업파일\2.2.7_상점확인수정'
$src=Join-Path $work 'source'
$old=Join-Path $base '최종\EXP_v2.2.6'
$out=Join-Path $base '16차'
if(Test-Path -LiteralPath $out){throw '16차 already exists; refusing to overwrite'}
$release=Join-Path $out 'EXP_v2.2.7'
New-Item -ItemType Directory -Path $release -Force|Out-Null
foreach($name in @('1_고객배포','2_윤석님용')){Copy-Item -LiteralPath (Join-Path $old $name) -Destination $release -Recurse}
Copy-Item -LiteralPath (Join-Path $old 'EXP.sln') -Destination $release
$customer=Join-Path $release '1_고객배포'
$owner=Join-Path $release '2_윤석님용'
$oldSetup=[IO.Path]::GetFullPath((Join-Path $customer 'EXP_Setup_2.2.6.exe'))
if(!$oldSetup.StartsWith($out+'\')){throw 'Unexpected delete target'}
Remove-Item -LiteralPath $oldSetup
$newSetup=Join-Path $src 'dist\installer\EXP_Setup_2.2.7.exe'
Copy-Item -LiteralPath $newSetup -Destination $customer
Copy-Item -LiteralPath $newSetup -Destination $out
foreach($name in @('EXP-Master.exe','EXP-Reset.exe')){
    Copy-Item -LiteralPath (Join-Path $src ('dist\'+$name)) -Destination $owner -Force
    Copy-Item -LiteralPath (Join-Path $src ('dist\'+$name)) -Destination $out
}
Copy-Item -LiteralPath (Join-Path $src 'dist\EXP.exe') -Destination (Join-Path $owner '설치없이확인\EXP.exe') -Force
& robocopy $src (Join-Path $release '3_소스코드') /E /XD dist bin obj /NFL /NDL /NJH /NJS /NP|Out-Null
if($LASTEXITCODE -ge 8){throw 'Source copy failed'}
$utf=[Text.UTF8Encoding]::new($true)
$guide=Join-Path $customer '시작하기.txt'
[IO.File]::WriteAllText($guide,([IO.File]::ReadAllText($guide).Replace('2.2.6','2.2.7')+"`r`nEXP는 계정 변경과 관계없이 기기의 하루 총등록수를 합산합니다.`r`nClassic 25개 / Studio 50개 / Pro 100개. 번개장터 계정별 제한은 번개장터에서 적용합니다.`r`n"),$utf)
$security=[IO.File]::ReadAllText((Join-Path $old 'SECURITY.md')).Replace('2.2.6','2.2.7').Replace('계정별 사용량은 상점 번호의 해시로 기록합니다.','상점 번호를 조회하지 않으며 계정 변경과 관계없이 기기의 총등록수만 제한합니다. 이전 버전의 계정별 기록은 호환을 위해 읽을 수 있지만 등록 판단에는 사용하지 않습니다.').Replace('기본 사용량/계정별 사용량만 0으로 바꾸며','기본 사용량과 이전 버전의 계정별 기록을 0으로 바꾸며')
[IO.File]::WriteAllText((Join-Path $release 'SECURITY.md'),$security,$utf)
$readme=@'
EXP 2.2.7 · 16차

설치: EXP_Setup_2.2.7.exe
고객에게 전달: EXP_v2.2.7_고객배포.zip
관리자 키 발급: EXP-Master.exe
관리자 초기화 도구: EXP-Reset.exe (라이트 / 다크)

이번 수정
- 상점 번호 조회를 제거했습니다. 번개장터 로그인 상태를 확인하면 등록을 진행합니다.
- 계정을 바꿔도 등록할 수 있습니다. EXP는 기기의 하루 총량만 관리합니다.
- Classic 25개 / Studio 50개 / Pro 100개. Pro 추가 등록권은 기존처럼 사용합니다.
- 번개장터 계정별 등록 제한은 번개장터에서 적용합니다.
- 기본 한도는 한국시간 자정에 초기화되며 누적되지 않습니다.

이 PC의 기존 EXP 설치와 사용자 인증을 초기화했습니다.
상품 목록과 사진, 관리자 발급 보안키 및 발급 이력은 보존했습니다.
설치 파일에서 새 등록코드를 복사하고 EXP Master에서 새 키를 발급해 직접 인증하세요.

Master, Reset, 소스코드 및 전체 ZIP은 관리자용입니다. 고객에게는 고객배포 ZIP만 전달하세요.
EXP Reset의 Master 발급 이력 삭제는 별도 선택 항목입니다.

이번 확인은 실제 로그인 화면 확인과 별도 테스트 저장소의 자동 검사입니다.
실제 상품 게시, 다른 PC의 설치 검증, 배포자 코드 서명은 수행하지 않았습니다.
문의: expmasterxx00@gmail.com
'@
[IO.File]::WriteAllText((Join-Path $out '먼저읽기.txt'),$readme,$utf)
[IO.File]::WriteAllText((Join-Path $release '먼저읽기.txt'),$readme,$utf)
$reports=Join-Path $release '4_검증결과'
New-Item -ItemType Directory -Path $reports|Out-Null
foreach($name in @('ReleaseTests.txt','UsageRecoveryTests.txt','UsageRecoveryTests-attempt1.txt','test-build.txt','installer-build.txt','reset-build.txt')){Copy-Item -LiteralPath (Join-Path $work $name) -Destination $reports}
$report=@'
EXP 2.2.7 / 16차 검증 · 2026-10-13

원인: 기존 코드는 '내 상점' 링크에서 상점 번호를 찾았지만 현재 사이트는 메뉴 안의 버튼을 사용합니다.
최종 변경: 사용자 요청에 따라 상점 번호 조회와 EXP 자체 계정별 50개 제한을 제거했습니다.
로그인된 화면이면 진행하며 EXP는 기기별 등급 한도와 추가 등록권만 차감합니다.
실제 상품 등록 페이지에서 기존 로그인 표식(알림 버튼) 1개가 확인됐습니다.
인증/로그인 토큰을 읽거나 저장하지 않았고 실제 상품 게시를 수행하지 않았습니다.

ReleaseTests: 50 통과, 0 실패. 등급별 총량, Pro 51~100번째 등록, 과거 계정 기록 호환,
추가 등록권, 재사용 차단, 자정 초기화, 시간 예약 및 테스트 인증을 확인했습니다.
UsageRecoveryTests: 재실행 18 통과, 0 실패.
첫 실행에서는 테스트 저장소의 File.Replace에 일시적 IO 오류가 발생했습니다(13 통과, 1 실패).
원인은 확정하지 않았으며 새 테스트 프로세스에서 같은 소스로 재실행해 통과했습니다. 첫 결과도 보존했습니다.
보안 저장 형식, 발급 공개키, TPM 인증 방식은 변경하지 않았습니다.

실제 PC 작업: 사용자 요청에 따라 정상 Windows 환경에서 기존 설치를 제거하고 사용자 인증을 초기화했습니다.
제품/사진 9개 파일의 SHA256을 전후 비교해 동일함을 확인했습니다.
관리자 발급키 2개도 초기화 도구 내부 비교를 통과했습니다. Master 이력 삭제는 선택하지 않았습니다.
초기화 후 앱 설치/인증은 실행하지 않았습니다.

고객 ZIP에는 설치 파일, 시작 안내, 오픈소스/폰트 라이선스만 포함합니다.
다른 PC에서의 설치 및 실제 게시 전체 과정은 이번 작업에서 검증하지 않았습니다.
Authenticode 배포자 서명은 없습니다.
'@
[IO.File]::WriteAllText((Join-Path $reports '16차_검증결과.txt'),$report,$utf)
$hashes=[ordered]@{}
foreach($file in Get-ChildItem -LiteralPath $release -Recurse -File|Sort-Object FullName){$key=$file.FullName.Substring($release.Length+1).Replace('\','/');$hashes[$key]=(Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
[IO.File]::WriteAllText((Join-Path $release 'SHA256.json'),($hashes|ConvertTo-Json -Depth 3),$utf)
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($customer,(Join-Path $out 'EXP_v2.2.7_고객배포.zip'))
[IO.Compression.ZipFile]::CreateFromDirectory($release,(Join-Path $out 'EXP_v2.2.7_16차_전체.zip'))
foreach($pair in @(@{zip='EXP_v2.2.7_고객배포.zip';folder=$customer},@{zip='EXP_v2.2.7_16차_전체.zip';folder=$release})){
    $archive=[IO.Compression.ZipFile]::OpenRead((Join-Path $out $pair.zip))
    try{foreach($entry in $archive.Entries){if(!$entry.Name){continue};$stream=$entry.Open();$sha=[Security.Cryptography.SHA256]::Create();try{$actual=[BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-','')}finally{$stream.Dispose();$sha.Dispose()};$expected=(Get-FileHash -LiteralPath (Join-Path $pair.folder $entry.FullName)).Hash;if($actual -ne $expected){throw ('ZIP mismatch '+$entry.FullName)}};Write-Output ($pair.zip+': '+$archive.Entries.Count+' files verified')}finally{$archive.Dispose()}
}
Write-Output ('Manifest: '+$hashes.Count+' files')
