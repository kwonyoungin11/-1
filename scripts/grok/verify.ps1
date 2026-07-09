#!/usr/bin/env pwsh
$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "../..")
Write-Host "== TradingBot verify harness (pwsh) =="
bash ./scripts/grok/verify.sh
