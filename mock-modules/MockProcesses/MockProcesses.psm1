# Mock Get-Process and Stop-Process for Levels 3+. Shipped via
# QuestResource.MockModulePaths = ["res://mock-modules/MockProcesses"].
#
# The fake process table includes a few benign system-ish names plus the
# adversary process ("Sentinel.EDR") the player has to terminate in the boss.
# Stop-Process by Id removes from the table for the rest of the quest session
# so the player can see their own effect.

$script:MockProcessTable = @(
    [PSCustomObject]@{ Id = 4;    Name = 'kernel';           CPU = 12.4;  Description = 'OS kernel'; Path = '/System/Library/Kernels/kernel' }
    [PSCustomObject]@{ Id = 220;  Name = 'systemd';          CPU =  3.1;  Description = 'init'; Path = '/sbin/systemd' }
    [PSCustomObject]@{ Id = 644;  Name = 'pwsh';             CPU =  5.0;  Description = 'PowerShell host'; Path = '/usr/bin/pwsh' }
    [PSCustomObject]@{ Id = 1024; Name = 'Sentinel.EDR';     CPU = 28.7;  Description = 'Endpoint Detection (SentinelOne-like)'; Path = 'C:\Program Files\SentinelOne\sentinel.exe' }
    [PSCustomObject]@{ Id = 1337; Name = 'svc-pipeline-bot'; CPU =  0.8;  Description = 'internal automation'; Path = 'C:\bots\pipeline.exe' }
    [PSCustomObject]@{ Id = 2048; Name = 'chrome';           CPU = 41.2;  Description = 'Web browser'; Path = '/Applications/Google Chrome.app' }
    [PSCustomObject]@{ Id = 2049; Name = 'chrome-helper';    CPU =  3.4;  Description = 'Chrome helper'; Path = '/Applications/Google Chrome.app' }
    [PSCustomObject]@{ Id = 3300; Name = 'cartel-ledger';    CPU = 19.1;  Description = 'redacted'; Path = 'C:\Users\Public\ledger\ledger.exe' }
)

function Get-Process {
    [CmdletBinding(DefaultParameterSetName = 'All')]
    param(
        [Parameter(Position = 0, ParameterSetName = 'ByName')]
        [string[]] $Name,

        [Parameter(ParameterSetName = 'ById')]
        [int[]] $Id
    )
    switch ($PSCmdlet.ParameterSetName) {
        'ByName' {
            $script:MockProcessTable | Where-Object {
                $proc = $_
                $Name | Where-Object { $proc.Name -like $_ }
            }
        }
        'ById' {
            $script:MockProcessTable | Where-Object { $Id -contains $_.Id }
        }
        default {
            $script:MockProcessTable
        }
    }
}

function Stop-Process {
    [CmdletBinding(SupportsShouldProcess = $true, DefaultParameterSetName = 'ById')]
    param(
        [Parameter(Position = 0, ValueFromPipelineByPropertyName = $true, ParameterSetName = 'ById')]
        [int[]] $Id,

        [Parameter(ValueFromPipelineByPropertyName = $true, ParameterSetName = 'ByName')]
        [string[]] $Name,

        [Parameter(ValueFromPipeline = $true, ParameterSetName = 'ByObject')]
        [PSObject] $InputObject,

        [switch] $Force,
        [switch] $PassThru
    )
    process {
        # Emit a unique tombstone line per terminated process so quest objectives
        # can distinguish "you actually killed it" from "you just listed it."
        # The Information stream renders into the host output by default.
        function _emit-tombstone($proc) {
            Write-Host ('[X] TERMINATED {0} (PID {1})' -f $proc.Name, $proc.Id)
        }
        switch ($PSCmdlet.ParameterSetName) {
            'ById' {
                foreach ($targetPid in $Id) {
                    $proc = $script:MockProcessTable | Where-Object Id -EQ $targetPid | Select-Object -First 1
                    if ($null -ne $proc) {
                        $script:MockProcessTable = $script:MockProcessTable | Where-Object Id -NE $targetPid
                        _emit-tombstone $proc
                        if ($PassThru) { $proc }
                    }
                }
            }
            'ByName' {
                foreach ($n in $Name) {
                    $matches = $script:MockProcessTable | Where-Object Name -Like $n
                    foreach ($m in $matches) {
                        $script:MockProcessTable = $script:MockProcessTable | Where-Object Id -NE $m.Id
                        _emit-tombstone $m
                        if ($PassThru) { $m }
                    }
                }
            }
            'ByObject' {
                if ($null -ne $InputObject -and $null -ne $InputObject.Id) {
                    $tid = [int] $InputObject.Id
                    $proc = $script:MockProcessTable | Where-Object Id -EQ $tid | Select-Object -First 1
                    if ($null -ne $proc) {
                        $script:MockProcessTable = $script:MockProcessTable | Where-Object Id -NE $tid
                        _emit-tombstone $proc
                        if ($PassThru) { $proc }
                    }
                }
            }
        }
    }
}

Export-ModuleMember -Function Get-Process, Stop-Process
