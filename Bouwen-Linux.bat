@echo off
rem Maakt publish\DMX-Light-Control-<versie>-linux-arm64.tar.gz: DMX Light Control voor een Raspberry Pi
rem (Raspberry Pi OS 64-bit). Uitpakken op de Pi en "sudo bash install.sh" draaien: zie linux\LEESMIJ-Linux.txt.
rem Nodig om te BOUWEN: .NET 9 SDK en Node.js (de Windows-pc volstaat; .NET zit in het pakket).
rem
rem Versie: het nummer uit version.txt, dus dat van de laatste Windows-build (Bouwen.bat). Dit script verhoogt
rem het nummer niet: zo horen de Windows-zip en het Linux-pakket van dezelfde versie bij elkaar.
rem Andere processor: Bouwen-Linux.bat linux-x64 (bijv. een gewone pc met Linux).
setlocal
cd /d "%~dp0"
set RID=%1
if "%RID%"=="" set RID=linux-arm64
for /f "usebackq delims=" %%v in (`powershell -NoProfile -Command "$v=[version]((Get-Content version.txt -Raw).Trim()); '{0}.{1}.{2}' -f $v.Major,$v.Minor,$v.Build"`) do set VERSION=%%v
if "%VERSION%"=="" (echo version.txt is niet leesbaar & goto fail)
echo Versie %VERSION% (%RID%)
set NAME=DMX-Light-Control-%VERSION%-%RID%
set STAGE=publish\_linux-%VERSION%\%NAME%

where dotnet >nul 2>&1 || (echo .NET 9 SDK ontbreekt: https://dotnet.microsoft.com/download & goto fail)
where npm >nul 2>&1 || (echo Node.js ontbreekt: https://nodejs.org & goto fail)
if not exist "%SystemRoot%\System32\tar.exe" (echo tar.exe ontbreekt: nodig is Windows 10 1803 of nieuwer & goto fail)

echo [1/4] Interface bouwen...
pushd ui
call npm ci --no-audit --no-fund || (popd & goto fail)
call npm run build || (popd & goto fail)
popd

echo [2/4] App publiceren (zelfstandig, %RID%)...
if exist publish\_linux-%VERSION% rmdir /s /q publish\_linux-%VERSION%
dotnet publish src\DmxLicht.App -f net9.0 -c Release -r %RID% --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=none -p:Version=%VERSION% -o %STAGE% --nologo -v q || goto fail

echo [3/4] Installatiebestanden...
rem altijd LF-regeleinden: een script met Windows-regeleinden start niet op Linux
powershell -NoProfile -Command "foreach ($f in 'install.sh','dmx-light-control.service','60-udmx.rules','LEESMIJ-Linux.txt') { $t = [IO.File]::ReadAllText((Resolve-Path ('linux\' + $f))).Replace([string][char]13, ''); [IO.File]::WriteAllText((Join-Path (Resolve-Path '%STAGE%') $f), $t) }" || goto fail

echo [4/4] tar.gz maken...
if exist publish\%NAME%.tar.gz del /q publish\%NAME%.tar.gz
rem tar van Windows kent geen Linux-rechten: install.sh zet DmxLicht zelf uitvoerbaar
"%SystemRoot%\System32\tar.exe" -czf publish\%NAME%.tar.gz -C publish\_linux-%VERSION% %NAME% || goto fail
powershell -NoProfile -Command "$h=(Get-FileHash 'publish\%NAME%.tar.gz' -Algorithm SHA256).Hash.ToLower(); [IO.File]::WriteAllText('publish\%NAME%.tar.gz.sha256', $h + '  %NAME%.tar.gz' + [char]10)" || goto fail
rmdir /s /q publish\_linux-%VERSION%

echo.
echo Klaar: publish\%NAME%.tar.gz
echo Op de Pi: tar xzf %NAME%.tar.gz ^&^& cd %NAME% ^&^& sudo bash install.sh
exit /b 0

:fail
echo.
echo Bouwen mislukt.
pause
exit /b 1
