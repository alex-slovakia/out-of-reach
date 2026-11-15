$ErrorActionPreference='Stop'
$stage=$PSScriptRoot
$repo=(Resolve-Path (Join-Path $stage '../../../.local/i01-integration')).Path
$unity='C:/Program Files/Unity/Hub/Editor/6000.3.28f1/Editor/Data'
$evidence=Join-Path $stage 'Evidence'
$framework="$unity/UnityReferenceAssemblies/unity-4.8-api"
$options=@('/nologo','/langversion:9','/nostdlib+','/warnaserror+','/nowarn:0649',"/reference:$framework/mscorlib.dll","/reference:$framework/System.dll","/reference:$framework/System.Core.dll","/reference:$framework/Facades/netstandard.dll")
$refs=@(Get-ChildItem -LiteralPath "$unity/Managed" -Filter 'Unity*.dll' -Recurse | ForEach-Object { '/reference:'+$_.FullName })
$runtime=@(Get-ChildItem -LiteralPath "$repo/Library/ScriptAssemblies" -Filter '*.dll' | Where-Object {$_.Name -notmatch 'NativeQa|PlayQa|MissionQa|Assembly-CSharp|BootstrapMicroRoute|OutOfReach.Composition|OutOfReach.Tests'} | ForEach-Object {'/reference:'+$_.FullName})
$candidate="$stage/Assets/_OutOfReach/Scripts/Composition/Runtime/I06MicrogravityBridge.cs"
$response=Join-Path $evidence 'compile.rsp'
@($options+$refs+$runtime+@('/target:library',"/out:$evidence/DirectedPushIntent.Check.dll",$candidate)) | ForEach-Object {'"'+$_+'"'} | Set-Content -LiteralPath $response -Encoding utf8
& dotnet "$unity/DotNetSdkRoslyn/csc.dll" /noconfig "@$response" 2>&1 | Tee-Object -FilePath "$evidence/compile.log"
if($LASTEXITCODE-ne 0){exit $LASTEXITCODE}
'PASS: full candidate bridge reference compile; no Unity/native execution.' | Add-Content -LiteralPath "$evidence/compile.log"
$response=Join-Path $evidence 'tests.rsp'
@($options+$refs+$runtime+@('/target:exe',"/out:$evidence/DirectedPushTests.exe",$candidate,"$evidence/DirectedPushTests.cs")) | ForEach-Object {'"'+$_+'"'} | Set-Content -LiteralPath $response -Encoding utf8
& dotnet "$unity/DotNetSdkRoslyn/csc.dll" /noconfig "@$response" 2>&1 | Tee-Object -FilePath "$evidence/tests-compile.log"
if($LASTEXITCODE-ne 0){exit $LASTEXITCODE}
& dotnet "$evidence/DirectedPushTests.exe" $repo 2>&1 | Tee-Object -FilePath "$evidence/tests.log"
if($LASTEXITCODE-ne 0){exit $LASTEXITCODE}
