namespace ArquivoMate2.API.Utilities
{
    using System;

    /// <summary>
    /// Helper for building cache keys and determining whether an artifact should be cached.
    /// Centralizes prefix mapping so keys can be managed consistently across the codebase.
    /// </summary>
    public static class CacheKeyHelper
    {
        /// <summary>
        /// Builds a cache key and indicates whether the artifact should be cached.
        /// By policy we avoid caching large blobs such as original files and archives.
        /// </summary>
        /// <param name="artifactWire">Artifact wire value (e.g. "file", "thumb", "preview", "metadata", "archive").</param>
        /// <param name="viewId">Document view id used in the key namespace.</param>
        /// <returns>Tuple of (cacheKey, shouldCache).</returns>
        public static (string CacheKey, bool ShouldCache) CacheKeyFor(string artifactWire, Guid viewId)
        {
            if (string.IsNullOrWhiteSpace(artifactWire)) artifactWire = "enc";

            var prefix = artifactWire switch
            {
                "thumb" => "thumb",
                "metadata" => "meta",
                "preview" => "preview",
                "archive" => "archive",
                "file" => "file",
                _ => "enc"
            };

            // Policy: do not cache original file or archive artifacts to prevent Redis growing too large.
            var shouldCache = artifactWire != "file" && artifactWire != "archive";

            var key = $"{prefix}:{viewId}:{artifactWire}";
            return (key, shouldCache);
        }
    }
}
