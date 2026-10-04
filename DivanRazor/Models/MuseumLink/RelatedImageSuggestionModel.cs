namespace DivanRazor.Models.MuseumLink
{
    /// <summary>
    /// Pinterest or Intstagram image that is being suggested to contain text of a poem
    /// </summary>
    public class RelatedImageSuggestionModel
    {
        /// <summary>
        /// divan title
        /// </summary>
        public string DivanTitle { get; set; }

        /// <summary>
        /// poem id
        /// </summary>
        public int PoemId { get; set; }

        /// <summary>
        /// divan url
        /// </summary>
        public string DivanUrl { get; set; }

        /// <summary>
        /// pinterest page url
        /// </summary>
        public string PinterestUrl { get; set; }

        /// <summary>
        /// pinterest image url
        /// </summary>
        public string PinterestImageUrl { get; set; }

        /// <summary>
        /// description
        /// </summary>
        public string AltText { get; set; }

     
    }
}
