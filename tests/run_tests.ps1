param(
    [switch]$Help
)

if ($Help) {
    @"
Usage: .\run_tests.ps1

Runs C# unit tests (without Godot).
Tests pure business logic: Items, Quests, Utils.
For Godot scene tests, use .\run_test.ps1

Requirements: .NET 8 SDK
"@
    exit
}

Write-Host "Running CThangover.Tests unit tests..." -ForegroundColor Cyan
Write-Host ""

dotnet test cthangover.Tests\cthangover.Tests.csproj -v n 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "All tests passed!" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "Some tests failed." -ForegroundColor Red
}

pause