using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using DivanRazor.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RMuseum.Models.Divan;
using RMuseum.Models.Divan.ViewModels;

namespace DivanRazor.Areas.Admin.Pages
{
    public class CatTransModel : PageModel
    {
        /// <summary>
        /// last message
        /// </summary>
        public string LastMessage { get; set; }

        /// <summary>
        /// poems
        /// </summary>
        public DivanDuplicateViewModel[] Poems { get; set; }


        /// <summary>
        /// cat id
        /// </summary>
        public int CatId { get; set; }

        /// <summary>
        /// dest cat Id
        /// </summary>
        [BindProperty]
        public int DestCatId { get; set; }

        private async Task<bool> _GetCatDuplicatesAsync()
        {
            if (string.IsNullOrEmpty(Request.Query["id"]))
            {
                LastMessage = "شناسهٔ بخش مشخص نیست.";
                return false;
            }
            CatId = int.Parse(Request.Query["id"]);
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.GetAsync($"{APIRoot.Url}/api/divan/duplicates/{CatId}");
                    if (!response.IsSuccessStatusCode)
                    {
                        LastMessage = JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync());
                        return false;
                    }
                    else
                    {
                        Poems = JsonConvert.DeserializeObject<DivanDuplicateViewModel[]>(await response.Content.ReadAsStringAsync());

                        if(!string.IsNullOrEmpty(Request.Query["empty"]))
                        {
                            Poems = Poems.Where(p => p.DestPoemId == null).ToArray();
                        }
                    }
                }
                else
                {
                    LastMessage = "لطفاً از دیوان خارج و مجددا به آن وارد شوید.";
                    return false;
                }
            }
            return true;
        }
        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrEmpty(Request.Cookies["Token"]))
                return Redirect("/");

            await _GetCatDuplicatesAsync();


            return Page();
        }

        public async Task<IActionResult> OnPostFindDuplicatesAsync()
        {
            CatId = int.Parse(Request.Query["id"]);
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.PostAsync($"{APIRoot.Url}/api/divan/duplicates/{CatId}/{DestCatId}?hardTry=true", null);
                    if (!response.IsSuccessStatusCode)
                    {
                        LastMessage = JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync());
                        return Page();
                    }
                    else
                    {
                        LastMessage = "فرایند شروع شد.";
                        return Page();
                    }
                }
                else
                {
                    LastMessage = "لطفاً از دیوان خارج و مجددا به آن وارد شوید.";
                    return Page();
                }
            }
        }

        public async Task<IActionResult> OnPostFinalizeAsync()
        {
            CatId = int.Parse(Request.Query["id"]);
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.PutAsync($"{APIRoot.Url}/api/divan/duplicates/finish/{CatId}/{DestCatId}", null);
                    if (!response.IsSuccessStatusCode)
                    {
                        LastMessage = JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync());
                        return Page();
                    }
                    else
                    {
                        LastMessage = "فرایند شروع شد.";
                        return Page();
                    }
                }
                else
                {
                    LastMessage = "لطفاً از دیوان خارج و مجددا به آن وارد شوید.";
                    return Page();
                }
            }
        }

        public async Task<IActionResult> OnDeleteAsync(int id)
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.DeleteAsync($"{APIRoot.Url}/api/divan/duplicates/{id}");
                    if (!response.IsSuccessStatusCode)
                    {
                        LastMessage = JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync());
                        return new BadRequestObjectResult(LastMessage);
                    }
                    else
                    {
                        return new OkResult();
                    }
                }
                else
                {
                    return new BadRequestObjectResult("لطفاً از دیوان خارج و مجددا به آن وارد شوید.");
                }
            }
        }
    }
        
}
