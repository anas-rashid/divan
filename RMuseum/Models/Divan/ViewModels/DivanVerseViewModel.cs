namespace RMuseum.Models.Divan.ViewModels
{    /// <summary>
    /// Divan Verse View Model
    /// </summary>
    public class DivanVerseViewModel
    {
        /// <summary>
        /// global id, auto generated (missing in Divan Desktop database)
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// vorder
        /// </summary>
        public int VOrder { get; set; }

        /// <summary>
        /// couplet index
        /// </summary>
        public int? CoupletIndex { get; set; }

        /// <summary>
        /// position
        /// </summary>
        public VersePosition VersePosition { get; set; }

        /// <summary>
        /// DivanPoemSection index
        /// </summary>
        public int? SectionIndex1 { get; set; }

        /// <summary>
        /// second DivanPoemSection index
        /// </summary>
        public int? SectionIndex2 { get; set; }

        /// <summary>
        /// third DivanPoemSection index
        /// </summary>
        public int? SectionIndex3 { get; set; }

        /// <summary>
        /// forth DivanPoemSection index
        /// </summary>
        public int? SectionIndex4 { get; set; }

        /// <summary>
        /// text
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// language id
        /// </summary>
        public int? LanguageId { get; set; }

        /// <summary>
        /// couplet summary
        /// </summary>
        public string CoupletSummary { get; set; }

        /// <summary>
        /// original text (e.g. before a correction). This is a pure view
        /// model (not an EF entity), so no [NotMapped] or migration concerns here.
        /// </summary>
        public string OriginalText { get; set; }

        public override string ToString()
        {
            return Text;
        }
    }
}
