# Testing

Running the tests with cobertura code coverage:

```powershell
dotnet test "Cognitive Code Analysis.Tests\CognitiveCodeAnalysis.Tests.csproj" --collect:"XPlat Code Coverage" --results-directory:"./coverage" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura
```
