namespace RMuseum.Models.Divan
{
    /// <summary>
    /// Verse translation
    /// </summary>
    public class DivanVerseTranslation
    {
        /// <summary>
        /// id
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// verse id
        /// </summary>
        public int VerseId { get; set; }

        /// <summary>
        /// verse
        /// </summary>
        public DivanVerse Verse { get; set; }

        /// <summary>
        /// translated text
        /// </summary>
        public string TText { get; set; }
    }
}
