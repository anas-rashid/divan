namespace RMuseum.Models.Divan.ViewModels
{
    /// <summary>
    /// verse translation view mode
    /// </summary>
    public class DivanVerseTranslationViewModel
    {
        /// <summary>
        /// verse
        /// </summary>
        public DivanVerseViewModel Verse { get; set; }


        /// <summary>
        /// translated text
        /// </summary>
        public string TText { get; set; }
    }
}
