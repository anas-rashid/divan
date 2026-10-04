namespace RMuseum.Models.Divan.ViewModels
{
    /// <summary>
    /// a moderator's decision on a pending DivanPersonEditSuggestion
    /// </summary>
    public class PersonEditSuggestionModerationViewModel
    {
        /// <summary>
        /// review result
        /// </summary>
        public CorrectionReviewResult Result { get; set; }

        /// <summary>
        /// review note
        /// </summary>
        public string ReviewNote { get; set; }
    }
}
