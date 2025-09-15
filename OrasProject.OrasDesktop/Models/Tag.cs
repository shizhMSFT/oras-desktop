using System;

namespace OrasProject.OrasDesktop.Models
{
    /// <summary>
    /// Represents a tag in an OCI repository
    /// </summary>
    public class Tag
    {
        /// <summary>
        /// Gets or sets the name of the tag
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the digest of the tag
        /// </summary>
        public string Digest { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the repository this tag belongs to
        /// </summary>
        public Repository? Repository { get; set; }

        /// <summary>
        /// Gets or sets the timestamp when this tag was created
        /// </summary>
        public DateTimeOffset CreatedAt { get; set; }

        /// <summary>
        /// Gets or sets the size of the tag in bytes
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Gets the full reference for this tag in the format <registry>/<repository>:<tag>
        /// </summary>
        public string FullReference => Repository?.FullPath != null ? $"{Repository.FullPath}:{Name}" : $":{Name}";
    }
}