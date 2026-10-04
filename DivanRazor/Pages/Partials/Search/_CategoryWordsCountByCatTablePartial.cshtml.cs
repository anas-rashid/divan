using Microsoft.AspNetCore.Mvc.RazorPages;
using RMuseum.Models.Divan.ViewModels;

namespace DivanRazor.Pages
{
    public class _CategoryWordsCountByCatTablePartialModel : PageModel
    {
        public string Term { get; set; }
        public PoetOrCatWordStat[]  WordStats { get; set; }
        public bool Whole { get; set; }
        public int TotalCount { get; set; }

        public bool Blur { get; set; }
        public string SectionName
        {
            get
            {
                return Whole ? "دیوان" : "این بخش";
            }
        }
    }
}
