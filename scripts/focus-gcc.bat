@echo off
setlocal
cd /d %~dp0
echo [focus-gcc] minimize all EqZero windows...
python .\tile\terminal_tiler.py EqZero --hide
echo [focus-gcc] bring and tile gcc windows...
python .\tile\terminal_tiler.py gcc
endlocal
