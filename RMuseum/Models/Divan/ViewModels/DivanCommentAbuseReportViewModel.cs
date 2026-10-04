namespace RMuseum.Models.Divan.ViewModels
{
    /// <summary>
    /// DivanCommentAbuseReport View Model
    /// </summary>
    public class DivanCommentAbuseReportViewModel
    {
        /// <summary>
        /// id
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// reason code for better grouping: offensive, bogus, other
        /// </summary>
        public string ReasonCode { get; set; }

        /// <summary>
        /// some explanotory text provided by reporter
        /// </summary>
        public string ReasonText { get; set; }

        /// <summary>
        /// comment
        /// </summary>
        public DivanCommentFullViewModel Comment { get; set; }
    }
}
