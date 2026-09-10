# Run once in an elevated PowerShell window (Run as administrator).
# Allows phones on the same LAN to reach Metro (default port 8081).

param(
  [int]$Port = 8081
)

$ruleName = "Expo Metro $Port"

$existing = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
if ($existing) {
  Write-Host "Firewall rule already exists: $ruleName"
  exit 0
}

New-NetFirewallRule `
  -DisplayName $ruleName `
  -Direction Inbound `
  -Action Allow `
  -Protocol TCP `
  -LocalPort $Port `
  -Profile Private

Write-Host "Created inbound firewall rule for TCP port $Port on Private networks."
