namespace RMuseum.Models.Divan
{
    public class DivanVerseNumber
    {
        /// <summary>
        /// id
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Numbering schema Id
        /// </summary>
        public int NumberingId { get; set; }

        /// <summary>
        /// Numbering schema
        /// </summary>
        public DivanNumbering Numbering { get; set; }

        /// <summary>
        /// Poem Id (no relation is defined)
        /// </summary>
        public int PoemId { get; set; }

        /// <summary>
        /// verse couplet index (do not add related verses here)
        /// </summary>
        public int CoupletIndex { get; set; }

        /// <summary>
        /// line number
        /// </summary>
        public int Number { get; set; }

        /// <summary>
        /// is poem verse or paragraph
        /// </summary>
        public bool IsPoemVerse { get; set; }

        /// <summary>
        /// number based on type of line: is it a poem verse or a paragraph
        /// </summary>
        public int SameTypeNumber { get; set; }
    }

}
