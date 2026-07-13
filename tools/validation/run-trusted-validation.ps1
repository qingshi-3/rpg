param(
    [string]$GodotBin = $env:GODOT_BIN
)

$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path

# This list is the maintained non-preview suite. Keep it explicit: validation must never discover projects by walking the tree.
$runnerProjects = @(
    'tests/BattleHitFeedbackRegression/BattleHitFeedbackRegression.csproj',
    'tests/GameCursorAnimationRegression/GameCursorAnimationRegression.csproj',
    'tests/SceneTransitionCasRegression/SceneTransitionCasRegression.csproj',
    'tests/StrategicManagementRegression/StrategicManagementRegression.csproj',
    'tests/TargetBattleArchitectureRegression/TargetBattleArchitectureRegression.csproj',
    'tests/WorldArmyMovementRegression/WorldArmyMovementRegression.csproj',
    'tests/WorldSiteDeploymentCacheRegression/WorldSiteDeploymentCacheRegression.csproj'
)

Push-Location $root
try {
    & dotnet build rpg.sln --no-restore -maxcpucount:2 -v:minimal
    if ($LASTEXITCODE -ne 0) { throw "Solution build failed with exit code $LASTEXITCODE." }

    foreach ($project in $runnerProjects) {
        Write-Host "RUN $project"
        & dotnet run --project $project --no-build --no-restore
        if ($LASTEXITCODE -ne 0) { throw "Regression runner failed: $project" }
    }

    if ([string]::IsNullOrWhiteSpace($GodotBin)) {
        $godotCommand = Get-Command godot, godot4 -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($null -eq $godotCommand) { throw 'Godot executable not found. Pass -GodotBin or set GODOT_BIN.' }
        $GodotBin = $godotCommand.Source
    }

    Write-Host 'RUN explicit trusted scene smoke'
    & $GodotBin --headless --path $root --script res://tests/smoke/trusted-scene-smoke.gd
    if ($LASTEXITCODE -ne 0) { throw "Trusted scene smoke failed with exit code $LASTEXITCODE." }
}
finally {
    Pop-Location
}
