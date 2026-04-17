# ========================================================
# SCRIPT DE COMPILACION MAESTRO - AUTOCONTENIDO (ONE FILE)
# ========================================================
$ErrorActionPreference = "Stop"
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "     GENERANDO ENTREGABLES FINALES        " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $scriptPath

# 1. Limpiar carpeta de despliegue
$deployDir = ".\Deploy"
if (Test-Path $deployDir) { Remove-Item -Recurse -Force $deployDir }
New-Item -ItemType Directory -Force -Path $deployDir | Out-Null

$RID = "win-x64"

# --- A) COMPILAR SERVIDOR (API) ---
Write-Host "1. Compilando SERVIDOR (API)..." -ForegroundColor Yellow
dotnet publish .\LabControl.Api\LabControl.Api.csproj -c Release -r $RID --self-contained true -o "$deployDir\Servidor"

# --- B) COMPILAR ADMINISTRADOR ---
Write-Host "2. Compilando ADMINISTRADOR..." -ForegroundColor Yellow
dotnet publish .\LabControl.Admin\LabControl.Admin.csproj -c Release -r $RID --self-contained true -o "$deployDir\Admin"

# --- C) COMPILAR AGENTE ---
Write-Host "3. Compilando AGENTE..." -ForegroundColor Yellow
dotnet publish .\LabAgent\LabAgent.csproj -c Release -r $RID --self-contained true -o "$deployDir\Agente"

# --- 4. LIMPIEZA POST-COMPILACION ---
Write-Host "4. Organizando archivos..." -ForegroundColor Green
Get-ChildItem -Path $deployDir -Recurse -Filter "*.pdb" | Remove-Item -Force

Write-Host "`n==========================================" -ForegroundColor Green
Write-Host "      ¡COMPILACION FINAL EXITOSA!" -ForegroundColor Green
Write-Host " Los archivos estan en la carpeta \Deploy" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
