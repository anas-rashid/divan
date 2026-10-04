namespace RMuseum.Models.Divan
{
    /// <summary>
    /// Divan Page Health Check log
    /// </summary>
    public class DivanHealthCheckError
    {
        /// <summary>
        /// id
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// referer url
        /// </summary>
        public string ReferrerPageUrl { get; set; }

        /// <summary>
        /// broken link
        /// </summary>
        public bool BrokenLink { get; set; }

        /// <summary>
        /// multiple targets for a page
        /// </summary>
        public bool MulipleTargets { get; set; }

        /// <summary>
        /// target url
        /// </summary>
        public string TargetUrl { get; set; }
    }
}
