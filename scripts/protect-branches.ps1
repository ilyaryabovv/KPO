$ErrorActionPreference = "Stop"

$repo = "ilyaryabovv/KPO"
$contexts = @(
    "CI / commitlint",
    "CI / lint",
    "CI / test",
    "CI / build"
)

$protection = @{
    required_status_checks = @{
        strict = $true
        contexts = $contexts
    }
    enforce_admins = $true
    required_pull_request_reviews = @{
        dismissal_restrictions = @{}
        dismiss_stale_reviews = $false
        require_code_owner_reviews = $false
        required_approving_review_count = 0
    }
    restrictions = $null
    allow_force_pushes = $false
    allow_deletions = $false
}

$path = Join-Path $PSScriptRoot "branch-protection.json"
$protection | ConvertTo-Json -Depth 6 | Set-Content -Path $path -Encoding utf8

foreach ($branch in @("main", "develop")) {
    Write-Host "Protecting $branch"
    & gh api --method PUT "repos/$repo/branches/$branch/protection" --input $path
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to protect $branch"
    }
}

Write-Host "Branch protection updated for main and develop."
