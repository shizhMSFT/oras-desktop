using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using OrasProject.Oras; // for TargetExtensions.CopyAsync
using OrasProject.Oras.Content;
using OrasProject.Oras.Registry.Remote;
using OrasProject.Oras.Registry.Remote.Auth;

// Reflection no longer needed after using official CopyAsync extension.

namespace OrasProject.OrasDesktop.Services;

/// <summary>
/// Concrete implementation over oras-dotnet APIs (to be filled in next step).
/// Currently provides JSON digest extraction logic.
/// </summary>
public sealed class RegistryService : IRegistryService
{
    private RegistryConnection? _connection;
    private Client? _client; // oras remote client
    private HttpClient? _httpClient; // retained only for constructing Client
    private Registry? _registry; // oras registry wrapper

    private static readonly Regex DigestRegex = new(
        "sha256:[a-f0-9]{64}",
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    public Task InitializeAsync(RegistryConnection connection, CancellationToken ct)
    {
        _connection = connection;
        _httpClient = new HttpClient();

        // Build credential based on auth type.
        var credential = new Credential();
        switch (connection.AuthType)
        {
            case AuthType.Basic:
                credential.Username = connection.Username;
                credential.Password = connection.Password; // oras client will negotiate bearer if needed
                break;
            case AuthType.Bearer:
                // Library examples use RefreshToken for token-style auth.
                credential.RefreshToken = connection.BearerToken;
                break;
            case AuthType.Anonymous:
            default:
                break; // leave empty for anonymous
        }

        // Only supply credential provider when we actually have credentials; oras library throws if completely empty
        if (connection.AuthType == AuthType.Anonymous)
        {
            _client = new Client(_httpClient);
        }
        else
        {
            var credentialProvider = new SingleRegistryCredentialProvider(
                connection.Registry,
                credential
            );
            _client = new Client(_httpClient, credentialProvider);
        }
        _registry = new Registry(connection.Registry, _client);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<string>> ListRepositoriesAsync(CancellationToken ct)
    {
        EnsureInitialized();
        var results = new List<string>();
        await foreach (var name in _registry!.ListRepositoriesAsync(null, ct))
        {
            results.Add(name);
        }
        return results;
    }

    public async Task<IReadOnlyList<string>> ListTagsAsync(string repository, CancellationToken ct)
    {
        EnsureInitialized();
        var repo = await _registry!.GetRepositoryAsync(repository, ct);
        var tags = new List<string>();
        if (repo is not null)
        {
            await foreach (var t in repo.ListTagsAsync(null, ct))
            {
                tags.Add(t);
            }
        }
        return tags;
    }

    public async Task<ManifestResult> GetManifestByTagAsync(
        string repository,
        string tag,
        CancellationToken ct
    )
    {
        EnsureInitialized();
        var repo = await _registry!.GetRepositoryAsync(repository, ct);
        var (descriptor, stream) = await repo.FetchAsync(tag, ct);
        byte[] bytes;
        using (stream)
        {
            bytes = await stream.ReadAllAsync(descriptor, ct);
        }
        var json = SafePretty(bytes);
        var digests = ExtractDigests(json);
        return new ManifestResult(descriptor.Digest, descriptor.MediaType, json, digests);
    }

    public async Task<ManifestResult> GetManifestByDigestAsync(
        string repository,
        string digest,
        CancellationToken ct
    )
    {
        EnsureInitialized();
        var repo = await _registry!.GetRepositoryAsync(repository, ct);
        var (descriptor, stream) = await repo.FetchAsync(digest, ct);
        byte[] bytes;
        using (stream)
        {
            bytes = await stream.ReadAllAsync(descriptor, ct);
        }
        var json = SafePretty(bytes);
        var digests = ExtractDigests(json);
        return new ManifestResult(descriptor.Digest, descriptor.MediaType, json, digests);
    }

    public async Task DeleteManifestAsync(string repository, string digest, CancellationToken ct)
    {
        EnsureInitialized();
        var repo = await _registry!.GetRepositoryAsync(repository, ct);
        var desc = await repo.ResolveAsync(digest, ct);
        await repo.DeleteAsync(desc, ct);
    }

    public async Task CopyAsync(
        CopyRequest request,
        IProgress<CopyProgress>? progress,
        CancellationToken ct
    )
    {
        EnsureInitialized();
        progress?.Report(new CopyProgress("Copying", 0, null));
        var src = await _registry!.GetRepositoryAsync(request.SourceRepository, ct);
        var dst = await _registry.GetRepositoryAsync(request.TargetRepository, ct);
        await src.CopyAsync(request.Reference, dst, request.TargetTag ?? string.Empty, ct);
        progress?.Report(new CopyProgress("Completed", 1, 1));
    }

    /// <summary>
    /// Extract digests from a manifest JSON string.
    /// </summary>
    internal static IReadOnlyList<string> ExtractDigests(string json)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var doc = JsonDocument.Parse(json);
            Traverse(doc.RootElement, set);
        }
        catch
        {
            // Fallback: regex only
        }
        foreach (Match m in DigestRegex.Matches(json))
        {
            set.Add(m.Value);
        }
        return set.ToList();
    }

    private static void Traverse(JsonElement el, HashSet<string> set)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in el.EnumerateObject())
                {
                    if (prop.NameEquals("digest") && prop.Value.ValueKind == JsonValueKind.String)
                    {
                        var v = prop.Value.GetString();
                        if (!string.IsNullOrWhiteSpace(v) && DigestRegex.IsMatch(v))
                            set.Add(v!);
                    }
                    Traverse(prop.Value, set);
                }
                break;
            case JsonValueKind.Array:
                foreach (var item in el.EnumerateArray())
                    Traverse(item, set);
                break;
        }
    }

    private void EnsureInitialized()
    {
        if (_connection is null || _client is null || _registry is null)
            throw new InvalidOperationException(
                "Service not initialized. Call InitializeAsync first."
            );
    }

    private static string SafePretty(byte[] bytes)
    {
        try
        {
            var doc = JsonDocument.Parse(bytes);
            return JsonSerializer.Serialize(
                doc.RootElement,
                new JsonSerializerOptions { WriteIndented = true }
            );
        }
        catch
        {
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
    }

    // Raw manifest fallback removed; all operations now use oras-dotnet APIs.
}
