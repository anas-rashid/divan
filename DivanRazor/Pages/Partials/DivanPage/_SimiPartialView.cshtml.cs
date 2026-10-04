using Microsoft.AspNetCore.Mvc.RazorPages;
using RMuseum.Models.Divan;

namespace DivanRazor.Pages
{
    public class _SimiPartialViewModel : PageModel
    {
        public DivanCachedRelatedSection[] RelatedSections { get; set; }

        public string Rhythm { get; set; }

        public string RhymeLetters { get; set; }

        public int Skip { get; set; }

        public int PoemId { get; set; }
        public string PoemFullUrl { get; set; }

        public int SectionIndex { get; set; }
    }
}
