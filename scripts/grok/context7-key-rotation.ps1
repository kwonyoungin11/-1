#!/usr/bin/env pwsh
# Context7 API key rotation wrapper (PowerShell). Never prints key values.
$aliases = @(
  "CONTEXT7_API_KEY_1","CONTEXT7_API_KEY_2","CONTEXT7_API_KEY_3",
  "CONTEXT7_API_KEY_4","CONTEXT7_API_KEY_5","CONTEXT7_API_KEY_6"
)
$query = $args[0]
if (-not $query) {
  Write-Host 'usage: context7-key-rotation.ps1 "<docs query>"'
  exit 2
}
foreach ($alias in $aliases) {
  $val = [Environment]::GetEnvironmentVariable($alias)
  if ([string]::IsNullOrEmpty($val)) {
    Write-Host "skip: $alias (unset)"
    continue
  }
  Write-Host "try: $alias"
  Write-Host "Context7 request would use $alias"
  Write-Host "SUCCESS_ALIAS=$alias"
  exit 0
}
Write-Host "Context7 사용 실패: all aliases unset (values not printed)"
exit 1
