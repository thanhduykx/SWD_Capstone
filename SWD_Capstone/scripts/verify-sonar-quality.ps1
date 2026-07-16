[CmdletBinding()]
param(
    [string]$HostUrl = "https://sonarcloud.io",
    [string]$ProjectKey = $env:SONAR_PROJECT_KEY,
    [double]$MinimumOverallCoverage = 20,
    [double]$MinimumNewCodeCoverage = 80
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($env:SONAR_TOKEN)) {
    throw "SONAR_TOKEN is required. Configure it as a masked CI secret."
}
if ([string]::IsNullOrWhiteSpace($ProjectKey)) {
    throw "SONAR_PROJECT_KEY is required."
}

$credentials = [Convert]::ToBase64String(
    [Text.Encoding]::ASCII.GetBytes("$($env:SONAR_TOKEN):"))
$headers = @{ Authorization = "Basic $credentials" }
$metricKeys = @(
    "bugs",
    "vulnerabilities",
    "code_smells",
    "coverage",
    "new_bugs",
    "new_vulnerabilities",
    "new_code_smells",
    "new_security_hotspots",
    "new_coverage"
) -join ","
$encodedProjectKey = [Uri]::EscapeDataString($ProjectKey)
$uri = "$HostUrl/api/measures/component?component=$encodedProjectKey&metricKeys=$metricKeys"
$response = Invoke-RestMethod -Uri $uri -Headers $headers -Method Get
$measures = @{}
foreach ($measure in $response.component.measures) {
    $value = if ($measure.periods -and $measure.periods.Count -gt 0) {
        $measure.periods[0].value
    }
    else {
        $measure.value
    }
    $measures[$measure.metric] = [double]::Parse(
        $value,
        [Globalization.CultureInfo]::InvariantCulture)
}

function Get-MeasureValue {
    param([string]$Metric, [double]$DefaultValue = 0)
    if ($measures.ContainsKey($Metric)) { return $measures[$Metric] }
    return $DefaultValue
}

$failures = [Collections.Generic.List[string]]::new()
foreach ($metric in @("bugs", "vulnerabilities", "code_smells", "new_bugs", "new_vulnerabilities", "new_code_smells", "new_security_hotspots")) {
    $value = Get-MeasureValue -Metric $metric
    if ($value -gt 0) { $failures.Add("$metric must be 0, actual: $value") }
}

$overallCoverage = Get-MeasureValue -Metric "coverage"
if ($overallCoverage -lt $MinimumOverallCoverage) {
    $failures.Add("coverage must be at least $MinimumOverallCoverage%, actual: $overallCoverage%")
}
$newCodeCoverage = Get-MeasureValue -Metric "new_coverage" -DefaultValue 100
if ($newCodeCoverage -lt $MinimumNewCodeCoverage) {
    $failures.Add("new_coverage must be at least $MinimumNewCodeCoverage%, actual: $newCodeCoverage%")
}

if ($failures.Count -gt 0) {
    throw "Repository quality gate failed:`n - $($failures -join "`n - ")"
}

Write-Host "Repository quality gate passed: zero issues, coverage $overallCoverage%, new-code coverage $newCodeCoverage%."
