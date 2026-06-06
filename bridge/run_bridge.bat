@echo off
REM Launches the IsaacSimSharp bridge inside Isaac Sim.
REM Override the Isaac Sim location with: set ISAACSIM_HOME=D:\path\to\isaacsim
setlocal
set "BRIDGE_DIR=%~dp0"
if "%ISAACSIM_HOME%"=="" set "ISAACSIM_HOME=C:\isaacsim"
set "PYTHONPATH=%BRIDGE_DIR%;%PYTHONPATH%"
call "%ISAACSIM_HOME%\python.bat" -m isaacsim_bridge %*
endlocal
