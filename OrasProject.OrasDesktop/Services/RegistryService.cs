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
        public async Task<bool> AuthenticateAsync(Models.Registry registry)
        {
            try
            {
                // Store registry info
                _registries[registry.Url] = registry;
                
                // Use oras-dotnet Registry.PingAsync for authentication testing
                IClient client;
                
                if (registry.RequiresAuthentication)
                {
                    var credential = new Credential
                    {
                        Username = registry.Username ?? string.Empty,
                        Password = registry.Password ?? string.Empty,
                        AccessToken = registry.Token ?? string.Empty
                    };
                    var credentialProvider = new SingleRegistryCredentialProvider(registry.Url, credential);
                    client = new Client(_httpClient, credentialProvider);
                }
                else
                {
                    client = new Client(_httpClient);
                }
                
                var orasRegistry = new OrasProject.Oras.Registry.Remote.Registry(registry.Url, client);
                await orasRegistry.PingAsync();
                return true;
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
                // Store registry info
                _registries[registry.Url] = registry;

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

                // Store registry info
                _registries[repository.Registry.Url] = repository.Registry;

                // Use oras-dotnet Repository for getting tags
                IClient client;
                
                if (repository.Registry.RequiresAuthentication)
                {
                    var credential = new Credential
                    {
                        Username = repository.Registry.Username ?? string.Empty,
                        Password = repository.Registry.Password ?? string.Empty,
                        AccessToken = repository.Registry.Token ?? string.Empty
                    };
                    var credentialProvider = new SingleRegistryCredentialProvider(repository.Registry.Url, credential);
                    client = new Client(_httpClient, credentialProvider);
                }
                else
                {
                    client = new Client(_httpClient);
                }

                // Get repository name without registry URL
                var repoPath = repository.FullPath.Replace($"{repository.Registry.Url}/", "");
                var repoReference = $"{repository.Registry.Url}/{repoPath}";
                
                // Create oras-dotnet Repository
                var repositoryOptions = new RepositoryOptions
                {
                    Reference = Reference.Parse(repoReference),
                    Client = client,
                    PlainHttp = !repository.Registry.IsSecure
                };
                var orasRepo = new OrasProject.Oras.Registry.Remote.Repository(repositoryOptions);
                
                var resultTags = new List<Models.Tag>();
                
                // Use oras-dotnet Repository.ListTagsAsync() to get tags
                await foreach (var tagName in orasRepo.ListTagsAsync())
                {
                    try
                    {
                        // Use oras-dotnet Repository.ResolveAsync() to get manifest descriptor
                        var descriptor = await orasRepo.ResolveAsync(tagName);
                        
                        // Create tag object
                        var tag = new Models.Tag
                        {
                            Name = tagName,
                            Repository = repository,
                            CreatedAt = DateTimeOffset.Now, // API doesn't provide creation time
                            Digest = descriptor.Digest
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

                // Store registry info
                _registries[tag.Repository.Registry.Url] = tag.Repository.Registry;

                // Use oras-dotnet Repository for getting manifest
                IClient client;
                
                if (tag.Repository.Registry.RequiresAuthentication)
                {
                    var credential = new Credential
                    {
                        Username = tag.Repository.Registry.Username ?? string.Empty,
                        Password = tag.Repository.Registry.Password ?? string.Empty,
                        AccessToken = tag.Repository.Registry.Token ?? string.Empty
                    };
                    var credentialProvider = new SingleRegistryCredentialProvider(tag.Repository.Registry.Url, credential);
                    client = new Client(_httpClient, credentialProvider);
                }
                else
                {
                    client = new Client(_httpClient);
                }

                // Get repository name without registry URL
                var repoPath = tag.Repository.FullPath.Replace($"{tag.Repository.Registry.Url}/", "");
                var repoReference = $"{tag.Repository.Registry.Url}/{repoPath}";
                
                // Create oras-dotnet Repository
                var repositoryOptions = new RepositoryOptions
                {
                    Reference = Reference.Parse(repoReference),
                    Client = client,
                    PlainHttp = !tag.Repository.Registry.IsSecure
                };
                var orasRepo = new OrasProject.Oras.Registry.Remote.Repository(repositoryOptions);
                
                // Use oras-dotnet Repository.FetchAsync() to get manifest directly
                var (descriptor, manifestStream) = await orasRepo.FetchAsync(tag.Name);
                
                using (manifestStream)
                {
                    using var memoryStream = new MemoryStream();
                    await manifestStream.CopyToAsync(memoryStream);
                    var manifestJson = Encoding.UTF8.GetString(memoryStream.ToArray());
                    
                    // Parse manifest
                    var result = Models.Manifest.Parse(manifestJson);
                    result.Tag = tag;
                    result.Digest = descriptor.Digest; // Use the actual digest from the descriptor
                    
                    return result;
                }
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

                // Store registry info
                _registries[repository.Registry.Url] = repository.Registry;

                // Use oras-dotnet Repository for getting blob content
                IClient client;
                
                if (repository.Registry.RequiresAuthentication)
                {
                    var credential = new Credential
                    {
                        Username = repository.Registry.Username ?? string.Empty,
                        Password = repository.Registry.Password ?? string.Empty,
                        AccessToken = repository.Registry.Token ?? string.Empty
                    };
                    var credentialProvider = new SingleRegistryCredentialProvider(repository.Registry.Url, credential);
                    client = new Client(_httpClient, credentialProvider);
                }
                else
                {
                    client = new Client(_httpClient);
                }

                // Get repository name without registry URL
                var repoPath = repository.FullPath.Replace($"{repository.Registry.Url}/", "");
                var repoReference = $"{repository.Registry.Url}/{repoPath}";
                
                // Create oras-dotnet Repository
                var repositoryOptions = new RepositoryOptions
                {
                    Reference = Reference.Parse(repoReference),
                    Client = client,
                    PlainHttp = !repository.Registry.IsSecure
                };
                var orasRepo = new OrasProject.Oras.Registry.Remote.Repository(repositoryOptions);
                
                // Use oras-dotnet BlobStore.FetchAsync() to get blob content directly by digest
                var blobStore = new BlobStore(orasRepo);
                var (descriptor, contentStream) = await blobStore.FetchAsync(digest);
                
                using (contentStream)
                {
                    using var memoryStream = new MemoryStream();
                    await contentStream.CopyToAsync(memoryStream);
                    return Encoding.UTF8.GetString(memoryStream.ToArray());
                }
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

                // Store registry info
                _registries[tag.Repository.Registry.Url] = tag.Repository.Registry;

                // Use oras-dotnet Repository and ManifestStore for manifest deletion
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
                
                // Create oras-dotnet Repository instance
                var repoPath = tag.Repository.FullPath.Replace($"{tag.Repository.Registry.Url}/", "");
                var reference = Reference.Parse($"{tag.Repository.Registry.Url}/{repoPath}");
                var options = new RepositoryOptions
                {
                    Reference = reference,
                    Client = client,
                    PlainHttp = !tag.Repository.Registry.IsSecure
                };
                var orasRepo = new OrasProject.Oras.Registry.Remote.Repository(options);
                
                // Use ManifestStore.DeleteAsync() with tag's digest as descriptor
                var manifestStore = new ManifestStore(orasRepo);
                var descriptor = new Descriptor
                {
                    MediaType = MediaType.ImageManifest, // Default to ImageManifest for tags
                    Digest = tag.Digest,
                    Size = tag.Size
                };
                
                await manifestStore.DeleteAsync(descriptor);
                return true;
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

                // Store registry info
                _registries[sourceTag.Repository.Registry.Url] = sourceTag.Repository.Registry;
                _registries[destinationRepository.Registry.Url] = destinationRepository.Registry;

                // Setup credentials for source repository
                ICredentialProvider? sourceCredentialProvider = null;
                if (sourceTag.Repository.Registry.RequiresAuthentication)
                {
                    var sourceCredential = new Credential
                    {
                        Username = sourceTag.Repository.Registry.Username ?? string.Empty,
                        Password = sourceTag.Repository.Registry.Password ?? string.Empty,
                        AccessToken = sourceTag.Repository.Registry.Token ?? string.Empty
                    };
                    sourceCredentialProvider = new SingleRegistryCredentialProvider(sourceTag.Repository.Registry.Url, sourceCredential);
                }

                // Setup credentials for destination repository  
                ICredentialProvider? destCredentialProvider = null;
                if (destinationRepository.Registry.RequiresAuthentication)
                {
                    var destCredential = new Credential
                    {
                        Username = destinationRepository.Registry.Username ?? string.Empty,
                        Password = destinationRepository.Registry.Password ?? string.Empty,
                        AccessToken = destinationRepository.Registry.Token ?? string.Empty
                    };
                    destCredentialProvider = new SingleRegistryCredentialProvider(destinationRepository.Registry.Url, destCredential);
                }

                // Create source repository instance
                var sourceClient = new Client(_httpClient, sourceCredentialProvider);
                var sourceRepoPath = sourceTag.Repository.FullPath.Replace($"{sourceTag.Repository.Registry.Url}/", "");
                var sourceReference = Reference.Parse($"{sourceTag.Repository.Registry.Url}/{sourceRepoPath}");
                var sourceOptions = new RepositoryOptions
                {
                    Reference = sourceReference,
                    Client = sourceClient,
                    PlainHttp = !sourceTag.Repository.Registry.IsSecure
                };
                var sourceRepo = new OrasProject.Oras.Registry.Remote.Repository(sourceOptions);
                var sourceManifestStore = new ManifestStore(sourceRepo);

                // Create destination repository instance
                var destClient = new Client(_httpClient, destCredentialProvider);
                var destRepoPath = destinationRepository.FullPath.Replace($"{destinationRepository.Registry.Url}/", "");
                var destReference = Reference.Parse($"{destinationRepository.Registry.Url}/{destRepoPath}");
                var destOptions = new RepositoryOptions
                {
                    Reference = destReference,
                    Client = destClient,
                    PlainHttp = !destinationRepository.Registry.IsSecure
                };
                var destRepo = new OrasProject.Oras.Registry.Remote.Repository(destOptions);
                var destManifestStore = new ManifestStore(destRepo);

                // Use oras-dotnet high-level APIs to copy manifest
                // 1. Fetch manifest from source using tag name
                var (descriptor, manifestStream) = await sourceManifestStore.FetchAsync(sourceTag.Name);
                
                // 2. Push manifest to destination with new tag
                using (manifestStream)
                {
                    await destManifestStore.PushAsync(descriptor, manifestStream, destinationTag);
                }

                return true;
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