@echo off
rem Maakt publish\DMX-Light-Control-<versie>.zip (om te delen) en werkt de map publish\DmxLicht-<versie> bij.
rem Die map werkt op elke Windows 10/11 pc zonder iets te installeren (.NET zit erin).
rem Nodig om te BOUWEN (niet om te gebruiken): .NET 9 SDK en Node.js.
rem
rem Veilig: er wordt in een aparte map gebouwd. De bestaande map wordt alleen vervangen als er
rem geen DmxLicht uit draait, en de map Instellingen daarin (je show) gaat mee naar de nieuwe versie.
rem Instellingen komt nooit in de zip.
rem
rem Versie: elke build krijgt een nieuw nummer (version.txt + 1, bijv. 2.1.4 -> 2.1.5). Het nummer
rem wordt pas bewaard als de build gelukt is. Major/minor ophogen: zet het zelf in version.txt (bijv. 2.2.0).
rem De nieuwe map neemt de show (Instellingen) over van de vorige versie.
setlocal
cd /d "%~dp0"
for /f "usebackq delims=" %%v in (`powershell -NoProfile -Command "$v=[version]((Get-Content version.txt -Raw).Trim()); '{0}.{1}.{2}' -f $v.Major,$v.Minor,($v.Build+1)"`) do set VERSION=%%v
if "%VERSION%"=="" (echo version.txt is niet leesbaar & goto fail)
echo Versie %VERSION%
set OUT=publish\DmxLicht-%VERSION%
set STAGE=publish\_build-%VERSION%

where dotnet >nul 2>&1 || (echo .NET 9 SDK ontbreekt: https://dotnet.microsoft.com/download & goto fail)
where npm >nul 2>&1 || (echo Node.js ontbreekt: https://nodejs.org & goto fail)

echo [1/5] Interface bouwen...
pushd ui
call npm ci --no-audit --no-fund || (popd & goto fail)
call npm run build || (popd & goto fail)
popd

echo [2/5] Tests...
dotnet test --nologo -v q -p:Version=%VERSION% || goto fail

echo [3/5] App publiceren (zelfstandig, win-x64)...
if exist %STAGE% rmdir /s /q %STAGE%
dotnet publish src\DmxLicht.App -f net9.0-windows -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=none -p:Version=%VERSION% -o %STAGE% --nologo -v q || goto fail
copy /y LEESMIJ.txt %STAGE%\ >nul

echo [4/5] Companion-module...
pushd companion-module
call npm ci --omit=dev --no-audit --no-fund || (popd & goto fail)
popd
set CM=%STAGE%\Companion-module
mkdir %CM%\companion
copy /y companion-module\main.js %CM%\ >nul
copy /y companion-module\package.json %CM%\ >nul
copy /y companion-module\README.md %CM%\ >nul
copy /y companion-module\companion\manifest.json %CM%\companion\ >nul
robocopy companion-module\node_modules %CM%\node_modules /E /NFL /NDL /NJH /NJS /NP >nul
if errorlevel 8 goto fail

echo [5/5] Zip maken...
powershell -NoProfile -Command "Compress-Archive -Path '%STAGE%\*' -DestinationPath 'publish\DMX-Light-Control-%VERSION%.zip' -Force" || goto fail
rem controlegetal: de app controleert hiermee een download bij automatisch bijwerken
powershell -NoProfile -Command "$h=(Get-FileHash 'publish\DMX-Light-Control-%VERSION%.zip' -Algorithm SHA256).Hash.ToLower(); [IO.File]::WriteAllText('publish\DMX-Light-Control-%VERSION%.zip.sha256', $h + '  DMX-Light-Control-%VERSION%.zip' + [Environment]::NewLine)" || goto fail

rem Map bijwerken. Een map hernoemen lukt niet als er een programma uit draait: zo weten we het
rem zeker voordat er iets weg gaat.
if exist %OUT% (
  move %OUT% %OUT%-oud >nul 2>&1
  if exist %OUT% (
    echo.
    echo Let op: er draait een DmxLicht uit %OUT%. Die map is niet aangeraakt.
    echo De nieuwe versie staat in %STAGE% en in de zip.
    goto done
  )
  if exist %OUT%-oud\Instellingen move %OUT%-oud\Instellingen %STAGE%\Instellingen >nul
)
rem Nieuwe versie zonder eigen Instellingen: kopie van de show uit de nieuwste vorige versie
rem (alleen kopieren: die versie mag blijven draaien en houdt zijn eigen show).
if not exist %STAGE%\Instellingen (
  for /f "usebackq delims=" %%d in (`powershell -NoProfile -Command "Get-ChildItem publish -Directory -Filter 'DmxLicht*' | Where-Object { $_.Name -notmatch '^_build|-oud$' -and (Test-Path (Join-Path $_.FullName 'Instellingen\show.json')) } | Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName"`) do (
    echo Show overgenomen uit %%d
    robocopy "%%d\Instellingen" "%STAGE%\Instellingen" /E /XD WebView2 /XF *.tmp /NFL /NDL /NJH /NJS /NP >nul
  )
)
move %STAGE% %OUT% >nul || goto fail
if exist %OUT%-oud rmdir /s /q %OUT%-oud

:done
rem gelukt: dit nummer bewaren (ook in de interface). De Companion-module houdt een vast nummer:
rem een verbinding in Companion staat vast op de versie waarmee hij is toegevoegd.
powershell -NoProfile -Command "[IO.File]::WriteAllText('version.txt', '%VERSION%' + [Environment]::NewLine); foreach ($f in 'ui\package.json') { $j=Get-Content $f -Raw; $j=$j -replace '\"version\": \"[0-9.]+\"', '\"version\": \"%VERSION%\"'; [IO.File]::WriteAllText((Resolve-Path $f), $j) }"
echo.
echo Klaar: publish\DMX-Light-Control-%VERSION%.zip
echo Uitpakken op de andere pc en DmxLicht.exe starten.
exit /b 0

:fail
echo.
echo Bouwen mislukt.
pause
exit /b 1
