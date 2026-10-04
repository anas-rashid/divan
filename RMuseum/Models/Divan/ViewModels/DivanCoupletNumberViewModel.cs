namespace RMuseum.Models.Divan.ViewModels
{
    /// <summary>
    /// Divan Couplet Number View Model
    /// </summary>
    public class DivanCoupletNumberViewModel
    {
        /// <summary>
        /// name
        /// </summary>
        public string NumberingName { get; set; }

        /// <summary>
        /// number
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

        /// <summary>
        /// total couplets
        /// </summary>
        public int TotalLines { get; set; }

        /// <summary>
        /// total poem couplets
        /// </summary>
        public int TotalCouplets { get; set; }

        /// <summary>
        /// total paragraphs
        /// </summary>
        public int TotalParagraphs { get; set; }
    }
}
