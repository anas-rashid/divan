using RMuseum.Models.Artifact;
using RSecurityBackend.Models.Auth.Db;
using System;


namespace RMuseum.Models.DivanIntegration
{
    /// <summary>
    /// pinterest link
    /// </summary>
    public class PinterestLink
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
        /// User Id who suggested the link (this would always be null for anonyous suggestions, kept it so that someday it would be used to integerate DivanLink into this class)
        /// </summary>
        public Guid? SuggestedById { get; set; }

        /// <summary>
        /// Suggestion Date
        /// </summary>
        public DateTime SuggestionDate { get; set; }

        /// <summary>
        /// User who suggested the link
        /// </summary>
        public virtual RAppUser SuggestedBy { get; set; }

        /// <summary>
        /// User id who reviewed the link
        /// </summary>
        public Guid? ReviewerId { get; set; }

        /// <summary>
        /// User who reviewed the link
        /// </summary>
        public virtual RAppUser Reviewer { get; set; }

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
        /// artifact
        /// </summary>
        public virtual RArtifactMasterRecord Artifact { get; set; }

        /// <summary>
        /// Artifact Item Id
        /// </summary>
        public Guid? ItemId { get; set; }

        /// <summary>
        /// Artifact Item
        /// </summary>
        public virtual RArtifactItemRecord Item { get; set; }

        /// <summary>
        /// Synchronized with divan
        /// </summary>
        public bool Synchronized { get; set; }

        /// <summary>
        /// is the is the text original source?
        /// </summary>
        public bool IsTextOriginalSource { get; set; }

        /// <summary>
        /// pdf book id
        /// </summary>
        public int PDFBookId { get; set; }

        /// <summary>
        /// page number
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// naskban link id
        /// </summary>
        public Guid? NaskbanLinkId { get; set; }

        /// <summary>
        /// match percent
        /// </summary>
        public int MatchPercent { get; set; }

        /// <summary>
        /// reviewed by a human
        /// </summary>
        public bool HumanReviewed { get; set; }
    }
}
