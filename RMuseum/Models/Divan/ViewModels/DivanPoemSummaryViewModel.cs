namespace RMuseum.Models.Divan.ViewModels
{
    /// <summary>
    /// poem summary
    /// </summary>
    public class DivanPoemSummaryViewModel
    {
        /// <summary>
        /// id
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// title
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// url => slug
        /// </summary>
        public string UrlSlug { get; set; }

        /// <summary>
        /// excerpt text
        /// </summary>
        public string Excerpt { get; set; }

        /// <summary>
        /// whole poem sections
        /// </summary>
        public DivanPoemSection[] MainSections { get; set; }

    }
}
