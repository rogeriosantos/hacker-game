# Mock Test-NetConnection / Resolve-DnsName / Invoke-WebRequest /
# Invoke-RestMethod for Levels 4+. The "network" is a static map of hosts ->
# (IP, open TCP ports, HTTP responses). All cross-platform; no real network
# calls. Lets quests drill recon patterns deterministically.

$script:MockDns = @{
    'target.obsidian.internal'      = '10.4.7.220'
    'crl.obsidian.internal'         = '10.4.7.221'
    'auth.obsidian.internal'        = '10.4.7.222'
    'jumpbox-east.cartel.local'     = '192.168.99.50'
    'ledger-db.cartel.local'        = '192.168.99.51'
    'dc01.federalhq.gov'            = '10.10.1.100'
    'localhost'                     = '127.0.0.1'
}

$script:MockOpenPorts = @{
    'target.obsidian.internal'  = @(22, 80, 443, 8080)
    'crl.obsidian.internal'     = @(443, 8443)
    'auth.obsidian.internal'    = @(443, 9443)
    'jumpbox-east.cartel.local' = @(22, 3389)
    'ledger-db.cartel.local'    = @(5432)
    'dc01.federalhq.gov'        = @(53, 88, 135, 389, 445, 636)
    'localhost'                 = @(22, 80)
}

# Each HTTP record: { StatusCode = 200; Headers = @{...}; Content = '...' }
$script:MockHttp = @{
    'http://target.obsidian.internal/' = @{
        StatusCode = 200
        Headers    = @{ 'Server' = 'obsidian-edge/4.2'; 'Content-Type' = 'text/html' }
        Content    = '<html><head><title>OBSIDIAN target node</title></head><body><h1>Welcome to node-220</h1><pre>X-Build: 2026.05.14-rc3</pre></body></html>'
    }
    'http://target.obsidian.internal/banner' = @{
        StatusCode = 200
        Headers    = @{ 'Server' = 'obsidian-edge/4.2' }
        Content    = "SSH-2.0-OpenSSH_9.6  build=obsidian-fork-rc3  node=target-220"
    }
    'https://crl.obsidian.internal/v2/auth' = @{
        StatusCode = 401
        Headers    = @{ 'WWW-Authenticate' = 'Bearer realm=obsidian-crl' }
        Content    = '{"error":"missing_credentials","hint":"send X-Token header"}'
    }
    'https://auth.obsidian.internal/api/version' = @{
        StatusCode = 200
        Headers    = @{ 'Content-Type' = 'application/json' }
        Content    = '{"version":"obsidian-auth/4.2.1","build":"rc3","region":"eu-central-1","capabilities":["totp","webauthn","gateway-relay"]}'
    }
    'https://auth.obsidian.internal/api/status' = @{
        StatusCode = 200
        Headers    = @{ 'Content-Type' = 'application/json' }
        Content    = '{"status":"degraded","online_nodes":3,"offline_nodes":["node-221","node-228"]}'
    }
}

function Resolve-DnsName {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [string] $Name,

        [Parameter()]
        [ValidateSet('A', 'AAAA', 'CNAME', 'MX', 'TXT')]
        [string] $Type = 'A'
    )
    if ($script:MockDns.ContainsKey($Name)) {
        [PSCustomObject]@{
            Name      = $Name
            Type      = $Type
            TTL       = 300
            Section   = 'Answer'
            IPAddress = $script:MockDns[$Name]
        }
    } else {
        Write-Error "Resolve-DnsName : $Name : DNS name does not exist"
    }
}

function Test-NetConnection {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [string] $ComputerName,

        [Parameter()]
        [int] $Port,

        [Parameter()]
        [switch] $InformationLevel
    )

    $resolved = if ($script:MockDns.ContainsKey($ComputerName)) { $script:MockDns[$ComputerName] } else { $null }
    $openPorts = if ($script:MockOpenPorts.ContainsKey($ComputerName)) { $script:MockOpenPorts[$ComputerName] } else { @() }

    if (-not $resolved) {
        [PSCustomObject]@{
            ComputerName       = $ComputerName
            RemoteAddress      = $null
            PingSucceeded      = $false
            TcpTestSucceeded   = $false
            NameResolutionFail = $true
        }
        return
    }

    if ($PSBoundParameters.ContainsKey('Port')) {
        $isOpen = $openPorts -contains $Port
        [PSCustomObject]@{
            ComputerName     = $ComputerName
            RemoteAddress    = $resolved
            RemotePort       = $Port
            PingSucceeded    = $true
            TcpTestSucceeded = $isOpen
        }
    } else {
        [PSCustomObject]@{
            ComputerName     = $ComputerName
            RemoteAddress    = $resolved
            PingSucceeded    = $true
            TcpTestSucceeded = $false
        }
    }
}

function Invoke-WebRequest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [Alias('Url')]
        [string] $Uri,

        [Parameter()]
        [string] $Method = 'GET',

        [Parameter()]
        [hashtable] $Headers
    )
    if ($script:MockHttp.ContainsKey($Uri)) {
        $r = $script:MockHttp[$Uri]
        [PSCustomObject]@{
            StatusCode        = $r.StatusCode
            StatusDescription = if ($r.StatusCode -eq 200) { 'OK' } elseif ($r.StatusCode -eq 401) { 'Unauthorized' } else { 'OK' }
            Content           = $r.Content
            Headers           = $r.Headers
            RawContentLength  = ($r.Content).Length
        }
    } else {
        Write-Error "Invoke-WebRequest : Unable to connect to the remote server: $Uri"
    }
}

function Invoke-RestMethod {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [string] $Uri,

        [Parameter()]
        [string] $Method = 'GET',

        [Parameter()]
        [hashtable] $Headers
    )
    if ($script:MockHttp.ContainsKey($Uri)) {
        $r = $script:MockHttp[$Uri]
        try {
            $r.Content | ConvertFrom-Json
        } catch {
            # not JSON — return raw
            $r.Content
        }
    } else {
        Write-Error "Invoke-RestMethod : Unable to connect to the remote server: $Uri"
    }
}

Export-ModuleMember -Function Resolve-DnsName, Test-NetConnection, Invoke-WebRequest, Invoke-RestMethod
