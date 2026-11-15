# Harness failure evidence - run-002

Spec: specs/001-expense-budget-tracker/spec.md. Do NOT weaken tests, thresholds, analyzers or the spec to obtain PASS.

## Gate G4-tests - Unit & contract tests
- command: `dotnet test`
- exit code: 1
- summary: passed=66 failed=1 skipped=0
- evidence (verbatim, de-duplicated):
```
[xUnit.net 00:00:00.26]       Assert.Equal() Failure: Values differ
[xUnit.net 00:00:00.26]       Expected: 2
[xUnit.net 00:00:00.26]       Actual:   0
Failed SpendWise.Tests.InfrastructureTests.UnwritableStoreIsStorageErrorFR013 [16 ms]
Error Message:
Assert.Equal() Failure: Values differ
Expected: 2
Actual:   0
```

## Gate G5-coverage - Coverage >= 85%
- command: `dotnet test + coverlet threshold`
- exit code: 1
- summary: line coverage=n/a% (exit 1)
- evidence (verbatim, de-duplicated):
```
Failed SpendWise.Tests.InfrastructureTests.UnwritableStoreIsStorageErrorFR013 [18 ms]
Error Message:
Assert.Equal() Failure: Values differ
Expected: 2
Actual:   0
```

