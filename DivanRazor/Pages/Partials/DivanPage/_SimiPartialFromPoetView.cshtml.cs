using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RMuseum.Models.Divan.ViewModels;

namespace DivanRazor.Pages
{
    public class _SimiPartialFromPoetViewModel : PageModel
    {
        public List<DivanPoemCompleteViewModel> Poems { get; set; }
    }
}
