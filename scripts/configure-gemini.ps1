#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^gemini-[a-zA-Z0-9.-]+$')][string]$Model,
    [switch]$EnableCapture,
    [ValidateRange(1, 10000)][int]$MaxRequestsPerDay = 10,
    [ValidateRange(60, 86400)][int]$MinRequestIntervalSeconds = 60
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
try {
    Write-Host 'Usar una clave de un proyecto Free tier de Google AI Studio sin facturación vinculada.'
    Write-Host 'Este script no activa facturación ni crea servicios pagos. Confirmá las cuotas del modelo en AI Studio.'
    $gemini = Read-Host 'Clave de Gemini (no se muestra)' -AsSecureString
    $ocr = Read-Host 'Clave propia de OCR.space (no se muestra)' -AsSecureString
    $geminiValue = [System.Net.NetworkCredential]::new('', $gemini).Password
    $ocrValue = [System.Net.NetworkCredential]::new('', $ocr).Password
    if ([string]::IsNullOrWhiteSpace($geminiValue) -or [string]::IsNullOrWhiteSpace($ocrValue)) { throw 'Ambas claves son necesarias para validar el flujo completo.' }
    $settings = @{
        'Capture:Provider' = 'Gemini'
        'Capture:Model' = $Model
        'Capture:ApiKey' = $geminiValue
        'Capture:Enabled' = $EnableCapture.IsPresent.ToString().ToLowerInvariant()
        'Capture:MaxRequestsPerDay' = $MaxRequestsPerDay.ToString()
        'Capture:MinRequestIntervalSeconds' = $MinRequestIntervalSeconds.ToString()
        'Ocr:Enabled' = 'true'
        'Ocr:ApiKey' = $ocrValue
    }
    $settings | ConvertTo-Json -Compress | & dotnet user-secrets set --project backend/Offers.Api
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo guardar la configuración privada de Gemini/OCR.' }
    $settings = $null; $geminiValue = $null; $ocrValue = $null
    Write-Host 'Configuración guardada fuera del repositorio. Aplicá migraciones y reiniciá la API.'
    Write-Host 'La extracción externa solo queda verificada después de una ejecución real y la revisión de sus resultados.'
}
finally { Pop-Location }
