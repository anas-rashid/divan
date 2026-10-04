namespace RMuseum.Models.Divan
{
    /// <summary>
    /// Temporary Model contianing duplicated poems information
    /// </summary>
    public class DivanDuplicate
    {
        /// <summary>
        /// Id
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// source category id (this category is about to be deleted and redirected)
        /// </summary>
        public int SrcCatId { get; set; }

        /// <summary>
        /// source poem id
        /// </summary>
        public int SrcPoemId { get; set; }

        /// <summary>
        /// source poem
        /// </summary>
        public DivanPoem SrcPoem { get; set; }

        /// <summary>
        /// destination poem id
        /// </summary>
        public int? DestPoemId { get; set; }

        /// <summary>
        /// destination poem
        /// </summary>
        public virtual DivanPoem DestPoem { get; set; }
    }
}
