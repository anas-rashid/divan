using RSecurityBackend.Models.Auth.ViewModels;
using System;

namespace RMuseum.Models.DivanIntegration.ViewModels
{
    /// <summary>
    /// Divan Link View Model
    /// </summary>
    public class DivanLinkViewModel
    {
        /// <summary>
        /// Id
        /// </summary>
        public Guid Id { get; set; }

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
        /// entity name
        /// </summary>
        public string EntityName { get; set; }

        /// <summary>
        /// entity friendly url
        /// </summary>
        public string EntityFriendlyUrl { get; set; }

        /// <summary>
        /// entity image id
        /// </summary>
        public Guid EntityImageId { get; set; }

        /// <summary>
        /// review result
        /// </summary>
        public ReviewResult ReviewResult { get; set; }

        /// <summary>
        /// Synchronized with divan
        /// </summary>
        public bool Synchronized { get; set; }

        /// <summary>
        /// suggested by
        /// </summary>
        public PublicRAppUser SuggestedBy { get; set; }

        /// <summary>
        /// is text original source
        /// </summary>
        public bool IsTextOriginalSource { get; set; }

        /// <summary>
        /// url to access this image from THE external host, contains '/norm/' which when
        /// you replace it with '/thumb/' you would have ExternalThumbnailImageUrl
        /// and if you replace it with '/orig/' you would have a url for ExternalOriginalSizeImageUrl which MIGHT NOT EXIST and end in a 404 error
        /// </summary>
        public string ExternalNormalSizeImageUrl { get; set; }
    }
}
