@echo off
setlocal
cd /d %~dp0

call python "%QUERCUS_DEVOPS%\watch-and-run-tests.py" .. %1