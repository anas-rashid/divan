namespace RMuseum.Models.PDFLibrary.ViewModels
{
    /// <summary>
    /// PDFBook Divan Link Suggestion
    /// </summary>
    public class PDFDivanLinkSuggestion
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
        /// pdf book id
        /// </summary>
        public int PDFBookId { get; set; }

        /// <summary>
        /// page number
        /// </summary>
        public int PageNumber { get; set; }

        /// <summary>
        /// is this the text original source?
        /// </summary>
        public bool IsTextOriginalSource { get; set; }
    }
}
