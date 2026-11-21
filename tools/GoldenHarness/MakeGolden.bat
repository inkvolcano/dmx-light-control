@echo off
rem Builds the ORIGINAL v1.4 source together with the golden harness (old .NET Framework compiler)
rem and writes the reference frames for the parity tests.
setlocal
cd /d "%~dp0"
set CSC=%windir%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%windir%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist out mkdir out
"%CSC%" /nologo /codepage:65001 /target:exe /main:DmxLicht.GoldenHarness /out:out\GoldenHarness.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll ..\..\original_v1.4\DmxLicht_v1.4.cs GoldenHarness.cs
if errorlevel 1 exit /b 1
if not exist ..\..\tests\DmxLicht.Tests\golden mkdir ..\..\tests\DmxLicht.Tests\golden
out\GoldenHarness.exe ..\..\tests\DmxLicht.Tests\golden\frames.txt
