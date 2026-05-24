@{
    ModuleVersion       = '0.1.0'
    GUID                = '33333333-aaaa-bbbb-cccc-444444444444'
    Author              = 'hacker-game'
    Description         = 'Mock Get-Service / Start-Service / Stop-Service for hacker-game quests.'
    RootModule          = 'MockServices.psm1'
    FunctionsToExport   = @('Get-Service', 'Start-Service', 'Stop-Service')
    CmdletsToExport     = @()
    AliasesToExport     = @()
    VariablesToExport   = @()
}
