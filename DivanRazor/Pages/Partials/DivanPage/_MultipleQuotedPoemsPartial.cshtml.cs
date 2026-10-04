using Microsoft.AspNetCore.Mvc.RazorPages;
using RMuseum.Models.Divan.ViewModels;

namespace DivanRazor.Pages
{
    public class _MultipleQuotedPoemsPartialModel : PageModel
    {
        public DivanQuotedPoemViewModel[] DivanQuotedPoems { get; set; }

        public string PoetImageUrl { get; set; }

        public string PoetNickName { get; set; }

        public bool CanEdit { get; set; }

        public _QuotedPoemPartialModel GetQuotedPoemModel(DivanQuotedPoemViewModel quotedPoem, string poetImageUrl, string poetNickName)
        {
            return new _QuotedPoemPartialModel()
            {
                DivanQuotedPoemViewModel = quotedPoem,
                PoetImageUrl = poetImageUrl,
                PoetNickName = poetNickName,
                CanEdit = CanEdit,
            };
        }
    }
}
