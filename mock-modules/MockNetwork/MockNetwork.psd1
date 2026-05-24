@{
    ModuleVersion       = '0.1.0'
    GUID                = '44444444-aaaa-bbbb-cccc-555555555555'
    Author              = 'hacker-game'
    Description         = 'Mock network cmdlets (Test-NetConnection / Resolve-DnsName / Invoke-WebRequest / Invoke-RestMethod) for hacker-game quests.'
    RootModule          = 'MockNetwork.psm1'
    FunctionsToExport   = @('Resolve-DnsName', 'Test-NetConnection', 'Invoke-WebRequest', 'Invoke-RestMethod')
    CmdletsToExport     = @()
    AliasesToExport     = @()
    VariablesToExport   = @()
}
