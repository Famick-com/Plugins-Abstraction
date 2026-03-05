# Plugins-Abstraction

Plugin interface contracts for Famick Home Management. Defines `IPlugin`, `IProductLookupPlugin`, and `IStoreIntegrationPlugin` interfaces for building product lookup and store integration plugins.

## Installation

This package is published to [GitHub Packages](https://github.com/Famick-com/Plugins-Abstraction/packages). You need to configure the Famick GitHub Packages NuGet feed before you can install it.

### 1. Create a GitHub Personal Access Token

You need a GitHub PAT with the `read:packages` scope:

1. Go to [GitHub Settings > Developer settings > Personal access tokens](https://github.com/settings/tokens)
2. Generate a new token (classic) with the **`read:packages`** scope
3. Copy the token

Alternatively, if you have the [GitHub CLI](https://cli.github.com/) installed:

```bash
# Add the read:packages scope to your existing token
gh auth refresh -h github.com -s read:packages

# View your token
gh auth token
```

### 2. Add the NuGet Feed

**Option A: NuGet.config in your project** (recommended for CI/CD)

Create or update `NuGet.config` at your solution root:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="github-famick" value="https://nuget.pkg.github.com/Famick-com/index.json" />
  </packageSources>
</configuration>
```

Then add credentials at the user level (so they aren't committed to source control):

```bash
dotnet nuget update source github-famick \
  --username YOUR_GITHUB_USERNAME \
  --password YOUR_GITHUB_PAT \
  --store-password-in-clear-text
```

**Option B: User-level configuration only**

```bash
# Add the source with credentials
dotnet nuget add source "https://nuget.pkg.github.com/Famick-com/index.json" \
  --name github-famick \
  --username YOUR_GITHUB_USERNAME \
  --password YOUR_GITHUB_PAT \
  --store-password-in-clear-text
```

### 3. Add the Package Reference

In your `.csproj` file:

```xml
<PackageReference Include="Famick.HomeManagement.Plugin.Abstractions" Version="1.0.0" />
```

Or via the CLI:

```bash
dotnet add package Famick.HomeManagement.Plugin.Abstractions --version 1.0.0
```

### CI/CD (GitHub Actions)

In GitHub Actions, use `sed` to inject credentials into your `NuGet.config` before restore:

```yaml
- name: Configure GitHub Packages auth
  env:
    NUGET_TOKEN: ${{ secrets.PACKAGES_TOKEN }}
  run: |
    sed -i "s|</packageSources>|</packageSources><packageSourceCredentials><github-famick><add key=\"Username\" value=\"Famick-com\" /><add key=\"ClearTextPassword\" value=\"${NUGET_TOKEN}\" /></github-famick></packageSourceCredentials>|" NuGet.config
```

The `PACKAGES_TOKEN` secret should be a PAT with `read:packages` scope. Note that the default `GITHUB_TOKEN` cannot access packages from other repositories.

## Interfaces

| Interface | Purpose |
|---|---|
| `IPlugin` | Base interface — identity, availability, initialization |
| `IProductLookupPlugin` | Product search from external databases (nutrition, images, barcodes) |
| `IStoreIntegrationPlugin` | Grocery store API integration (OAuth, pricing, cart) |

## Documentation

- [Plugin Authoring Guide](docs/author-plugins.md) — How to build a product lookup plugin
- [Store Integration Guide](docs/STORE_INTEGRATIONS.md) — How to build a store integration plugin
- [Plugin-Kroger](https://github.com/Famick-com/Plugin-Kroger) — Reference implementation of a store integration plugin

## License

Apache-2.0
