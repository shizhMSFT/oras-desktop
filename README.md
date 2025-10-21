# ORAS Desktop

<p style="text-align: left;">
<a href="https://oras.land/"><img src="https://oras.land/img/oras.svg" alt="banner" width="100px"></a>
</p>

A user-friendly desktop application for managing container artifacts in OCI registries (like Docker Hub, GitHub Container Registry, Azure Container Registry, etc.)

## What is ORAS Desktop?

ORAS Desktop lets you easily view and manage your container artifacts without using command-line tools. With a simple graphical interface, you can:

- Browse your repositories in any container registry
- View all tags and versions of your containers
- Inspect manifest details with an easy-to-read display
- Explore relationships between artifacts

ORAS Desktop is built on top of the [ORAS .NET SDK](https://github.com/oras-project/oras-dotnet), which provides .NET bindings for the OCI Registry As Storage (ORAS) project.

## Getting Started

### Download the App

1. Visit our [Releases page](https://github.com/shizhMSFT/oras-desktop/releases)
2. Download the version for your operating system:
   - macOS: `.app.zip` file
   - Linux: `.tar.gz` file
   - Windows: `.zip` file

### Installation

#### macOS

1. Extract the downloaded `.app.zip` file
2. Move the extracted app to your Applications folder
3. Right-click the app and select "Open" (required only on first launch)

#### Linux

1. Extract the downloaded TAR.GZ file
2. Open a terminal and navigate to the extracted folder
3. Make the application executable: `chmod +x OrasProject.OrasDesktop.Desktop`
4. Run the application: `./OrasProject.OrasDesktop.Desktop`

#### Windows

1. Extract the downloaded ZIP file
2. Double-click `OrasProject.OrasDesktop.Desktop.exe` to start the application

## Using ORAS Desktop

### Connecting to a Registry

1. Click the "Connect" button in the top bar
2. Enter your registry address (e.g., `mcr.microsoft.com`)
3. Choose authentication type:
   - Anonymous (no credentials)
   - Basic (username/password)
   - Bearer Token (for registries using token-based auth)
4. Enter your credentials if required
5. Click "Connect"

### Browsing Repositories

- All accessible repositories will appear in the left sidebar
- Click on a repository to view its available tags
- Select a tag to view the manifest details

### Viewing Manifests

- Manifest details are displayed with syntax highlighting
- Clickable digest links allow quick navigation between related manifests
- The manifest shows important metadata like layers, configuration, and annotations

## System Requirements

- **macOS**: macOS 11 (Big Sur) or later (Intel or Apple Silicon)
- **Linux**: Most modern 64-bit distributions (Ubuntu 20.04+, Fedora 34+, etc.)
- **Windows**: Windows 10 or later (64-bit)

## Building from Source

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later

### Build Steps

1. Clone the repository:
   ```bash
   git clone https://github.com/shizhMSFT/oras-desktop.git
   cd oras-desktop
   ```

2. Restore dependencies and build:
   ```bash
   dotnet restore
   dotnet build --configuration Release
   ```

3. Run the application:
   ```bash
   dotnet run --project OrasProject.OrasDesktop.Desktop/OrasProject.OrasDesktop.Desktop.csproj
   ```

## Troubleshooting

### Application Won't Start

- **macOS**: If you get a "damaged app" warning, try right-clicking the app and selecting "Open"
- **Linux**: Ensure the application has execute permissions (`chmod +x`)
- **Windows**: Make sure you have the [Visual C++ Redistributable](https://aka.ms/vs/17/release/vc_redist.x64.exe) installed

### Can't Connect to Registry

- Check your network connection
- Verify your credentials are correct
- Some registries may have IP restrictions or other access controls

### Enable Debug Logging

To enable detailed debug logging, run the application with the `--debug` flag:

```bash
dotnet run --project OrasProject.OrasDesktop.Desktop/OrasProject.OrasDesktop.Desktop.csproj -- --debug
```

Logs are written to a temp file. The file path is shown in the status bar.

### For Additional Help

If you encounter issues not covered here:

1. Check the [GitHub Issues](https://github.com/shizhMSFT/oras-desktop/issues) to see if your problem is already known
2. Create a new issue with details about your problem

## License

ORAS Desktop is licensed under the [Apache License 2.0](LICENSE).

## Related Projects

- [ORAS Project](https://oras.land/) - The main ORAS project website
- [ORAS CLI](https://github.com/oras-project/oras) - Command-line tool for working with OCI registries
- [ORAS .NET SDK](https://github.com/oras-project/oras-dotnet) - .NET SDK for working with OCI registries
