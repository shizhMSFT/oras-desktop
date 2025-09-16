using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace OrasProject.OrasDesktop.Services;

/// <summary>
/// High level abstraction over oras-dotnet for registry operations consumed by ViewModels.
/// </summary>
public interface IOrasRegistryService
{
    Task InitializeAsync(RegistryConnection connection, CancellationToken ct);
    Task<IReadOnlyList<string>> ListRepositoriesAsync(CancellationToken ct);
    Task<IReadOnlyList<string>> ListTagsAsync(string repository, CancellationToken ct);
    Task<ManifestResult> GetManifestByTagAsync(string repository, string tag, CancellationToken ct);
    Task<ManifestResult> GetManifestByDigestAsync(
        string repository,
        string digest,
        CancellationToken ct
    );
    Task DeleteManifestAsync(string repository, string digest, CancellationToken ct);
    Task CopyAsync(CopyRequest request, IProgress<CopyProgress>? progress, CancellationToken ct);
}

/// <param name="Registry">Registry hostname (e.g., ghcr.io)</param>
/// <param name="IsSecure">Whether to use HTTPS</param>
/// <param name="Username">Optional username</param>
/// <param name="Password">Optional password / token (basic or bearer token depending on server)</param>
/// <summary>
/// Connection parameters for a registry, supporting anonymous, basic, or bearer auth.
/// </summary>
/// <param name="Registry">Registry hostname (e.g., ghcr.io)</param>
/// <param name="IsSecure">Use HTTPS when true, HTTP otherwise</param>
/// <param name="AuthType">Authentication mode</param>
/// <param name="Username">Username for basic auth</param>
/// <param name="Password">Password for basic auth</param>
/// <param name="BearerToken">Token for bearer auth</param>
public record RegistryConnection(
    string Registry,
    bool IsSecure = true,
    AuthType AuthType = AuthType.Anonymous,
    string? Username = null,
    string? Password = null,
    string? BearerToken = null
);

/// <summary>
/// Supported authentication types.
/// </summary>
public enum AuthType
{
    Anonymous,
    Basic,
    Bearer,
}

public record ManifestResult(
    string Digest,
    string MediaType,
    string Json,
    IReadOnlyList<string> ReferencedDigests
);

public record CopyRequest(
    string SourceRepository,
    string Reference,
    string TargetRepository,
    string? TargetTag
);

public record CopyProgress(string Stage, long? Completed, long? Total);
