param([string]$OutputPath = 'Logs/TownRetailManagedResults.json')

# Invokes only the pure managed regression methods listed below. This does not
# run the Unity test lifecycle, scene tests, rendering, or PlayerPrefs native calls.
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$runtimePath = Join-Path $projectRoot 'Temp/bin/Debug/AzureDreams.Runtime.dll'
$testsPath = Join-Path $projectRoot 'Temp/bin/Debug/AzureDreams.Reimagined.PlayModeTests.dll'
[void][Reflection.Assembly]::LoadFrom((Join-Path $projectRoot 'Library/PackageCache/com.unity.ext.nunit@0198eae3b53e/net472/unity-custom/nunit.framework.dll'))
[void][Reflection.Assembly]::LoadFrom($runtimePath)
$assembly = [Reflection.Assembly]::LoadFrom($testsPath)
$fixtureType = $assembly.GetType('TownInteractionIntegrationTests', $true)
$fixture = [Activator]::CreateInstance($fixtureType)
$methodNames = @(
    'RetailTownScheduler_UsesRetailRowAndDispatchOrder',
    'RetailTownScheduler_PoolEventGuardResetsThenStops',
    'RetailTownScheduler_EnabledEntryRequiresItsConditionFlagSet',
    'RetailTownScheduler_FullScanAtCursorFiveOmitsFirstTrailingState',
    'RetailStorySelector_SeroEndingBranchHasHighestPriority',
    'RetailStorySelector_StoresVeldilEntryAndSelectsItsMode',
    'RetailStorySelector_UsesPairedEntryThenAdvancesWhenNoMatch',
    'RetailStorySelector_UsesEggFallbackOnlyWhenEligible',
    'RetailStorySelector_SelectsFirstPairedRecordAndConsumesItsPairedFlag',
    'RetailStorySelector_ExtractedSceneTablePreservesWeightedCycleAndPrioritySubset',
    'RetailScriptFlags_PreserveConstantQueriesAndWordBoundaries',
    'RetailScriptFlags_SelectorSetsMorningAndClearsConflictOnlyOnce',
    'RetailStoryDispatcher_PreservesPhaseOrderAndDoesNotRestart',
    'RetailStoryDispatcher_ActiveCheckReturnsPhaseStatusEvenWithoutMatchingCommand',
    'RetailTownAudio_StorySelectionReservesThenAppliesAndHonorsHold',
    'RetailTownAudio_ClearSentinelStopsWithoutLoadingNegativeStream'
)
$results = @()
foreach ($name in $methodNames) {
    try {
        $method = $fixtureType.GetMethod($name)
        if (!$method) { throw "Regression method missing: $name" }
        $method.Invoke($fixture, @()) | Out-Null
        $results += [pscustomobject]@{ method = $name; passed = $true }
    } catch {
        $results += [pscustomobject]@{ method = $name; passed = $false; error = $_.Exception.ToString() }
    }
}
$report = [pscustomobject]@{
    execution = 'Direct invocation of pure managed NUnit methods; Unity lifecycle not run'
    utc = [DateTime]::UtcNow.ToString('o')
    runtime_sha256 = (Get-FileHash -LiteralPath $runtimePath -Algorithm SHA256).Hash
    tests_sha256 = (Get-FileHash -LiteralPath $testsPath -Algorithm SHA256).Hash
    results = $results
}
$target = if ([IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $projectRoot $OutputPath }
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $target
$failed = @($results | Where-Object { !$_.passed })
if ($failed.Count) { throw ($failed | ConvertTo-Json -Depth 4) }
Write-Output "PASS: $($results.Count) managed retail regression methods. Report: $target"
