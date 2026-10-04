using Microsoft.AspNetCore.Mvc.RazorPages;
using RMuseum.Models.Divan.ViewModels;

namespace DivanRazor.Pages
{
    public class _QuotedPoemPartialModel : PageModel
    {
        public DivanQuotedPoemViewModel DivanQuotedPoemViewModel { get; set; }
        public string PoetImageUrl { get; set; }
        public string PoetNickName { get; set; }
        public string BlockClass
        {
            get
            {
                return DivanQuotedPoemViewModel.ClaimedByBothPoets ? "inlinesimi ribbon-parent" : "inlinesimi";
            }
        }
        public bool CanEdit { get; set; }
    }
}
