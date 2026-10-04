using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using DivanRazor.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using RMuseum.Models.Auth.Memory;
using RMuseum.Models.Divan;

namespace DivanRazor.Areas.User.Pages
{
    public class LanguageEditModel : PageModel
    {
        [BindProperty]
        public DivanLanguage Language { get; set; }

        public string LastMessage { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrEmpty(Request.Cookies["Token"]))
                return Redirect("/");

            LastMessage = "";
            await DivanSessionChecker.ApplyPermissionsToViewData(Request, Response, ViewData);
            if (!ViewData.ContainsKey($"{RMuseumSecurableItem.DivanEntityShortName}-{RMuseumSecurableItem.Translations}"))
            {
                LastMessage = "شما به این بخش دسترسی ندارید.";
                return Page();
            }

            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.GetAsync($"{APIRoot.Url}/api/translations/languages/{Request.Query["id"]}");
                    if (!response.IsSuccessStatusCode)
                    {
                        LastMessage = JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync());
                        return Page();
                    }

                    Language = JsonConvert.DeserializeObject<DivanLanguage>(await response.Content.ReadAsStringAsync());

                }
                else
                {
                    LastMessage = "لطفاً از دیوان خارج و مجددا به آن وارد شوید.";
                }
            }


            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                await DivanSessionChecker.PrepareClient(secureClient, Request, Response);
                HttpResponseMessage response = await secureClient.PutAsync($"{APIRoot.Url}/api/translations/languages", new StringContent(JsonConvert.SerializeObject(Language), Encoding.UTF8, "application/json"));
                if (!response.IsSuccessStatusCode)
                {
                    LastMessage = JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync());
                    return Page();
                }

                return Redirect("/User/Languages");
            }
        }
    }
}
