# Mock Register-ScheduledTask / Get-ScheduledTask / Unregister-ScheduledTask
# for Level 5+. The task table is in-process (lives only as long as this
# runspace's import) so each quest sandbox starts clean.
#
# Functions emit a unique marker line ([X] REGISTERED ... / [X] UNREGISTERED ...)
# so quest objectives can gate on "you actually registered" vs "you just listed."

$script:MockTaskTable = @()

function Register-ScheduledTask {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $TaskName,

        [Parameter()]
        [string] $TaskPath = '\',

        [Parameter()]
        [string] $Description = '',

        [Parameter(Mandatory = $true)]
        [object] $Action,

        [Parameter()]
        [object] $Trigger,

        [Parameter()]
        [string] $User = 'SYSTEM',

        [Parameter()]
        [switch] $Force
    )
    # Refuse a duplicate unless -Force.
    $existing = $script:MockTaskTable | Where-Object TaskName -EQ $TaskName | Select-Object -First 1
    if ($existing -and -not $Force) {
        Write-Error "Register-ScheduledTask : A task with the name '$TaskName' already exists. Use -Force to overwrite."
        return
    }
    if ($existing) {
        $script:MockTaskTable = $script:MockTaskTable | Where-Object TaskName -NE $TaskName
    }

    # Pull a readable summary out of the Action object. Real New-ScheduledTaskAction
    # returns a CIM object; we just stringify whatever shape comes in.
    $actionSummary = if ($Action -is [string]) { $Action } else { "$Action" }

    $task = [PSCustomObject]@{
        TaskName    = $TaskName
        TaskPath    = $TaskPath
        Description = $Description
        Action      = $actionSummary
        Trigger     = if ($null -ne $Trigger) { "$Trigger" } else { $null }
        User        = $User
        State       = 'Ready'
        RegisteredAt = (Get-Date).ToString('o')
    }
    $script:MockTaskTable += $task
    Write-Host ('[X] REGISTERED ScheduledTask "{0}" runs: {1}' -f $TaskName, $actionSummary)
    $task
}

function Get-ScheduledTask {
    [CmdletBinding()]
    param(
        [Parameter(Position = 0)]
        [string] $TaskName,

        [Parameter()]
        [string] $TaskPath
    )
    $result = $script:MockTaskTable
    if ($TaskName) {
        $result = $result | Where-Object { $_.TaskName -like $TaskName }
    }
    if ($TaskPath) {
        $result = $result | Where-Object { $_.TaskPath -like $TaskPath }
    }
    $result
}

function Unregister-ScheduledTask {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0, ValueFromPipelineByPropertyName = $true)]
        [string] $TaskName,

        [switch] $Confirm
    )
    process {
        $existing = $script:MockTaskTable | Where-Object TaskName -EQ $TaskName | Select-Object -First 1
        if ($existing) {
            $script:MockTaskTable = $script:MockTaskTable | Where-Object TaskName -NE $TaskName
            Write-Host ('[X] UNREGISTERED ScheduledTask "{0}"' -f $TaskName)
        }
    }
}

function New-ScheduledTaskAction {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string] $Execute,
        [Parameter()]
        [string] $Argument = ''
    )
    if ($Argument) { "$Execute $Argument" } else { "$Execute" }
}

function New-ScheduledTaskTrigger {
    [CmdletBinding()]
    param(
        [switch] $AtStartup,
        [switch] $AtLogOn,
        [datetime] $At,
        [switch] $Daily
    )
    if ($AtStartup) { 'AT_STARTUP' }
    elseif ($AtLogOn) { 'AT_LOGON' }
    elseif ($Daily -and $At) { "DAILY_AT_$($At.ToString('HH:mm'))" }
    elseif ($At) { "ONCE_AT_$($At.ToString('o'))" }
    else { 'AT_STARTUP' }
}

Export-ModuleMember -Function Register-ScheduledTask, Get-ScheduledTask, Unregister-ScheduledTask, New-ScheduledTaskAction, New-ScheduledTaskTrigger
