using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OrasProject.OrasDesktop.Models
{
    /// <summary>
    /// Represents an OCI registry
    /// </summary>
    public class Registry
    {
        /// <summary>
        /// Gets or sets the URL of the registry
        /// </summary>
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the username for authentication
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the password for authentication
        /// </summary>
        public string Password { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the token for authentication
        /// </summary>
        public string Token { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the registry requires authentication
        /// </summary>
        public bool RequiresAuthentication { get; set; }

        /// <summary>
        /// Gets or sets the type of authentication
        /// </summary>
        public AuthenticationType AuthenticationType { get; set; }

        /// <summary>
        /// Gets or sets whether the connection is secure
        /// </summary>
        public bool IsSecure { get; set; } = true;
    }

    /// <summary>
    /// Authentication types supported by the application
    /// </summary>
    public enum AuthenticationType
    {
        /// <summary>
        /// No authentication
        /// </summary>
        None,

        /// <summary>
        /// Basic authentication with username and password
        /// </summary>
        Basic,

        /// <summary>
        /// Token-based authentication
        /// </summary>
        Token
    }
}