# ORAS Desktop Release Process

## Prerequisites
- GitHub access with push/release permissions
- Git and .NET 9.0 SDK installed

## Release Steps

### 1. Prepare Release
- Merge features/fixes to main branch
- Update version in `Directory.Build.props` if needed
- Verify `<RuntimeIdentifiers>win-x64;linux-x64;osx-arm64</RuntimeIdentifiers>` in project files
- Test build: `dotnet restore && dotnet build --configuration Release`
- Test publish: `dotnet publish OrasProject.OrasDesktop.Desktop/OrasProject.OrasDesktop.Desktop.csproj --configuration Release --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -r win-x64 -o ./publish`

### 2. Create Tag
```bash
git tag -a v1.0.0 -m "ORAS Desktop v1.0.0"
git push origin v1.0.0
```

### 3. Automated Process
GitHub Actions workflow (`release-github.yml`) will:
- Build for Windows, macOS, Linux
- Package appropriately per platform
- Generate checksums
- Create draft GitHub Release

### 4. Review and Publish
- Check [Releases page](https://github.com/shizhMSFT/oras-desktop/releases)
- Review notes and artifacts
- Publish when ready

## Release Artifacts
- Windows: `oras-desktop_VERSION_windows_amd64.zip`
- macOS: `oras-desktop_VERSION_darwin_arm64.app.zip`
- Linux: `oras-desktop_VERSION_linux_amd64.tar.gz`
- Checksums: `oras-desktop_VERSION_checksums.txt`

## Installation Instructions

### Windows
1. Download and extract ZIP
2. Run `OrasProject.OrasDesktop.Desktop.exe`

### macOS
1. Download and extract `.app.zip`
2. Move to Applications folder
3. Right-click > Open (first launch only)

> **Note**: App is ad-hoc signed. First launch requires right-click > Open to bypass Gatekeeper.

### Linux
1. Download and extract TAR.GZ
2. `chmod +x OrasProject.OrasDesktop.Desktop`
3. Run `./OrasProject.OrasDesktop.Desktop`

## Verifying Checksums

### Linux/macOS
```bash
sha256sum -c oras-desktop_VERSION_checksums.txt
```

### Windows
```powershell
Get-Content oras-desktop_VERSION_checksums.txt | 
  Select-String -Pattern "^([0-9a-f]+)\s+(.+)$" | 
  ForEach-Object { 
    $hash = $_.Matches.Groups[1].Value; 
    $file = $_.Matches.Groups[2].Value; 
    if ((Get-FileHash -Algorithm SHA256 $file).Hash.ToLower() -eq $hash) {
      "$file: OK"
    } else {
      "$file: FAILED"
    }
  }
```

## Build Configuration
- All platforms: `-p:PublishReadyToRun=true` for better startup
- All platforms: `-p:PublishSingleFile=true` for single executable
- All platforms: `-p:PublishTrimmed=true` for smaller size (optional, produces warnings)
- Restore with `-p:PublishReadyToRun=true` for required runtime packages

## macOS Code Signing
The macOS app is ad-hoc signed (`codesign -s -`), which:
- ✅ Provides code integrity and runtime hardening
- ✅ Prevents "damaged app" warnings
- ❌ Does not bypass Gatekeeper (users still need right-click > Open first time)

## Troubleshooting
1. Check GitHub Actions logs
2. Verify permissions and tag format
3. For ReadyToRun errors, ensure the project is built for the target runtime before publishing
4. Don't use `--no-build` flag with ReadyToRun unless you've already built for the specific runtime
5. When using `PublishTrimmed=true`, expect warnings related to reflection in Avalonia UI and JSON serialization