using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using DivanRazor.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using RMuseum.Models.Divan;

namespace DivanRazor.Areas.User.Pages
{
    public class AllPoemSectionsModel : PageModel
    {
        /// <summary>
        /// fatal error
        /// </summary>
        public string FatalError { get; set; }

        public DivanPoemSection[] PoemSections { get; set; }

        /// <summary>
        /// can edit
        /// </summary>
        public bool CanEdit { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrEmpty(Request.Cookies["Token"]))
                return Redirect("/");
            CanEdit = Request.Cookies["CanEdit"] == "True";
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    var sectionsResponse = await secureClient.GetAsync($"{APIRoot.Url}/api/divan/sections/{Request.Query["id"]}");
                    if (!sectionsResponse.IsSuccessStatusCode)
                    {
                        FatalError = JsonConvert.DeserializeObject<string>(await sectionsResponse.Content.ReadAsStringAsync());
                        return Page();
                    }
                    PoemSections = JsonConvert.DeserializeObject<DivanPoemSection[]>(await sectionsResponse.Content.ReadAsStringAsync());
                }
            }
            return Page();
        }


        public async Task<IActionResult> OnPostRebuildRelatedSectionsAsync(int meterId, string rhyming)
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.PostAsync(
                        $"{APIRoot.Url}/api/divan/sections/updaterelated?metreId={meterId}&rhyme={rhyming}",
                        null
                        );
                    if (!response.IsSuccessStatusCode)
                    {
                        return new BadRequestObjectResult(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }
                    return new OkResult();
                }
                else
                {
                    return new BadRequestObjectResult("لطفاً از دیوان خارج و مجددا به آن وارد شوید.");
                }
            }
        }
    }
}
