using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using OrasProject.OrasDesktop.Models;
using OrasProject.Oras.Registry;

namespace OrasProject.OrasDesktop.Services;

/// <summary>
/// Service responsible for managing registry connections.
/// Similar to ManifestLoader and RepositoryLoader, this encapsulates connection logic and provides events for components to subscribe to.
/// </summary>
public class ConnectionService
{
    private readonly IRegistryService _registryService;
    private readonly ILogger<ConnectionService> _logger;
    private Registry? _currentRegistry;

    public ConnectionService(
        IRegistryService registryService,
        ILogger<ConnectionService> logger)
    {
        _registryService = registryService;
        _logger = logger;
    }

    /// <summary>
    /// Event raised when a connection to a registry has been successfully established
    /// </summary>
    public event EventHandler<ConnectionEstablishedEventArgs>? ConnectionEstablished;

    /// <summary>
    /// Event raised when a connection attempt fails
    /// </summary>
    public event EventHandler<ConnectionFailedEventArgs>? ConnectionFailed;

    /// <summary>
    /// Gets the current connected registry
    /// </summary>
    public Registry? CurrentRegistry => _currentRegistry;

    /// <summary>
    /// Connects to a registry with the specified configuration
    /// </summary>
    public async Task<ConnectionResult> ConnectAsync(
        string registryUrl, 
        bool isSecure = true,
        AuthenticationType authType = AuthenticationType.None,
        string? username = null,
        string? password = null,
        string? token = null,
        CancellationToken cancellationToken = default)
    {
        return await ConnectInternalAsync(
            registryUrl, 
            isSecure, 
            authType, 
            username, 
            password, 
            token, 
            fireEvents: true, 
            validateConnection: null,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Internal connection method with option to suppress events
    /// </summary>
    private async Task<ConnectionResult> ConnectInternalAsync(
        string registryUrl, 
        bool isSecure = true,
        AuthenticationType authType = AuthenticationType.None,
        string? username = null,
        string? password = null,
        string? token = null,
        bool fireEvents = true,
        Func<CancellationToken, Task>? validateConnection = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Connecting to registry: {Registry} (Auth: {AuthType})", registryUrl, authType);
            }

            // Create registry model
            var registry = new Registry
            {
                Url = registryUrl,
                IsSecure = isSecure,
                AuthenticationType = authType,
                Username = username ?? string.Empty,
                Password = password ?? string.Empty,
                Token = token ?? string.Empty
            };

            // Initialize registry service with connection settings
            await _registryService.InitializeAsync(
                new RegistryConnection(
                    registryUrl,
                    isSecure,
                    authType switch
                    {
                        AuthenticationType.Basic => AuthType.Basic,
                        AuthenticationType.Token => AuthType.Bearer,
                        _ => AuthType.Anonymous,
                    },
                    username,
                    password,
                    token
                ),
                cancellationToken
            );

            // Test the connection using the provided validation function, or default to listing repositories
            if (validateConnection != null)
            {
                await validateConnection(cancellationToken);
            }
            else
            {
                // Default: test by listing repositories
                await _registryService.ListRepositoriesAsync(cancellationToken);
            }

            _currentRegistry = registry;

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Successfully connected to registry: {Registry}", registryUrl);
            }

            // Raise connection established event only if fireEvents is true
            if (fireEvents)
            {
                var eventArgs = new ConnectionEstablishedEventArgs(registry, authType != AuthenticationType.None);
                ConnectionEstablished?.Invoke(this, eventArgs);
            }

            return new ConnectionResult(true, null, registry);
        }
        catch (Exception ex)
        {
            if (_logger.IsEnabled(LogLevel.Error))
            {
                _logger.LogError(ex, "Failed to connect to registry: {Registry}", registryUrl);
            }

            // Raise connection failed event only if fireEvents is true
            if (fireEvents)
            {
                var eventArgs = new ConnectionFailedEventArgs(ex, registryUrl);
                ConnectionFailed?.Invoke(this, eventArgs);
            }

            return new ConnectionResult(false, ex.Message, null);
        }
    }

    /// <summary>
    /// Attempts to connect anonymously first, then prompts for authentication if needed
    /// </summary>
    public async Task<bool> TryConnectAnonymouslyAsync(string registryUrl, bool isSecure = true, CancellationToken cancellationToken = default)
    {
        var result = await ConnectAsync(registryUrl, isSecure, AuthenticationType.None, cancellationToken: cancellationToken);
        return result.Success;
    }

    /// <summary>
    /// Connects to a registry with full flow: try anonymous first, then request credentials if needed.
    /// Returns true if connection successful, false if user cancelled or connection failed.
    /// </summary>
    /// <param name="repository">Optional repository to test access (instead of listing all repos)</param>
    /// <param name="tag">Optional tag to test (requires repository parameter)</param>
    public async Task<bool> ConnectWithFlowAsync(
        string registryUrl,
        bool forceLogin,
        Func<string, Task<LoginDialogResult>> requestCredentials,
        CancellationToken cancellationToken = default,
        string? repository = null,
        string? tag = null)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Starting connection flow for {Registry} (forceLogin={ForceLogin})", registryUrl, forceLogin);
        }

        // Create validation function based on repository/tag if provided
        Func<CancellationToken, Task>? validateConnection = null;
        if (!string.IsNullOrEmpty(repository) && !string.IsNullOrEmpty(tag))
        {
            validateConnection = async (ct) =>
            {
                // Test by resolving the specific tag (cheaper than listing repos)
                var canResolve = await _registryService.CanResolveTagAsync(repository, tag, ct);
                if (!canResolve)
                {
                    throw new InvalidOperationException($"Cannot resolve tag '{tag}' in repository '{repository}'");
                }
            };
        }

        // If force login, skip anonymous and go straight to authentication
        if (forceLogin)
        {
            var loginResult = await requestCredentials(registryUrl);
            if (!loginResult.Result)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("User cancelled authentication for {Registry}", registryUrl);
                }
                return false;
            }

            // Use internal method with events enabled for the final attempt
            var authResult = await ConnectInternalAsync(
                registryUrl,
                isSecure: true,
                authType: loginResult.AuthType,
                username: loginResult.Username,
                password: loginResult.Password,
                token: loginResult.Token,
                fireEvents: true,
                validateConnection: validateConnection,
                cancellationToken: cancellationToken
            );

            return authResult.Success;
        }

        // Try anonymous connection first (suppress events - this is just a probe)
        var anonymousResult = await ConnectInternalAsync(
            registryUrl,
            isSecure: true,
            authType: AuthenticationType.None,
            fireEvents: false,
            validateConnection: validateConnection,
            cancellationToken: cancellationToken
        );

        if (anonymousResult.Success)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("Anonymous connection successful for {Registry}", registryUrl);
            }
            
            // Fire success event manually since we suppressed it above
            var eventArgs = new ConnectionEstablishedEventArgs(anonymousResult.Registry!, false);
            ConnectionEstablished?.Invoke(this, eventArgs);
            
            return true;
        }

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation("Anonymous connection failed for {Registry}, requesting credentials", registryUrl);
        }

        // Anonymous failed, request credentials
        var credentialsResult = await requestCredentials(registryUrl);
        
        if (!credentialsResult.Result)
        {
            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation("User cancelled authentication for {Registry}", registryUrl);
            }
            return false;
        }

        // Try with credentials (enable events for final attempt)
        var authenticatedResult = await ConnectInternalAsync(
            registryUrl,
            isSecure: true,
            authType: credentialsResult.AuthType,
            username: credentialsResult.Username,
            password: credentialsResult.Password,
            token: credentialsResult.Token,
            fireEvents: true,
            validateConnection: validateConnection,
            cancellationToken: cancellationToken
        );

        return authenticatedResult.Success;
    }
}

