@echo off
set "EXE=%ProgramFiles%\StudyGoodAndGood\StudyGoodAndGood.exe"
if not exist "%EXE%" (
  echo 未检测到已安装的学习好程序，无需卸载。
  pause
  exit /b
)
"%EXE%" --uninstall
