@echo off
chcp 65001 >nul
title Omni-Edu 9B 教育助手 (本机4090)
cd /d "%~dp0"

echo ============================================
echo   Omni-Edu 9B 教育助手
echo   模型加载约需1-3分钟，请耐心等待
echo   加载完成后会自动打开浏览器页面
echo   用完后直接关闭本窗口即可退出
echo ============================================
echo.

rem 后台监听：服务就绪后自动打开浏览器
start "" /min powershell -WindowStyle Hidden -Command "while($true){try{$r=Invoke-WebRequest -Uri 'http://127.0.0.1:8080/health' -UseBasicParsing -TimeoutSec 2; if($r.StatusCode -eq 200){break}}catch{}; Start-Sleep -Seconds 3}; Start-Process 'http://127.0.0.1:8080'"

"%~dp0runtime\llama.cpp\llama-server.exe" ^
  --model "%~dp0models\Omni-Edu-9B.Q6_K.gguf" ^
  --mmproj "%~dp0models\Omni-Edu-9B.mmproj-bf16.gguf" ^
  --n-gpu-layers 99 ^
  --ctx-size 32768 ^
  --image-min-tokens 1024 ^
  --port 8080 ^
  --host 127.0.0.1

pause
