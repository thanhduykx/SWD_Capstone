[CmdletBinding()]
param(
    [switch]$Staged,
    [switch]$Tracked
)

$ErrorActionPreference = 'Stop'

if ($Staged -and $Tracked) {
    throw 'Use either -Staged or -Tracked, not both.'
}

$repositoryRoot = (& git rev-parse --show-toplevel).Trim()
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repositoryRoot)) {
    throw 'This command must run inside a Git repository.'
}

$paths = if ($Staged) {
    @(& git diff --cached --name-only --diff-filter=ACMR)
} elseif ($Tracked) {
    @(& git ls-files)
} else {
    @(& git diff --name-only --diff-filter=ACMR)
}

$rules = @(
    @{
        Name = 'non-empty sensitive value in appsettings JSON'
        Path = '(?i)(^|/)appsettings(?:\.[^/]+)?\.json$'
        Pattern = '(?i)"(?:Password|ClientSecret|ApiKey|ServiceRoleKey)"\s*:\s*"[^"\s][^"]{7,}"'
    },
    @{
        Name = 'JWT signing key in appsettings JSON'
        Path = '(?i)(^|/)appsettings(?:\.[^/]+)?\.json$'
        Pattern = '(?i)"Key"\s*:\s*"[A-Za-z0-9+/_=-]{32,}"'
    },
    @{
        Name = 'credential-bearing database connection string'
        Path = '(?i)(^|/)appsettings(?:\.[^/]+)?\.json$'
        Pattern = '(?i)"(?:CpmsDatabase|DefaultConnection)"\s*:\s*"[^"]*(?:Password|Pwd)=[^";]+'
    },
    @{
        Name = 'known provider credential format'
        Path = '.*'
        Pattern = '(?i)(?:github_pat_[A-Za-z0-9_]{20,}|gh[pousr]_[A-Za-z0-9]{30,}|AIza[0-9A-Za-z_-]{30,}|sk-[A-Za-z0-9_-]{24,})'
    },
    @{
        Name = 'private key material'
        Path = '.*'
        Pattern = '-----BEGIN (?:RSA |EC |OPENSSH )?PRIVATE KEY-----'
    }
)

$findings = [System.Collections.Generic.List[string]]::new()
foreach ($path in $paths | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object -Unique) {
    $normalizedPath = $path -replace '\\', '/'
    if ($Staged) {
        $content = (& git show ":$path" 2>$null) -join "`n"
        if ($LASTEXITCODE -ne 0) { continue }
    } else {
        $absolutePath = Join-Path $repositoryRoot $path
        if (-not (Test-Path -LiteralPath $absolutePath -PathType Leaf)) { continue }
        try {
            $content = Get-Content -Raw -LiteralPath $absolutePath
        } catch {
            continue
        }
    }

    if ([string]::IsNullOrEmpty($content) -or $content.Contains([char]0)) { continue }

    foreach ($rule in $rules) {
        if ($normalizedPath -notmatch $rule.Path) { continue }
        $match = [regex]::Match($content, $rule.Pattern)
        if (-not $match.Success) { continue }
        $lineNumber = 1 + ([regex]::Matches($content.Substring(0, $match.Index), "`n")).Count
        $findings.Add("$normalizedPath`:$lineNumber - $($rule.Name)")
    }
}

if ($findings.Count -gt 0) {
    Write-Error ("Potential secrets detected. Values are redacted:`n - " + ($findings -join "`n - "))
    exit 1
}

Write-Host "Secret scan passed for $($paths.Count) file(s)."
