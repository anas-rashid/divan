using DivanRazor.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using RMuseum.Models.Divan.ViewModels;
using System.Net.Http;
using System.Threading.Tasks;

namespace DivanRazor.Areas.Admin.Pages
{
    public class EarlierVersionModel : PageModel
    {
        /// <summary>
        /// last message
        /// </summary>
        public string LastMessage { get; set; }

        /// <summary>
        /// model
        /// </summary>
        [BindProperty]
        public DivanModifyPageViewModel EarlierVersion { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrEmpty(Request.Cookies["Token"]))
                return Redirect("/");


            LastMessage = "";

            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    var response = await secureClient.GetAsync($"{APIRoot.Url}/api/divan/oldversion/{Request.Query["id"]}");
                    if (!response.IsSuccessStatusCode)
                    {
                        LastMessage = JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync());
                        return Page();
                    }

                    EarlierVersion = JsonConvert.DeserializeObject<DivanModifyPageViewModel>(await response.Content.ReadAsStringAsync());

                }
                else
                {
                    LastMessage = "لطفاً از دیوان خارج و مجددا به آن وارد شوید.";
                }

            }

            return Page();
        }
    }
}
