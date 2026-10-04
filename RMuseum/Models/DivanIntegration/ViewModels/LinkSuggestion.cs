using System;

namespace RMuseum.Models.DivanIntegration.ViewModels
{
    /// <summary>
    /// Link Suggestion
    /// </summary>
    public class LinkSuggestion
    {
        /// <summary>
        /// Divan Post Id
        /// </summary>
        public int DivanPostId { get; set; }

        /// <summary>
        /// divan url
        /// </summary>
        public string DivanUrl { get; set; }

        /// <summary>
        /// divan title
        /// </summary>
        public string DivanTitle { get; set; }

        /// <summary>
        /// Artifact Friendly Url
        /// </summary>
        public string ArtifactFriendlyUrl { get; set; }

        /// <summary>
        /// Artifact Item Id
        /// </summary>
        public Guid? ItemId { get; set; }
    }
}
