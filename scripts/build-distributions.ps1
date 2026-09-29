param([Parameter(Mandatory=$true)][string]$GamePath)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$build=Join-Path $repo 'build'
if(Test-Path -LiteralPath $build){throw 'build 폴더가 이미 있습니다. 이전 결과를 별도 보관한 뒤 다시 실행하세요.'}
New-Item -ItemType Directory -Path $build | Out-Null
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$refs=Get-ChildItem -LiteralPath (Join-Path $GamePath 'GAL PRO MASTER_Data\Managed') -Filter '*.dll' | ForEach-Object {'/reference:'+$_.FullName}
$package=Join-Path $repo 'package'
New-Item -ItemType Directory -Path "$package\BepInEx\plugins" -Force | Out-Null
& $csc /nologo /noconfig /target:library /nostdlib "/out:$package\BepInEx\plugins\GalMasterKorean.dll" @refs "/reference:$package\BepInEx\core\BepInEx.dll" "/reference:$package\BepInEx\core\0Harmony.dll" "$repo\src\GalMasterKorean.cs"
if($LASTEXITCODE -ne 0){throw '플러그인 컴파일 실패'}
$lines=Get-ChildItem -LiteralPath $package -Recurse -File | Sort-Object FullName | ForEach-Object {
 $relative=$_.FullName.Substring($package.Length+1).Replace('\','/')
 $relative+"`t"+(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
}
[IO.File]::WriteAllLines("$repo\installer\payload.tsv",[string[]]$lines,(New-Object Text.UTF8Encoding($false)))
[IO.File]::WriteAllLines("$repo\distribution\FILES.tsv",[string[]](@("path`tsha256")+$lines),(New-Object Text.UTF8Encoding($false)))
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($package,"$build\payload.zip",[IO.Compression.CompressionLevel]::Optimal,$false)
$auto="$build\auto"
$manual="$build\manual"
New-Item -ItemType Directory -Path $auto,$manual | Out-Null
Copy-Item -LiteralPath $package -Destination "$manual\패치파일" -Recurse
Copy-Item -LiteralPath "$repo\distribution\README_Auto.txt" -Destination "$auto\README.txt"
Copy-Item -LiteralPath "$repo\distribution\README_Manual.txt" -Destination "$manual\README.txt"
Copy-Item -LiteralPath "$repo\distribution\제3자_라이선스_고지.txt" -Destination $auto
Copy-Item -LiteralPath "$repo\distribution\제3자_라이선스_고지.txt" -Destination $manual
& $csc /nologo /target:winexe /platform:x64 "/out:$auto\GalMaster 한국어 패치 v1.0.0.exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll "/win32icon:$repo\installer\Installer.ico" "/win32manifest:$repo\installer\app.manifest" "/resource:$repo\installer\Installer.ico,InstallerIcon" "/resource:$repo\installer\InstallerLogo.png,InstallerLogo" "/resource:$build\payload.zip,payload.zip" "/resource:$repo\installer\payload.tsv,payload.tsv" "/resource:$repo\installer\supported.tsv,supported.tsv" "$repo\installer\Installer.cs"
if($LASTEXITCODE -ne 0){throw '설치기 컴파일 실패'}
[IO.Compression.ZipFile]::CreateFromDirectory($auto,"$build\GalMaster_Korean_Patch_v1.0.0.zip",[IO.Compression.CompressionLevel]::Optimal,$false)
[IO.Compression.ZipFile]::CreateFromDirectory($manual,"$build\GalMaster_Korean_Patch_Manual_v1.0.0.zip",[IO.Compression.CompressionLevel]::Optimal,$false)
$sums=Get-ChildItem -LiteralPath $build -Filter 'GalMaster*.zip' | Sort-Object Name | ForEach-Object {(Get-FileHash -LiteralPath $_.FullName).Hash.ToLower()+'  '+$_.Name}
[IO.File]::WriteAllLines("$build\SHA256SUMS.txt",[string[]]$sums,(New-Object Text.UTF8Encoding($false)))
Write-Output '빌드 완료'