/// <summary>
/// Result of a login dialog
/// </summary>
public class LoginDialogResult
{
    public bool Result { get; }
    public AuthenticationType AuthType { get; }
    public string Username { get; }
    public string Password { get; }
    public string Token { get; }

    public LoginDialogResult(bool result, AuthenticationType authType = AuthenticationType.None, string username = "", string password = "", string token = "")
    {
        Result = result;
        AuthType = authType;
        Username = username;
        Password = password;
        Token = token;
    }
}

/// <summary>
/// Result of a connection attempt
/// </summary>
public class ConnectionResult
{
    public bool Success { get; }
    public string? ErrorMessage { get; }
    public Registry? Registry { get; }

    public ConnectionResult(bool success, string? errorMessage, Registry? registry)
    {
        Success = success;
        ErrorMessage = errorMessage;
        Registry = registry;
    }
}

/// <summary>
/// Event args for when a connection is successfully established
/// </summary>
public class ConnectionEstablishedEventArgs : EventArgs
{
    public Registry Registry { get; }
    public bool IsAuthenticated { get; }

    public ConnectionEstablishedEventArgs(Registry registry, bool isAuthenticated)
    {
        Registry = registry;
        IsAuthenticated = isAuthenticated;
    }
}

/// <summary>
/// Event args for when a connection attempt fails
/// </summary>
public class ConnectionFailedEventArgs : EventArgs
{
    public Exception Exception { get; }
    public string RegistryUrl { get; }

    public ConnectionFailedEventArgs(Exception exception, string registryUrl)
    {
        Exception = exception;
        RegistryUrl = registryUrl;
    }
}
