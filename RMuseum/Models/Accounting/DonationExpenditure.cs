namespace RMuseum.Models.Accounting
{
    /// <summary>
    /// donation expenditures
    /// </summary>
    public class DonationExpenditure
    {
        /// <summary>
        /// Id
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// DivanDonation Id
        /// </summary>
        public int DivanDonationId { get; set; }

        /// <summary>
        /// DivanDonation
        /// </summary>
        public DivanDonation DivanDonation { get; set; }

        /// <summary>
        /// amount
        /// </summary>
        public decimal Amount { get; set; }
    }
}
