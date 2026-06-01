@echo off
title TallyG Tax  -  Launcher
color 0A
echo ============================================================
echo    TallyG Tax  -  starting on your computer
echo ============================================================
echo.
echo Two black windows will open and STAY open:
echo    1) "API"      - the engine
echo    2) "Website"  - the pages you see
echo Keep BOTH open while you use the site. Closing them stops it.
echo.

REM --- start the backend API (already built; no internet needed) ---
start "TallyG Tax - API  (keep this open)" cmd /k "set ASPNETCORE_ENVIRONMENT=Development& set ASPNETCORE_URLS=http://localhost:5080& dotnet D:\TallyGTax\backend\src\TallyG.Tax.Api\bin\Debug\net9.0\TallyG.Tax.Api.dll"

REM --- start the website (uses the newer Node 20) ---
start "TallyG Tax - Website  (keep this open)" cmd /k "C:\Users\kkgup\node20\node.exe D:\TallyGTax\frontend\_start.js"

echo Waiting ~15 seconds for the website to warm up...
timeout /t 15 /nobreak >nul

echo Opening the site in your browser...
start "" "http://localhost:3000"

echo.
echo ------------------------------------------------------------
echo  If the browser did NOT open, type this into your browser:
echo.
echo        http://localhost:3000
echo.
echo  To LOG IN:  type email   demo@itrhelp.com
echo              click "Send code"  -  a 6-digit code appears
echo              on the screen (test mode). Type it in. Done.
echo.
echo  (If a window mentions a port "in use", it is ALREADY
echo   running - just open http://localhost:3000 in your browser.)
echo ------------------------------------------------------------
echo.
echo You can close THIS window now. Keep the API + Website windows open.
pause
