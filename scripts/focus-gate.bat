@echo off
setlocal
cd /d %~dp0
echo [focus-gate] minimize all EqZero windows...
python .\tile\terminal_tiler.py EqZero --hide
echo [focus-gate] bring and tile gate windows...
python .\tile\terminal_tiler.py gate
endlocal
