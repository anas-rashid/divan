using DivanRazor.Models.MuseumLink;
using DivanRazor.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using RMuseum.Models.DivanIntegration.ViewModels;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DivanRazor.Pages
{
    public class PinModel : PageModel
    {
        /// <summary>
        /// related image suggestion model
        /// </summary>
        [BindProperty]
        public RelatedImageSuggestionModel RelatedImageSuggestionModel { get; set; }

        /// <summary>
        /// ارسال موفق
        /// </summary>
        public bool Succeeded { get; set; }

        /// <summary>
        /// خطا
        /// </summary>
        public string LastError { get; set; }

        /// <summary>
        /// is logged on
        /// </summary>
        public bool LoggedIn { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            Succeeded = false;
            LastError = "";
            LoggedIn = !string.IsNullOrEmpty(Request.Cookies["Token"]);
            if(!LoggedIn)
            {
                LastError = $"برای پیشنهاد تصاویر مرتبط با اشعار لازم است ابتدا با نام کاربری خود وارد دیوان شوید. </p><p><a href=\"/login/?redirect={RelatedImageSuggestionModel.DivanUrl}\")>ورود به دیوان</a>";
            }
            else
            if (Request.Query["final"] == "1")
            {
                using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
                {
                    if(await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                    {
                        PinterestLinkViewModel model = new PinterestLinkViewModel()
                        {
                            DivanPostId = RelatedImageSuggestionModel.PoemId,
                            DivanUrl = $"https://ganjoor.net{RelatedImageSuggestionModel.DivanUrl}",
                            DivanTitle = RelatedImageSuggestionModel.DivanTitle,
                            AltText = RelatedImageSuggestionModel.AltText,
                            LinkType = RMuseum.Models.DivanIntegration.LinkType.Pinterest,
                            PinterestUrl = RelatedImageSuggestionModel.PinterestUrl,
                            PinterestImageUrl = RelatedImageSuggestionModel.PinterestImageUrl
                        };
                        var stringContent = new StringContent(JsonConvert.SerializeObject(model), Encoding.UTF8, "application/json");
                        var response = await secureClient.PostAsync($"{APIRoot.Url}/api/artifacts/pinterest", stringContent);

                        if (response.StatusCode != HttpStatusCode.OK)
                        {
                            LastError = JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync());
                        }
                        else
                        {
                            Succeeded = true;
                        }
                    }
                    else
                    {
                        LastError = "لطفاً از دیوان خارج و مجددا به آن وارد شوید.";
                    }
                }
            }
            return Page();
        }
    }
}
