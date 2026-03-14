# .NET 10 Upgrade Plan

## Execution Steps

Execute steps below sequentially one by one in the order they are listed.

1. Validate that a .NET 10 SDK required for this upgrade is installed on the machine and if not, help to get it installed.
2. Ensure that the SDK version specified in global.json files is compatible with the .NET 10 upgrade.
3. Upgrade RadDefenceGame.Windows\RadDefenceGame.Windows.csproj
4. Upgrade RadDefenceGame.Android\RadDefenceGame.Android.csproj

## Settings

This section contains settings and data used by execution steps.

### Excluded projects

No projects are excluded from this upgrade.

### Aggregate NuGet packages modifications across all projects

No NuGet package modifications are required for this upgrade.

### Project upgrade details

This section contains details about each project upgrade and modifications that need to be done in the project.

#### RadDefenceGame.Windows\RadDefenceGame.Windows.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0-windows`

NuGet packages changes:
  - No NuGet package changes required

Feature upgrades:
  - No feature upgrades required

Other changes:
  - No other changes required

#### RadDefenceGame.Android\RadDefenceGame.Android.csproj modifications

Project properties changes:
  - Target framework should be changed from `net8.0` to `net10.0`

NuGet packages changes:
  - No NuGet package changes required

Feature upgrades:
  - No feature upgrades required

Other changes:
  - No other changes required
