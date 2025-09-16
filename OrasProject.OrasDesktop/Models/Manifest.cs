using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace OrasProject.OrasDesktop.Models
{
    /// <summary>
    /// Represents an OCI manifest
    /// </summary>
    public class Manifest
    {
        /// <summary>
        /// Gets or sets the raw JSON content of the manifest
        /// </summary>
        public string RawContent { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the parsed JObject of the manifest
        /// </summary>
        public JObject? Content { get; set; }

        /// <summary>
        /// Gets or sets the digest of the manifest
        /// </summary>
        public string Digest { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the media type of the manifest
        /// </summary>
        public string MediaType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the tag this manifest belongs to
        /// </summary>
        public Tag? Tag { get; set; }

        /// <summary>
        /// Gets or sets the schema version of the manifest
        /// </summary>
        public int SchemaVersion { get; set; }

        /// <summary>
        /// Gets or sets the config reference in the manifest
        /// </summary>
        public ManifestReference? Config { get; set; }

        /// <summary>
        /// Gets or sets the layers in the manifest
        /// </summary>
        public List<ManifestReference> Layers { get; set; } = new List<ManifestReference>();

        /// <summary>
        /// Parse a manifest from its raw JSON content
        /// </summary>
        /// <param name="json">The JSON content to parse</param>
        /// <returns>A parsed Manifest object</returns>
        public static Manifest Parse(string json)
        {
            var jObject = JObject.Parse(json);
            var manifest = new Manifest
            {
                RawContent = json,
                Content = jObject,
                SchemaVersion = jObject["schemaVersion"]?.Value<int>() ?? 0,
                MediaType = jObject["mediaType"]?.Value<string>() ?? string.Empty
            };

            // Parse config
            if (jObject["config"] is JObject config)
            {
                manifest.Config = new ManifestReference
                {
                    MediaType = config["mediaType"]?.Value<string>() ?? string.Empty,
                    Digest = config["digest"]?.Value<string>() ?? string.Empty,
                    Size = config["size"]?.Value<long>() ?? 0
                };
            }

            // Parse layers
            if (jObject["layers"] is JArray layers)
            {
                foreach (JObject layer in layers)
                {
                    manifest.Layers.Add(new ManifestReference
                    {
                        MediaType = layer["mediaType"]?.Value<string>() ?? string.Empty,
                        Digest = layer["digest"]?.Value<string>() ?? string.Empty,
                        Size = layer["size"]?.Value<long>() ?? 0
                    });
                }
            }

            return manifest;
        }
    }

    /// <summary>
    /// Represents a reference in an OCI manifest (layer or config)
    /// </summary>
    public class ManifestReference
    {
        /// <summary>
        /// Gets or sets the media type of the reference
        /// </summary>
        public string MediaType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the digest of the reference
        /// </summary>
        public string Digest { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the size of the reference in bytes
        /// </summary>
        public long Size { get; set; }
    }
}