using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OrasProject.OrasDesktop.Models;
using OrasProject.OrasDesktop.Services;

namespace OrasProject.OrasDesktop.ViewModels
{
    /// <summary>
    /// Extension methods for calculating artifact sizes
    /// </summary>
    public static class ArtifactSizeCalculator
    {
        /// <summary>
        /// Calculates the size of an image manifest by summing the size of the config blob,
        /// all the layer blobs, and the manifest itself
        /// </summary>
        /// <param name="json">The manifest JSON content</param>
        /// <param name="manifestSize">The size of the manifest in bytes</param>
        /// <returns>The total size in bytes</returns>
        public static long CalculateImageManifestSize(string json, long manifestSize)
        {
            // Start with the manifest size
            long totalSize = manifestSize;
            
            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                
                // Add config size
                if (root.TryGetProperty("config", out var config) && 
                    config.TryGetProperty("size", out var configSize))
                {
                    if (configSize.TryGetInt64(out var size))
                    {
                        totalSize += size;
                    }
                }
                
                // Add layer sizes
                if (root.TryGetProperty("layers", out var layers) && 
                    layers.ValueKind == JsonValueKind.Array)
                {
                    foreach (var layer in layers.EnumerateArray())
                    {
                        if (layer.TryGetProperty("size", out var layerSize) && 
                            layerSize.TryGetInt64(out var size))
                        {
                            totalSize += size;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // If JSON parsing fails, just return the manifest size
                return manifestSize;
            }
            
            return totalSize;
        }
        
        /// <summary>
        /// Analyzes a manifest and computes size information
        /// </summary>
        /// <param name="registryService">The registry service to use for fetching manifests</param>
        /// <param name="repository">The repository path</param>
        /// <param name="manifest">The manifest result</param>
        /// <param name="ct">Cancellation token</param>
        /// <returns>Tuple with (summary text, platform sizes list, has platform sizes flag)</returns>
        public static async Task<(string summary, List<PlatformImageSize> platformSizes, bool hasPlatformSizes)> 
            AnalyzeManifestSizeAsync(IRegistryService registryService, string repository, ManifestResult manifest, CancellationToken ct)
        {
            var mediaType = manifest.MediaType;
            var jsonContent = manifest.Json;
            var manifestSize = jsonContent.Length; // Use string length as an approximation of size
            
            // Check if this is a manifest index
            bool isIndex = IsManifestIndex(mediaType);
            
            if (!isIndex)
            {
                // Single image manifest
                long size = CalculateImageManifestSize(jsonContent, manifestSize);
                return ($"Image size: {FormatSize(size)}", new List<PlatformImageSize>(), false);
            }
            else
            {
                // Manifest index - need to fetch child manifests
                var platformSizes = await GetPlatformSizesAsync(registryService, repository, jsonContent, ct);
                
                if (platformSizes.Count == 0)
                {
                    return ("Index manifest: no platform sizes available", new List<PlatformImageSize>(), false);
                }
                
                // Get a filtered list of valid platforms (exclude unknown/unknown)
                var validPlatforms = platformSizes.Where(p => p.Platform != "unknown/unknown").ToList();
                
                // Calculate total size from all platforms (including unknown/unknown)
                long totalSize = platformSizes.Sum(p => p.SizeInBytes);
                string summary = $"Multi-arch index: {validPlatforms.Count} platforms, total {FormatSize(totalSize)}";
                
                return (summary, validPlatforms, true);
            }
        }
        
        /// <summary>
        /// Gets the sizes of platform-specific images from a manifest index
        /// </summary>
        private static async Task<List<PlatformImageSize>> GetPlatformSizesAsync(
            IRegistryService registryService, 
            string repository, 
            string indexJson, 
            CancellationToken ct)
        {
            var result = new List<PlatformImageSize>();
            
            try
            {
                using var document = JsonDocument.Parse(indexJson);
                var root = document.RootElement;
                
                if (root.TryGetProperty("manifests", out var manifests) && 
                    manifests.ValueKind == JsonValueKind.Array)
                {
                    // Process each referenced manifest
                    foreach (var item in manifests.EnumerateArray())
                    {
                        if (item.TryGetProperty("digest", out var digestElement))
                        {
                            string digest = digestElement.GetString() ?? string.Empty;
                            if (string.IsNullOrEmpty(digest))
                                continue;
                                
                            // Get platform information
                            string platform = ExtractPlatformInfo(item);
                            
                            try
                            {
                                // Fetch the platform-specific manifest
                                var childManifest = await registryService.GetManifestByDigestAsync(repository, digest, ct);
                                
                                // Calculate its size
                                long size = CalculateImageManifestSize(childManifest.Json, childManifest.Json.Length);
                                
                                // Add to the results
                                result.Add(new PlatformImageSize 
                                { 
                                    Platform = platform, 
                                    SizeInBytes = size 
                                });
                            }
                            catch (Exception)
                            {
                                // If we can't fetch this manifest, still include it with zero size
                                result.Add(new PlatformImageSize 
                                { 
                                    Platform = platform + " (error)", 
                                    SizeInBytes = 0 
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Return empty list on any error
            }
            
            // Calculate relative percentages based on the largest size
            if (result.Count > 0)
            {
                long maxSize = result.Max(p => p.SizeInBytes);
                if (maxSize > 0)
                {
                    foreach (var platform in result)
                    {
                        platform.RelativePercentage = (double)platform.SizeInBytes / maxSize * 100.0;
                    }
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Extracts platform information from a manifest reference
        /// </summary>
        private static string ExtractPlatformInfo(JsonElement manifestElement)
        {
            try
            {
                if (manifestElement.TryGetProperty("platform", out var platform))
                {
                    string os = platform.TryGetProperty("os", out var osElement) 
                        ? osElement.GetString() ?? "unknown" 
                        : "unknown";
                        
                    string architecture = platform.TryGetProperty("architecture", out var archElement) 
                        ? archElement.GetString() ?? "unknown" 
                        : "unknown";
                        
                    string variant = string.Empty;
                    if (platform.TryGetProperty("variant", out var variantElement))
                    {
                        variant = variantElement.GetString() ?? string.Empty;
                    }
                    
                    return !string.IsNullOrEmpty(variant) 
                        ? $"{os}/{architecture}/{variant}" 
                        : $"{os}/{architecture}";
                }
                
                // If platform property isn't available, use the digest as identifier
                if (manifestElement.TryGetProperty("digest", out var digestElement))
                {
                    string digest = digestElement.GetString() ?? "unknown";
                    return digest.Length > 20 ? digest.Substring(0, 20) + "..." : digest;
                }
            }
            catch (Exception)
            {
                // Fall back to "unknown" on any error
            }
            
            return "unknown";
        }
        
        /// <summary>
        /// Checks if a media type represents a manifest index
        /// </summary>
        private static bool IsManifestIndex(string mediaType)
        {
            return mediaType.Contains("index", StringComparison.OrdinalIgnoreCase) ||
                   mediaType.Contains("manifest.list", StringComparison.OrdinalIgnoreCase) ||
                   mediaType.Contains("distribution.manifest.list", StringComparison.OrdinalIgnoreCase);
        }
        
        /// <summary>
        /// Formats a byte count as a human-readable string
        /// </summary>
        private static string FormatSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            
            int suffixIndex = 0;
            double size = bytes;
            
            while (size >= 1024 && suffixIndex < suffixes.Length - 1)
            {
                size /= 1024;
                suffixIndex++;
            }
            
            return suffixIndex == 0 
                ? $"{bytes} {suffixes[suffixIndex]}" 
                : $"{size:0.##} {suffixes[suffixIndex]}";
        }
    }
}