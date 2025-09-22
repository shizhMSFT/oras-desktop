# ORAS Desktop Release Process

This document outlines the steps to create and publish releases for ORAS Desktop.

## Prerequisites

- GitHub access with permission to push tags and create releases
- Git installed locally
- .NET 9.0 SDK installed for local testing

## Release Process

### 1. Prepare the Release

1. Ensure all desired features and bug fixes are merged into the main branch
2. Update version numbers in relevant files:
   - Update `Directory.Build.props` if version is defined there
   - Update any other files that contain version information

3. Verify the application builds and runs correctly:

```powershell
dotnet restore
dotnet build --configuration Release
```

### 2. Create a Release Tag

1. Create a new tag following semantic versioning with a `v` prefix:

```bash
git tag -a v1.0.0 -m "ORAS Desktop v1.0.0"
```

2. Push the tag to GitHub:

```bash
git push origin v1.0.0
```

### 3. Automated Release Process

Once the tag is pushed, the GitHub Actions workflow (`release-github.yml`) will automatically:

1. Build the application for multiple platforms:
   - Windows (Intel/AMD 64-bit and ARM 64-bit)
   - macOS (Intel 64-bit and Apple Silicon)
   - Linux (Intel/AMD 64-bit and ARM 64-bit)

2. Package the application appropriately for each platform:
   - Windows: ZIP archives containing the application
   - macOS: APP bundles in ZIP archives
   - Linux: TAR.GZ archives containing the application

3. Generate checksums for all artifacts

4. Create a draft GitHub Release with all artifacts attached

### 4. Review and Publish the Release

1. Go to the [Releases page](https://github.com/shizhMSFT/oras-desktop/releases) in the GitHub repository
2. Find the draft release created by the GitHub Actions workflow
3. Review the release notes and edit if necessary
4. Review the attached artifacts to ensure they are all present and correctly named
5. When satisfied, click "Publish release" to make it public

## Release Artifacts

The following artifacts will be created for each release:

- Windows:
  - `oras-desktop_VERSION_windows_amd64.zip` - Intel/AMD 64-bit version
  - `oras-desktop_VERSION_windows_arm64.zip` - ARM 64-bit version

- macOS:
  - `oras-desktop_VERSION_darwin_amd64.app.zip` - Intel 64-bit version
  - `oras-desktop_VERSION_darwin_arm64.app.zip` - Apple Silicon version

- Linux:
  - `oras-desktop_VERSION_linux_amd64.tar.gz` - Intel/AMD 64-bit version
  - `oras-desktop_VERSION_linux_arm64.tar.gz` - ARM 64-bit version

- Checksums:
  - `oras-desktop_VERSION_checksums.txt` - SHA256 checksums for all artifacts

## Installation Instructions

### Windows

1. Download the appropriate ZIP file for your architecture
2. Extract the ZIP file to a location of your choice
3. Run the extracted executable (`OrasProject.OrasDesktop.Desktop.exe`)

### macOS

1. Download the appropriate `.app.zip` file for your architecture
2. Extract the ZIP file
3. Move `OrasDesktop.app` to your Applications folder
4. Right-click on the app and select "Open" (required the first time to bypass Gatekeeper)

### Linux

1. Download the appropriate TAR.GZ file for your architecture
2. Extract the archive:
   ```bash
   tar -xzf oras-desktop_VERSION_linux_ARCH.tar.gz
   ```
3. Make the executable file executable:
   ```bash
   chmod +x OrasProject.OrasDesktop.Desktop
   ```
4. Run the application:
   ```bash
   ./OrasProject.OrasDesktop.Desktop
   ```

## Verifying Checksums

### On Linux/macOS

```bash
sha256sum -c oras-desktop_VERSION_checksums.txt
```

### On Windows (PowerShell)

```powershell
Get-Content oras-desktop_VERSION_checksums.txt | Select-String -Pattern "^([0-9a-f]+)\s+(.+)$" | ForEach-Object { 
  $hash = $_.Matches.Groups[1].Value; 
  $file = $_.Matches.Groups[2].Value; 
  if ((Get-FileHash -Algorithm SHA256 $file).Hash.ToLower() -eq $hash) {
    "$file: OK"
  } else {
    "$file: FAILED"
  }
}
```

## Troubleshooting

If you encounter any issues during the release process:

1. Check the GitHub Actions workflow run for detailed logs
2. Ensure all necessary permissions are in place
3. Verify that the tag follows the expected format (starts with `v`)
4. Check if there are any network or GitHub API rate limit issues