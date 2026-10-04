namespace RMuseum.Models.Divan
{
    /// <summary>
    /// Geo Locations (Cities) referred by Divan Metadata
    /// </summary>
    public class DivanGeoLocation
    {
        /// <summary>
        /// id
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Latitude
        /// </summary>
        public double Latitude { get; set; }

        /// <summary>
        /// Longitude
        /// </summary>
        public double Longitude { get; set; }

        /// <summary>
        /// AI generated
        /// </summary>
        public bool MachineGenerated { get; set; }
    }
}
