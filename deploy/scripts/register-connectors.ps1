param(
    [string]$ConnectUrl = "http://localhost:8083",
    [string]$ConnectorName = "inventory-connector",
    [string]$ConfigPath = "$PSScriptRoot/../connectors/mysql-inventory.config.json"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $ConfigPath)) {
    throw "Connector config file was not found: $ConfigPath"
}

$config = Get-Content -Raw -LiteralPath $ConfigPath
$uri = "$ConnectUrl/connectors/$ConnectorName/config"

Invoke-RestMethod `
    -Method Put `
    -Uri $uri `
    -ContentType "application/json" `
    -Body $config | Out-Null

Write-Host "$ConnectorName registered at $ConnectUrl"
