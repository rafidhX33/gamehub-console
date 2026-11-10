# Run in the affected user's Windows session. Does not touch machine-wide shell.
$ErrorActionPreference = 'Stop'
$policy = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Policies\System'
$marker = Get-ItemProperty 'HKCU:\Software\GameHubConsole' -ErrorAction SilentlyContinue
$current = (Get-ItemProperty $policy -ErrorAction SilentlyContinue).Shell
if ($marker.ShellCommand -and $current -eq $marker.ShellCommand) {
    Remove-ItemProperty $policy -Name Shell
    Write-Host 'GameHub custom shell disabled for the next sign-in.'
} else {
    Write-Host 'No GameHub-owned shell policy found. Existing policy retained.'
}
Start-Process explorer.exe
