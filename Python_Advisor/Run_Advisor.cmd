@echo off
cd /d "%~dp0"
python advisor.py
if errorlevel 1 goto failed
pause
exit /b 0
:failed
echo Python 3.10 or later is required. Check the error above.
pause
exit /b 1
