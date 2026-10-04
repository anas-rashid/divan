namespace RMuseum.Models.Divan
{
    /// <summary>
    /// Divan Verse
    /// </summary>
    public class DivanVerse
    {
        /// <summary>
        /// global id, auto generated (missing in Divan Desktop database)
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// poem_id
        /// </summary>
        public int PoemId { get; set; }

        /// <summary>
        /// poem
        /// </summary>
        public DivanPoem Poem { get; set; }

        /// <summary>
        /// vorder
        /// </summary>
        public int VOrder { get; set; }

        /// <summary>
        /// position
        /// </summary>
        public VersePosition VersePosition { get; set; }

        /// <summary>
        /// text
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// couplet index
        /// </summary>
        public int? CoupletIndex { get; set; }

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
        /// language id
        /// </summary>
        public int? LanguageId { get; set; }

        /// <summary>
        /// language
        /// </summary>
        public virtual DivanLanguage Language { get; set; }

        /// <summary>
        /// couplet summary
        /// </summary>
        public string CoupletSummary { get; set; }

        public override string ToString()
        {
            return Text;
        }
    }
}
