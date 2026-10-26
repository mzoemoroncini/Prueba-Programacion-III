#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$MySqlServer = '127.0.0.1',
    [ValidateRange(1, 65535)][int]$MySqlPort = 3306,
    [string]$Database = 'offers',
    [switch]$InstallBrowser
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
Push-Location $repoRoot
$previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
try {
    foreach ($tool in @('dotnet', 'npm')) {
        if (-not (Get-Command $tool -ErrorAction SilentlyContinue)) { throw "Falta $tool en PATH. Revisá los requisitos del README." }
    }
    & dotnet restore DescuentosIA.sln
    if ($LASTEXITCODE -ne 0) { throw 'Falló la restauración de NuGet.' }
    & dotnet build DescuentosIA.sln --no-restore -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación de C#.' }
    & npm --prefix frontend ci
    if ($LASTEXITCODE -ne 0) { throw 'Falló la instalación de npm.' }
    & npm --prefix frontend run build
    if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación de React.' }

    Write-Host 'La base y el usuario MySQL deben existir. Ver deployment/create-database.sql.example.'
    $mysqlCredential = Get-Credential -Message 'Usuario y contraseña de la base MySQL de desarrollo'
    if ($null -eq $mysqlCredential) { throw 'Se canceló la configuración de MySQL.' }
    $adminUsername = Read-Host 'Nombre del administrador de la aplicación'
    if ([string]::IsNullOrWhiteSpace($adminUsername)) { throw 'El usuario administrador es obligatorio.' }
    $adminPassword = Read-Host 'Contraseña del administrador (mínimo 12 caracteres)' -AsSecureString
    $plainPassword = [System.Net.NetworkCredential]::new('', $adminPassword).Password
    if ($plainPassword.Length -lt 12) { throw 'La contraseña administrativa debe tener al menos 12 caracteres.' }
    $connection = [System.Data.Common.DbConnectionStringBuilder]::new()
    $connection['Server'] = $MySqlServer
    $connection['Port'] = $MySqlPort.ToString()
    $connection['Database'] = $Database
    $connection['User'] = $mysqlCredential.UserName
    $connection['Password'] = $mysqlCredential.GetNetworkCredential().Password
    # El JSON se entrega por stdin. No se incluyen secretos en argumentos ni en el repositorio.
    $settings = @{
        'ConnectionStrings:Offers' = $connection.ConnectionString
        'Admin:Username' = $adminUsername
        'Admin:Password' = $plainPassword
        'Capture:Enabled' = 'false'
    }
    $settings | ConvertTo-Json -Compress | & dotnet user-secrets set --project backend/Offers.Api
    if ($LASTEXITCODE -ne 0) { throw 'No se pudieron guardar los User Secrets.' }
    $plainPassword = $null; $settings = $null; $connection = $null
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    & dotnet run --project backend/Offers.Api --no-build -- --migrate
    if ($LASTEXITCODE -ne 0) { throw 'Fallaron las migraciones. Revisá la conexión, los permisos y el servicio MySQL.' }
    if ($InstallBrowser) {
        & pwsh backend/Offers.Api/bin/Debug/net10.0/playwright.ps1 install chromium --only-shell
        if ($LASTEXITCODE -ne 0) { throw 'No se pudo instalar Chromium para la extracción.' }
    }
    Write-Host 'Preparación completa. Abrí DescuentosIA.sln y ejecutá Offers.Api. Iniciá React con npm --prefix frontend run dev.'
    Write-Host 'IA y OCR requieren claves propias: no se consideran verificados por este script.'
}
finally {
    $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment
    Pop-Location
}
