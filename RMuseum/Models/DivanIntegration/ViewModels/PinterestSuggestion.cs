using System;

namespace RMuseum.Models.DivanIntegration.ViewModels
{
    /// <summary>
    /// pinterest / divan link suggestion view model
    /// </summary>
    public class PinterestSuggestion
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
        /// alt text
        /// </summary>
        public string AltText { get; set; }

        /// <summary>
        /// link type
        /// </summary>
        public LinkType LinkType { get; set; }

        /// <summary>
        /// pinterest url
        /// </summary>
        public string PinterestUrl { get; set; }

        /// <summary>
        /// pinterest image url
        /// </summary>
        public string PinterestImageUrl { get; set; }
    }
}
