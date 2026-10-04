namespace RMuseum.Models.Divan
{
    /// <summary>
    /// Probable Prosody Information for a poem
    /// </summary>
    public class DivanPoemProbableMetre
    {
        /// <summary>
        /// Id
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// poem id
        /// </summary>
        public int PoemId { get; set; }

        /// <summary>
        /// poem
        /// </summary>
        public DivanPoem Poem { get; set; }

        /// <summary>
        /// metre
        /// </summary>
        public string Metre { get; set; }

        /// <summary>
        /// section id
        /// </summary>
        public int SectionId { get; set; }
    }
}
