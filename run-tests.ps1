# Runs every automated test, collects coverage and writes the evidence files used in the final report.
# Usage (PowerShell, from the repository root):   .\run-tests.ps1
$ErrorActionPreference = "Stop"

if (Test-Path TestResults) { Remove-Item TestResults -Recurse -Force }

dotnet test CMRP.Tests/CMRP.Tests.csproj `
    --settings CMRP.Tests/coverlet.runsettings `
    --collect:"XPlat Code Coverage" `
    --logger "trx;LogFileName=test-results.trx" `
    --logger "console;verbosity=normal" `
    --results-directory TestResults

if ($LASTEXITCODE -ne 0) { Write-Host "Some tests failed - see above." -ForegroundColor Red }

# Coverage summary (installs the report tool once).
if (-not (Get-Command reportgenerator -ErrorAction SilentlyContinue)) {
    dotnet tool install --global dotnet-reportgenerator-globaltool
}
reportgenerator `
    "-reports:TestResults/**/coverage.cobertura.xml" `
    "-targetdir:coverage-report" `
    "-reporttypes:TextSummary;Html"

Write-Host ""
Write-Host "================ COVERAGE SUMMARY ================"
Get-Content coverage-report/Summary.txt
Write-Host ""
Write-Host "Open coverage-report/index.html for the per-class report (screenshot it for the final report)."
Write-Host "Test results file: TestResults/test-results.trx"
exit $LASTEXITCODE
