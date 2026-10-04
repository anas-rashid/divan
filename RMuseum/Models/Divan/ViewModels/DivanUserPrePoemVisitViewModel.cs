using System;

namespace RMuseum.Models.Divan.ViewModels
{
    public class DivanUserPrePoemVisitViewModel
    {
        public DateTime? LastVisit { get; set; }
        public int TotalVisits { get; set; }
        public bool KeepTrack { get; set; }
    }
}
