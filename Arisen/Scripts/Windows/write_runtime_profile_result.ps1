$ErrorActionPreference = "Stop"

function Get-NullableInt {
    param([string]$Value)
    if ([string]::IsNullOrEmpty($Value)) { return $null }
    return [int]$Value
}

function Get-NullableBool {
    param([string]$Value)
    if ([string]::IsNullOrEmpty($Value)) { return $null }
    return $Value -eq "1"
}

function Get-NullableString {
    param([string]$Value)
    if ([string]::IsNullOrEmpty($Value)) { return $null }
    return $Value
}

$result = [ordered]@{
    profile = $env:RESULT_PROFILE
    status = $env:RESULT_STATUS
    smokeMode = $env:SMOKE_MODE
    requiresVulkan = $env:RESULT_REQUIRES_VULKAN -eq "1"
    exitCode = Get-NullableInt $env:RESULT_EXIT_CODE
    logPath = Get-NullableString $env:RESULT_LOG_PATH
    visualSummary = [ordered]@{
        requested = $env:CURRENT_VISUAL_SUMMARY_REQUESTED -eq "1"
        path = Get-NullableString $env:CURRENT_VISUAL_SUMMARY_PATH
        passed = Get-NullableBool $env:CURRENT_VISUAL_SUMMARY_PASSED
    }
    worldStreaming = [ordered]@{
        requested = $env:CURRENT_WORLD_STREAMING_REQUESTED -eq "1"
        summaryPath = Get-NullableString $env:CURRENT_WORLD_STREAMING_SUMMARY_PATH
        visualBasePath = Get-NullableString $env:CURRENT_WORLD_STREAMING_VISUAL_BASE_PATH
        logPath = Get-NullableString $env:CURRENT_WORLD_STREAMING_LOG_PATH
        exitCode = Get-NullableInt $env:CURRENT_WORLD_STREAMING_EXIT_CODE
        passed = Get-NullableBool $env:CURRENT_WORLD_STREAMING_PASSED
    }
    editorViewportSmoke = [ordered]@{
        requested = $env:CURRENT_EDITOR_VIEWPORT_SMOKE_REQUESTED -eq "1"
        path = Get-NullableString $env:CURRENT_EDITOR_VIEWPORT_SMOKE_PATH
        logPath = Get-NullableString $env:CURRENT_EDITOR_VIEWPORT_SMOKE_LOG_PATH
        exitCode = Get-NullableInt $env:CURRENT_EDITOR_VIEWPORT_SMOKE_EXIT_CODE
        passed = Get-NullableBool $env:CURRENT_EDITOR_VIEWPORT_SMOKE_PASSED
    }
    relocatedProduction = [ordered]@{
        requested = $env:CURRENT_RELOCATED_PRODUCTION_REQUESTED -eq "1"
        summaryPath = Get-NullableString $env:CURRENT_RELOCATED_PRODUCTION_SUMMARY_PATH
        logPath = Get-NullableString $env:CURRENT_RELOCATED_PRODUCTION_LOG_PATH
        exitCode = Get-NullableInt $env:CURRENT_RELOCATED_PRODUCTION_EXIT_CODE
        passed = Get-NullableBool $env:CURRENT_RELOCATED_PRODUCTION_PASSED
    }
    message = Get-NullableString $env:RESULT_MESSAGE
}

$json = $result | ConvertTo-Json -Depth 6 -Compress
Add-Content -LiteralPath $env:PROFILE_RESULTS_JSONL -Value $json
