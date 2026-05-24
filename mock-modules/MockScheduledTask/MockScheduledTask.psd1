@{
    ModuleVersion       = '0.1.0'
    GUID                = '55555555-aaaa-bbbb-cccc-666666666666'
    Author              = 'hacker-game'
    Description         = 'Mock Register-ScheduledTask / Get-ScheduledTask / Unregister-ScheduledTask plus the action+trigger builders, for hacker-game persistence quests.'
    RootModule          = 'MockScheduledTask.psm1'
    FunctionsToExport   = @('Register-ScheduledTask', 'Get-ScheduledTask', 'Unregister-ScheduledTask', 'New-ScheduledTaskAction', 'New-ScheduledTaskTrigger')
    CmdletsToExport     = @()
    AliasesToExport     = @()
    VariablesToExport   = @()
}
