namespace RMuseum.Models.MusicCatalogue
{
    /// <summary>
    /// music catalogue track
    /// </summary>
    public class DivanTrack
    {
        /// <summary>
        /// id
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// album id
        /// </summary>
        public int AlbumId { get; set; }

        /// <summary>
        /// album
        /// </summary>
        public DivanAlbum Album { get; set; }

        /// <summary>
        /// name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// url
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// blocked from suggestion
        /// </summary>
        public bool Blocked { get; set; }

        /// <summary>
        /// reason
        /// </summary>
        /// <example>
        /// track is purely music and no singing is done through it
        /// </example>
        public string BlockReason { get; set; }
    }
}
