using System;

namespace OrasProject.OrasDesktop.Models
{
    /// <summary>
    /// Represents the size information of an image for a specific platform in a multi-architecture index
    /// </summary>
    public class PlatformImageSize
    {
        /// <summary>
        /// Gets or sets the platform identifier (e.g., "linux/amd64", "linux/arm64")
        /// </summary>
        public string Platform { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the size of the image in bytes
        /// </summary>
        public long SizeInBytes { get; set; }
        
        /// <summary>
        /// Gets or sets the relative size percentage compared to the largest platform image
        /// </summary>
        public double RelativePercentage { get; set; } = 100.0;
        
        /// <summary>
        /// Gets or sets the digest of the platform-specific manifest
        /// </summary>
        public string Digest { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets a human-readable representation of the size
        /// </summary>
        public string HumanReadableSize => FormatSize(SizeInBytes);
        
        /// <summary>
        /// Formats a byte count as a human-readable string with appropriate units
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