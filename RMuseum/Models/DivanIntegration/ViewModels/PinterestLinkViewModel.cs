using System;

namespace RMuseum.Models.DivanIntegration.ViewModels
{

    /// <summary>
    /// Safe pinterest link view model
    /// </summary>
    public class PinterestLinkViewModel
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

  
        /// <summary>
        /// Suggestion Date
        /// </summary>
        public DateTime SuggestionDate { get; set; }

 
        /// <summary>
        /// User id who reviewed the link
        /// </summary>
        public Guid? ReviewerId { get; set; }

 
        /// <summary>
        /// Review Date
        /// </summary>
        public DateTime ReviewDate { get; set; }

        /// <summary>
        /// review result
        /// </summary>
        public ReviewResult ReviewResult { get; set; }

        /// <summary>
        /// review description
        /// </summary>
        public string ReviewDesc { get; set; }

        /// <summary>
        /// Artifact Id
        /// </summary>
        public Guid? ArtifactId { get; set; }

 
        /// <summary>
        /// Artifact Item Id
        /// </summary>
        public Guid? ItemId { get; set; }

          /// <summary>
        /// Synchronized with divan
        /// </summary>
        public bool Synchronized { get; set; }

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

  
    }
}
