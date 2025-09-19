using System;
using System.Collections.Generic;

namespace OrasProject.OrasDesktop.Models
{
    /// <summary>
    /// Represents a repository in an OCI registry
    /// </summary>
    public class Repository : IComparable<Repository>
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

        /// <summary>
        /// Compares this repository with another repository for ordering.
        /// </summary>
        /// <param name="other">The repository to compare with this repository.</param>
        /// <returns>
        /// A value indicating the relative order of the repositories being compared.
        /// The return value has these meanings:
        /// Less than zero: This repository precedes other in the sort order.
        /// Zero: This repository occurs in the same position in the sort order as other.
        /// Greater than zero: This repository follows other in the sort order.
        /// </returns>
        public int CompareTo(Repository? other)
        {
            if (other == null)
                return 1;
                
            return string.Compare(Name, other.Name, StringComparison.OrdinalIgnoreCase);
        }
    }
}