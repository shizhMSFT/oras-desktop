using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OrasProject.OrasDesktop.Models;

namespace OrasProject.OrasDesktop.Services
{
    /// <summary>
    /// Interface for registry operations
    /// </summary>
    public interface IRegistryService
    {
        /// <summary>
        /// Connect to a registry with the provided URL
        /// </summary>
        /// <param name="registry">The registry to connect to</param>
        /// <returns>True if connection is successful, false otherwise</returns>
        Task<bool> ConnectAsync(Registry registry);

        /// <summary>
        /// Authenticate with the registry
        /// </summary>
        /// <param name="registry">The registry to authenticate with</param>
        /// <returns>True if authentication is successful, false otherwise</returns>
        Task<bool> AuthenticateAsync(Registry registry);

        /// <summary>
        /// Get repositories from the registry
        /// </summary>
        /// <param name="registry">The registry to get repositories from</param>
        /// <returns>A list of repositories</returns>
        Task<List<Repository>> GetRepositoriesAsync(Registry registry);

        /// <summary>
        /// Get tags from a repository
        /// </summary>
        /// <param name="repository">The repository to get tags from</param>
        /// <returns>A list of tags</returns>
        Task<List<Tag>> GetTagsAsync(Repository repository);

        /// <summary>
        /// Get manifest for a tag
        /// </summary>
        /// <param name="tag">The tag to get manifest for</param>
        /// <returns>The manifest</returns>
        Task<Manifest> GetManifestAsync(Tag tag);

        /// <summary>
        /// Get content for a digest reference
        /// </summary>
        /// <param name="repository">The repository containing the reference</param>
        /// <param name="digest">The digest of the reference</param>
        /// <returns>The content as a string</returns>
        Task<string> GetContentAsync(Repository repository, string digest);

        /// <summary>
        /// Delete a manifest
        /// </summary>
        /// <param name="tag">The tag whose manifest to delete</param>
        /// <returns>True if deletion is successful, false otherwise</returns>
        Task<bool> DeleteManifestAsync(Tag tag);

        /// <summary>
        /// Copy a manifest and its references to another repository
        /// </summary>
        /// <param name="sourceTag">The source tag</param>
        /// <param name="destinationRepository">The destination repository</param>
        /// <param name="destinationTag">The destination tag</param>
        /// <returns>True if copy is successful, false otherwise</returns>
        Task<bool> CopyManifestAsync(Tag sourceTag, Repository destinationRepository, string destinationTag);
    }
}