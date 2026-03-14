# .NET 10 Upgrade Report

## Project target framework modifications

| Project name                                           | Old Target Framework | New Target Framework | Commits    |
|:-------------------------------------------------------|:--------------------:|:--------------------:|:-----------|
| RadDefenceGame.Windows\RadDefenceGame.Windows.csproj   | net8.0               | net10.0-windows      | 7f4db0b5   |
| RadDefenceGame.Android\RadDefenceGame.Android.csproj   | net8.0               | net10.0              | 1827de80   |

## NuGet Packages

No NuGet package changes were required for this upgrade.

## All commits

| Commit ID  | Description                                                                                              |
|:-----------|:---------------------------------------------------------------------------------------------------------|
| b25542ea   | Commit upgrade plan                                                                                      |
| 1300de21   | Store final changes for step 'Ensure that the SDK version specified in global.json files is compatible with the .NET 10 upgrade.' |
| 7f4db0b5   | Update target framework in RadDefenceGame.Windows.csproj                                                 |
| 1827de80   | Update target framework in RadDefenceGame.Android.csproj                                                 |

## Project feature upgrades

No feature upgrades were required for this upgrade.

## Next steps

- Review and test the upgraded projects to ensure they work correctly with .NET 10
- Consider updating any CI/CD pipelines to use .NET 10 SDK
- Review .NET 10 release notes for new features you may want to adopt
