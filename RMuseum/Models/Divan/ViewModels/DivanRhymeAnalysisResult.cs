namespace RMuseum.Models.Divan.ViewModels
{
    /// <summary>
    /// Rhyme Analysis Result
    /// </summary>
    public class DivanRhymeAnalysisResult
    {
        /// <summary>
        /// rhyme
        /// </summary>
        public string Rhyme { get; set; }

        /// <summary>
        /// the verse analysis stopped at
        /// </summary>
        public string FailVerse { get; set; }
    }
}
