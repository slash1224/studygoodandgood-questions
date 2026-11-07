# Build StudyTV APK without Gradle (aapt2 + javac + d8 + apksigner)
$ErrorActionPreference = "Stop"

$PROJ = $PSScriptRoot
$TOOLS = Join-Path (Split-Path $PROJ -Parent) "tools"
$JDK = "$TOOLS\jdk17"
$SDK = "$TOOLS\android-sdk"
$BT = "$SDK\build-tools\34.0.0"
$ANDROID_JAR = "$SDK\platforms\android-34\android.jar"
$ZXING = "$TOOLS\libs\zxing-core-3.5.3.jar"
$BUILD = "$PROJ\build"
$OUT = "$PROJ\dist"

$env:JAVA_HOME = $JDK
$JAVA = "$JDK\bin\java.exe"
$JAVAC = "$JDK\bin\javac.exe"

if (!(Test-Path $ANDROID_JAR)) { throw "android.jar not found: $ANDROID_JAR" }
if (!(Test-Path $ZXING)) { throw "zxing jar not found: $ZXING" }

Remove-Item $BUILD -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path "$BUILD\gen" | Out-Null
New-Item -ItemType Directory -Force -Path "$BUILD\classes" | Out-Null
New-Item -ItemType Directory -Force -Path "$BUILD\dex" | Out-Null
New-Item -ItemType Directory -Force -Path "$BUILD\resflat" | Out-Null
New-Item -ItemType Directory -Force -Path "$OUT" | Out-Null

Write-Host "[1/7] aapt2 compile resources"
& "$BT\aapt2.exe" compile --dir "$PROJ\res" -o "$BUILD\res.zip"
Expand-Archive -Path "$BUILD\res.zip" -DestinationPath "$BUILD\resflat" -Force
$flatFiles = @(Get-ChildItem "$BUILD\resflat" -Recurse -Filter *.flat | ForEach-Object { $_.FullName })

Write-Host "[2/7] aapt2 link"
$linkArgs = @(
  "link",
  "-o", "$BUILD\base.apk",
  "-I", $ANDROID_JAR,
  "--manifest", "$PROJ\AndroidManifest.xml",
  "-A", "$PROJ\assets",
  "--java", "$BUILD\gen",
  "--min-sdk-version", "22",
  "--target-sdk-version", "34",
  "--version-code", "1",
  "--version-name", "1.0",
  "--auto-add-overlay"
) + $flatFiles
& "$BT\aapt2.exe" @linkArgs
if (!(Test-Path "$BUILD\base.apk")) { throw "aapt2 link failed" }

Write-Host "[3/7] javac"
$src = @(Get-ChildItem "$PROJ\java" -Recurse -Filter *.java | ForEach-Object { $_.FullName })
& $JAVAC -encoding UTF-8 -source 8 -target 8 -bootclasspath $ANDROID_JAR -classpath $ZXING -d "$BUILD\classes" @src
if ($LASTEXITCODE -ne 0) { throw "javac failed" }

Write-Host "[4/7] d8 -> classes.dex"
$cls = @(Get-ChildItem "$BUILD\classes" -Recurse -Filter *.class | ForEach-Object { $_.FullName })
$dexInputs = $cls + @($ZXING)
& $JAVA -Xmx1024m -cp "$BT\lib\d8.jar" com.android.tools.r8.D8 --release --min-api 22 --lib $ANDROID_JAR --output "$BUILD\dex" @dexInputs
if (!(Test-Path "$BUILD\dex\classes.dex")) { throw "d8 failed" }

Write-Host "[5/7] put classes.dex into apk"
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::Open("$BUILD\base.apk", [System.IO.Compression.ZipArchiveMode]::Update)
$entry = $zip.CreateEntry("classes.dex", [System.IO.Compression.CompressionLevel]::Optimal)
$es = $entry.Open()
$fs = [System.IO.File]::OpenRead("$BUILD\dex\classes.dex")
$fs.CopyTo($es)
$es.Dispose()
$fs.Dispose()
$zip.Dispose()

Write-Host "[6/7] zipalign"
& "$BT\zipalign.exe" -p -f 4 "$BUILD\base.apk" "$BUILD\aligned.apk"
if ($LASTEXITCODE -ne 0) { throw "zipalign failed" }

Write-Host "[7/7] sign"
$KS = "$PROJ\studygoodtv.keystore"
if (!(Test-Path $KS)) {
  Write-Host "  generating keystore"
  & "$JDK\bin\keytool.exe" -genkeypair -keystore $KS -alias studytv -keyalg RSA -keysize 2048 `
    -validity 10000 -storepass studytv2026 -keypass studytv2026 -noprompt `
    -dname "CN=StudyGoodAndGood TV,O=Family,L=Beijing,ST=Beijing,C=CN"
  if ($LASTEXITCODE -ne 0) { throw "keytool failed" }
}
$final = "$OUT\StudyTV-v1.0.apk"
if (Test-Path $final) { Remove-Item $final -Force }
& "$BT\apksigner.bat" sign --ks $KS --ks-pass pass:studytv2026 --key-pass pass:studytv2026 `
  --min-sdk-version 22 --out $final "$BUILD\aligned.apk"
if ($LASTEXITCODE -ne 0) { throw "apksigner failed" }

& "$BT\apksigner.bat" verify --print-certs $final | Out-Host

Write-Host ""
Write-Host "OK -> $final"
Get-Item $final | Select-Object Name, Length, LastWriteTime | Format-List
