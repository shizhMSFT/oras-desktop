using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.Extensions.Caching.Memory;
using OrasProject.OrasDesktop.Models;
using OrasProject.Oras.Registry.Remote.Auth;
using OrasProject.Oras.Registry;
using OrasProject.Oras.Registry.Remote;
using OrasProject.Oras.Oci;
using OrasProject.Oras.Content;

namespace OrasProject.OrasDesktop.Services
{
    /// <summary>
    /// Implementation of the registry service using ORAS .NET SDK
    /// </summary>
    public class RegistryService : IRegistryService
    {
        private readonly HttpClient _httpClient;
        private readonly Dictionary<string, Models.Registry> _registries;
        private readonly Dictionary<string, string> _repositoryClients;
        private readonly MemoryCache _memoryCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="RegistryService"/> class
        /// </summary>
        public RegistryService()
        {
            _httpClient = new HttpClient();
            _registries = new Dictionary<string, Models.Registry>();
            _repositoryClients = new Dictionary<string, string>();
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
        }

        /// <inheritdoc/>
        public async Task<bool> ConnectAsync(Models.Registry registry)
        {
            try
            {
                // Store the registry for later use
                _registries[registry.Url] = registry;
                
                // Use oras-dotnet Client to test connection
                var client = new Client(_httpClient);
                var testUri = new Uri($"{(registry.IsSecure ? "https" : "http")}://{registry.Url}/v2/");
                var request = new HttpRequestMessage(HttpMethod.Get, testUri);
                
                var response = await client.SendAsync(request, default);
                return response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Unauthorized;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> AuthenticateAsync(Models.Registry registry)
        {
            try
            {
                if (!_registries.ContainsKey(registry.Url))
                {
                    if (!await ConnectAsync(registry))
                    {
                        return false;
                    }
                }

                _registries[registry.Url] = registry;
                
                // Use oras-dotnet Client with authentication
                ICredentialProvider? credentialProvider = null;
                
                if (registry.RequiresAuthentication)
                {
                    var credential = new Credential
                    {
                        Username = registry.Username ?? string.Empty,
                        Password = registry.Password ?? string.Empty,
                        AccessToken = registry.Token ?? string.Empty
                    };
                    credentialProvider = new SingleRegistryCredentialProvider(registry.Url, credential);
                }
                
                var client = new Client(_httpClient, credentialProvider);
                var testUri = new Uri($"{(registry.IsSecure ? "https" : "http")}://{registry.Url}/v2/");
                var request = new HttpRequestMessage(HttpMethod.Get, testUri);
                
                var response = await client.SendAsync(request, default);
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<List<Models.Repository>> GetRepositoriesAsync(Models.Registry registry)
        {
            try
            {
                if (!_registries.ContainsKey(registry.Url))
                {
                    if (!await ConnectAsync(registry))
                    {
                        return new List<Models.Repository>();
                    }
                }

                // Use oras-dotnet Client for catalog request
                ICredentialProvider? credentialProvider = null;
                if (registry.RequiresAuthentication)
                {
                    var credential = new Credential
                    {
                        Username = registry.Username ?? string.Empty,
                        Password = registry.Password ?? string.Empty,
                        AccessToken = registry.Token ?? string.Empty
                    };
                    credentialProvider = new SingleRegistryCredentialProvider(registry.Url, credential);
                }
                
                var client = new Client(_httpClient, credentialProvider);
                var catalogUri = new Uri($"{(registry.IsSecure ? "https" : "http")}://{registry.Url}/v2/_catalog");
                var request = new HttpRequestMessage(HttpMethod.Get, catalogUri);
                
                var response = await client.SendAsync(request, default);
                if (!response.IsSuccessStatusCode)
                {
                    return new List<Models.Repository>();
                }
                
                var content = await response.Content.ReadAsStringAsync();
                var catalog = JsonConvert.DeserializeObject<RepositoryCatalog>(content);
                
                if (catalog?.Repositories == null)
                {
                    return new List<Models.Repository>();
                }
                
                var rootRepositories = new List<Models.Repository>();
                var repoDict = new Dictionary<string, Models.Repository>();
                
                // Process repositories and build a tree structure
                foreach (var repoName in catalog.Repositories)
                {
                    // Split repository path by segments
                    var segments = repoName.Split('/');
                    string currentPath = "";
                    Models.Repository? parent = null;
                    
                    // Process each segment
                    for (int i = 0; i < segments.Length; i++)
                    {
                        var segment = segments[i];
                        currentPath = string.IsNullOrEmpty(currentPath) ? segment : $"{currentPath}/{segment}";
                        
                        // Check if we already processed this path
                        if (!repoDict.TryGetValue(currentPath, out var repo))
                        {
                            // Create a new repository
                            repo = new Models.Repository
                            {
                                Name = segment,
                                FullPath = $"{registry.Url}/{currentPath}",
                                Parent = parent,
                                Registry = registry,
                                IsLeaf = (i == segments.Length - 1) // Leaf if it's the last segment
                            };
                            
                            // Add to dictionary
                            repoDict[currentPath] = repo;
                            
                            // Add to parent's children if parent exists
                            if (parent != null)
                            {
                                parent.Children.Add(repo);
                            }
                            else
                            {
                                // Add to root repositories if no parent
                                rootRepositories.Add(repo);
                            }
                        }
                        
                        // Set current repository as parent for next iteration
                        parent = repo;
                    }
                }
                
                return rootRepositories;
            }
            catch (Exception)
            {
                return new List<Models.Repository>();
            }
        }

        /// <inheritdoc/>
        public async Task<List<Models.Tag>> GetTagsAsync(Models.Repository repository)
        {
            try
            {
                if (repository.Registry == null)
                {
                    return new List<Models.Tag>();
                }

                if (!_registries.ContainsKey(repository.Registry.Url))
                {
                    if (!await ConnectAsync(repository.Registry))
                    {
                        return new List<Models.Tag>();
                    }
                }

                // Configure HTTP protocol based on registry settings
                string protocol = repository.Registry.IsSecure ? "https" : "http";
                
                // Get repository name without registry URL
                var repoPath = repository.FullPath.Replace($"{repository.Registry.Url}/", "");
                
                // Get tags from the repository using the OCI API
                var response = await _httpClient.GetAsync($"{protocol}://{repository.Registry.Url}/v2/{repoPath}/tags/list");
                if (!response.IsSuccessStatusCode)
                {
                    return new List<Models.Tag>();
                }
                
                var content = await response.Content.ReadAsStringAsync();
                var tagList = JsonConvert.DeserializeObject<TagList>(content);
                
                if (tagList?.Tags == null)
                {
                    return new List<Models.Tag>();
                }
                
                var resultTags = new List<Models.Tag>();
                
                foreach (var tagName in tagList.Tags)
                {
                    try
                    {
                        // Get manifest for this tag to get the digest
                        _httpClient.DefaultRequestHeaders.Accept.Clear();
                        _httpClient.DefaultRequestHeaders.Accept.Add(
                            new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.manifest.v1+json"));
                        
                        var manifestResponse = await _httpClient.GetAsync(
                            $"{protocol}://{repository.Registry.Url}/v2/{repoPath}/manifests/{tagName}");
                        
                        if (!manifestResponse.IsSuccessStatusCode)
                        {
                            continue;
                        }
                        
                        var digest = manifestResponse.Headers.GetValues("Docker-Content-Digest").FirstOrDefault() ?? "";
                        
                        // Create tag object
                        var tag = new Models.Tag
                        {
                            Name = tagName,
                            Repository = repository,
                            CreatedAt = DateTimeOffset.Now, // API doesn't provide creation time
                            Digest = digest
                        };
                        
                        resultTags.Add(tag);
                    }
                    catch
                    {
                        // Skip tags that can't be resolved
                        continue;
                    }
                }
                
                return resultTags;
            }
            catch (Exception)
            {
                return new List<Models.Tag>();
            }
        }

        /// <inheritdoc/>
        public async Task<Models.Manifest> GetManifestAsync(Models.Tag tag)
        {
            try
            {
                if (tag.Repository?.Registry == null)
                {
                    return new Models.Manifest();
                }

                if (!_registries.ContainsKey(tag.Repository.Registry.Url))
                {
                    if (!await ConnectAsync(tag.Repository.Registry))
                    {
                        return new Models.Manifest();
                    }
                }

                // Use oras-dotnet Client for manifest request
                ICredentialProvider? credentialProvider = null;
                if (tag.Repository.Registry.RequiresAuthentication)
                {
                    var credential = new Credential
                    {
                        Username = tag.Repository.Registry.Username ?? string.Empty,
                        Password = tag.Repository.Registry.Password ?? string.Empty,
                        AccessToken = tag.Repository.Registry.Token ?? string.Empty
                    };
                    credentialProvider = new SingleRegistryCredentialProvider(tag.Repository.Registry.Url, credential);
                }
                
                var client = new Client(_httpClient, credentialProvider);
                
                // Get repository name without registry URL
                var repoPath = tag.Repository.FullPath.Replace($"{tag.Repository.Registry.Url}/", "");
                
                // Get manifest for this tag using the OCI API
                var manifestUri = new Uri($"{(tag.Repository.Registry.IsSecure ? "https" : "http")}://{tag.Repository.Registry.Url}/v2/{repoPath}/manifests/{tag.Name}");
                var request = new HttpRequestMessage(HttpMethod.Get, manifestUri);
                request.Headers.Accept.Clear();
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.manifest.v1+json"));
                
                var response = await client.SendAsync(request, default);
                
                if (!response.IsSuccessStatusCode)
                {
                    return new Models.Manifest();
                }
                
                var manifestJson = await response.Content.ReadAsStringAsync();
                
                // Parse manifest
                var result = Models.Manifest.Parse(manifestJson);
                result.Tag = tag;
                result.Digest = tag.Digest;
                
                return result;
            }
            catch (Exception)
            {
                return new Models.Manifest();
            }
        }

        /// <inheritdoc/>
        public async Task<string> GetContentAsync(Models.Repository repository, string digest)
        {
            try
            {
                if (repository.Registry == null)
                {
                    return string.Empty;
                }

                if (!_registries.ContainsKey(repository.Registry.Url))
                {
                    if (!await ConnectAsync(repository.Registry))
                    {
                        return string.Empty;
                    }
                }

                // Use oras-dotnet Client for blob request
                ICredentialProvider? credentialProvider = null;
                if (repository.Registry.RequiresAuthentication)
                {
                    var credential = new Credential
                    {
                        Username = repository.Registry.Username ?? string.Empty,
                        Password = repository.Registry.Password ?? string.Empty,
                        AccessToken = repository.Registry.Token ?? string.Empty
                    };
                    credentialProvider = new SingleRegistryCredentialProvider(repository.Registry.Url, credential);
                }
                
                var client = new Client(_httpClient, credentialProvider);
                
                // Get repository name without registry URL
                var repoPath = repository.FullPath.Replace($"{repository.Registry.Url}/", "");
                
                // Get blob (content) for this digest using the OCI API
                var blobUri = new Uri($"{(repository.Registry.IsSecure ? "https" : "http")}://{repository.Registry.Url}/v2/{repoPath}/blobs/{digest}");
                var request = new HttpRequestMessage(HttpMethod.Get, blobUri);
                
                var response = await client.SendAsync(request, default);
                
                if (!response.IsSuccessStatusCode)
                {
                    return string.Empty;
                }
                
                return await response.Content.ReadAsStringAsync();
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> DeleteManifestAsync(Models.Tag tag)
        {
            try
            {
                if (tag.Repository?.Registry == null)
                {
                    return false;
                }

                if (!_registries.ContainsKey(tag.Repository.Registry.Url))
                {
                    if (!await ConnectAsync(tag.Repository.Registry))
                    {
                        return false;
                    }
                }

                // Configure HTTP protocol based on registry settings
                string protocol = tag.Repository.Registry.IsSecure ? "https" : "http";
                
                // Get repository name without registry URL
                var repoPath = tag.Repository.FullPath.Replace($"{tag.Repository.Registry.Url}/", "");
                
                // Delete the manifest using the OCI API
                var request = new HttpRequestMessage(
                    HttpMethod.Delete, 
                    $"{protocol}://{tag.Repository.Registry.Url}/v2/{repoPath}/manifests/{tag.Digest}");
                
                var response = await _httpClient.SendAsync(request);
                
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <inheritdoc/>
        public async Task<bool> CopyManifestAsync(Models.Tag sourceTag, Models.Repository destinationRepository, string destinationTag)
        {
            try
            {
                if (sourceTag.Repository?.Registry == null || destinationRepository.Registry == null)
                {
                    return false;
                }

                // Get source manifest
                var manifest = await GetManifestAsync(sourceTag);
                if (manifest.Content == null)
                {
                    return false;
                }
                
                // Configure HTTP protocol based on registry settings
                string srcProtocol = sourceTag.Repository.Registry.IsSecure ? "https" : "http";
                string dstProtocol = destinationRepository.Registry.IsSecure ? "https" : "http";
                
                // Get source and destination repository names without registry URL
                var sourceRepoPath = sourceTag.Repository.FullPath.Replace($"{sourceTag.Repository.Registry.Url}/", "");
                var destRepoPath = destinationRepository.FullPath.Replace($"{destinationRepository.Registry.Url}/", "");
                
                // First, copy all the layers
                foreach (var layer in manifest.Layers)
                {
                    // Get the layer content
                    var layerResponse = await _httpClient.GetAsync(
                        $"{srcProtocol}://{sourceTag.Repository.Registry.Url}/v2/{sourceRepoPath}/blobs/{layer.Digest}");
                    
                    if (!layerResponse.IsSuccessStatusCode)
                    {
                        continue;
                    }
                    
                    var layerContent = await layerResponse.Content.ReadAsByteArrayAsync();
                    
                    // Upload the layer to the destination
                    var layerContentStream = new MemoryStream(layerContent);
                    
                    // Start upload session
                    var uploadResponse = await _httpClient.PostAsync(
                        $"{dstProtocol}://{destinationRepository.Registry.Url}/v2/{destRepoPath}/blobs/uploads/",
                        null);
                    
                    if (!uploadResponse.IsSuccessStatusCode)
                    {
                        continue;
                    }
                    
                    // Get the upload location
                    var location = uploadResponse.Headers.Location;
                    if (location == null)
                    {
                        continue;
                    }
                    
                    // Upload the content
                    var uploadRequest = new HttpRequestMessage(HttpMethod.Put, location + $"&digest={layer.Digest}");
                    uploadRequest.Content = new StreamContent(layerContentStream);
                    uploadRequest.Content.Headers.ContentType = new MediaTypeHeaderValue(layer.MediaType);
                    
                    var uploadCompleteResponse = await _httpClient.SendAsync(uploadRequest);
                    if (!uploadCompleteResponse.IsSuccessStatusCode)
                    {
                        continue;
                    }
                }
                
                // Upload the config
                if (manifest.Config != null)
                {
                    var configResponse = await _httpClient.GetAsync(
                        $"{srcProtocol}://{sourceTag.Repository.Registry.Url}/v2/{sourceRepoPath}/blobs/{manifest.Config.Digest}");
                    
                    if (configResponse.IsSuccessStatusCode)
                    {
                        var configContent = await configResponse.Content.ReadAsByteArrayAsync();
                        var configContentStream = new MemoryStream(configContent);
                        
                        // Start upload session
                        var uploadResponse = await _httpClient.PostAsync(
                            $"{dstProtocol}://{destinationRepository.Registry.Url}/v2/{destRepoPath}/blobs/uploads/",
                            null);
                        
                        if (uploadResponse.IsSuccessStatusCode)
                        {
                            // Get the upload location
                            var location = uploadResponse.Headers.Location;
                            if (location != null)
                            {
                                // Upload the content
                                var uploadRequest = new HttpRequestMessage(HttpMethod.Put, location + $"&digest={manifest.Config.Digest}");
                                uploadRequest.Content = new StreamContent(configContentStream);
                                uploadRequest.Content.Headers.ContentType = new MediaTypeHeaderValue(manifest.Config.MediaType);
                                
                                await _httpClient.SendAsync(uploadRequest);
                            }
                        }
                    }
                }
                
                // Finally, upload the manifest with the new tag
                var manifestContent = manifest.RawContent;
                
                var manifestRequest = new HttpRequestMessage(
                    HttpMethod.Put,
                    $"{dstProtocol}://{destinationRepository.Registry.Url}/v2/{destRepoPath}/manifests/{destinationTag}");
                
                manifestRequest.Content = new StringContent(manifestContent);
                manifestRequest.Content.Headers.ContentType = new MediaTypeHeaderValue(manifest.MediaType);
                
                var manifestResponse = await _httpClient.SendAsync(manifestRequest);
                
                return manifestResponse.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }
        
        // Helper classes for JSON deserialization
        private class RepositoryCatalog
        {
            [JsonProperty("repositories")]
            public List<string> Repositories { get; set; } = new List<string>();
        }
        
        private class TagList
        {
            [JsonProperty("name")]
            public string Name { get; set; } = string.Empty;
            
            [JsonProperty("tags")]
            public List<string> Tags { get; set; } = new List<string>();
        }
    }
}