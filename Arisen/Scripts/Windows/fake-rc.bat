@echo off
setlocal

REM === Ensure console code page matches localized tool output ===
for /f "tokens=2 delims=:" %%I in ('chcp') do set "ORIGINAL_CP=%%I"
set "ORIGINAL_CP=%ORIGINAL_CP: =%"
if defined ARISEN_CODEPAGE (
    chcp %ARISEN_CODEPAGE% >nul
) else (
    chcp 936 >nul
)

REM Fake rc.exe to bypass resource compilation
set "EXIT_CODE=0"

if defined ORIGINAL_CP chcp %ORIGINAL_CP% >nul
endlocal
exit /b %EXIT_CODE%