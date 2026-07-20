$ErrorActionPreference = "SilentlyContinue"
Stop-Process -Name explorer -Force
Start-Process explorer.exe
