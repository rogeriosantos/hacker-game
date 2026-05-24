# Mock Get-Service / Start-Service / Stop-Service for Levels 3+.

$script:MockServiceTable = @(
    [PSCustomObject]@{ Name = 'SentinelAgent';     DisplayName = 'SentinelOne Agent';            Status = 'Running'; StartType = 'Automatic' }
    [PSCustomObject]@{ Name = 'WindowsDefender';   DisplayName = 'Microsoft Defender';           Status = 'Running'; StartType = 'Automatic' }
    [PSCustomObject]@{ Name = 'OBSIDIAN.Sync';     DisplayName = 'OBSIDIAN Background Sync';     Status = 'Running'; StartType = 'Automatic' }
    [PSCustomObject]@{ Name = 'wuauserv';          DisplayName = 'Windows Update';               Status = 'Stopped'; StartType = 'Manual' }
    [PSCustomObject]@{ Name = 'BITS';              DisplayName = 'Background Intelligent Xfer';  Status = 'Running'; StartType = 'Automatic' }
    [PSCustomObject]@{ Name = 'CartelLedger.Svc';  DisplayName = 'Cartel Ledger Daemon';         Status = 'Running'; StartType = 'Automatic' }
)

function Get-Service {
    [CmdletBinding()]
    param(
        [Parameter(Position = 0)]
        [string[]] $Name
    )
    if ($Name) {
        $script:MockServiceTable | Where-Object {
            $svc = $_
            $Name | Where-Object { $svc.Name -like $_ }
        }
    } else {
        $script:MockServiceTable
    }
}

function Set-MockServiceStatus([string] $name, [string] $status) {
    $script:MockServiceTable = $script:MockServiceTable | ForEach-Object {
        if ($_.Name -eq $name) {
            [PSCustomObject]@{ Name = $_.Name; DisplayName = $_.DisplayName; Status = $status; StartType = $_.StartType }
        } else { $_ }
    }
}

function Stop-Service {
    [CmdletBinding()]
    param(
        [Parameter(Position = 0, ValueFromPipelineByPropertyName = $true)]
        [string[]] $Name,
        [switch] $Force,
        [switch] $PassThru
    )
    process {
        foreach ($n in $Name) {
            $matches = $script:MockServiceTable | Where-Object { $_.Name -like $n }
            foreach ($m in $matches) {
                Set-MockServiceStatus $m.Name 'Stopped'
                if ($PassThru) { $script:MockServiceTable | Where-Object Name -EQ $m.Name }
            }
        }
    }
}

function Start-Service {
    [CmdletBinding()]
    param(
        [Parameter(Position = 0, ValueFromPipelineByPropertyName = $true)]
        [string[]] $Name,
        [switch] $PassThru
    )
    process {
        foreach ($n in $Name) {
            $matches = $script:MockServiceTable | Where-Object { $_.Name -like $n }
            foreach ($m in $matches) {
                Set-MockServiceStatus $m.Name 'Running'
                if ($PassThru) { $script:MockServiceTable | Where-Object Name -EQ $m.Name }
            }
        }
    }
}

Export-ModuleMember -Function Get-Service, Stop-Service, Start-Service
