using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OrasProject.OrasDesktop.Models
{
    /// <summary>
    /// Represents a repository in an OCI registry
    /// </summary>
    public class Repository
    {
        /// <summary>
        /// Gets or sets the name of the repository
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the full path of the repository including the registry
        /// </summary>
        public string FullPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the parent repository (if any)
        /// </summary>
        public Repository? Parent { get; set; }

        /// <summary>
        /// Gets or sets the children repositories
        /// </summary>
        public List<Repository> Children { get; set; } = new List<Repository>();

        /// <summary>
        /// Gets or sets whether this repository has child repositories
        /// </summary>
        public bool HasChildren => Children.Count > 0;

        /// <summary>
        /// Gets or sets whether this is a leaf repository (containing tags)
        /// </summary>
        public bool IsLeaf { get; set; }

        /// <summary>
        /// Gets or sets the registry this repository belongs to
        /// </summary>
        public Registry? Registry { get; set; }
    }
}