using Microsoft.AspNetCore.Mvc.RazorPages;
using RMuseum.Models.Divan.ViewModels;

namespace DivanRazor.Pages
{
    public class _FooterPartialModel : PageModel
    {
        public bool StickyEnabled { get; set; }

        public DivanPageCompleteViewModel DivanPage { get; set; }
    }
}
