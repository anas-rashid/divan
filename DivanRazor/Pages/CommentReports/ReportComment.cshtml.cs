using DivanRazor.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using RMuseum.Models.Divan.ViewModels;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DivanRazor.Pages
{
    public class ReportCommentModel : PageModel
    {
        /// <summary>
        /// is logged on
        /// </summary>
        public bool LoggedIn { get; set; }

        /// <summary>
        /// Last Error
        /// </summary>
        public string LastError { get; set; }

        /// <summary>
        /// Post Success
        /// </summary>
        public bool PostSuccess { get; set; }

        /// <summary>
        /// api model
        /// </summary>
        [BindProperty]
        public DivanPostReportCommentViewModel Report { get; set; }

        public void OnGet()
        {
            PostSuccess = false;
            LastError = "";
            LoggedIn = !string.IsNullOrEmpty(Request.Cookies["Token"]);

            Report = new DivanPostReportCommentViewModel()
            {
                ReasonCode = "bogus",
                ReasonText = "",
            };

            if (!string.IsNullOrEmpty(Request.Query["CommentId"]))
            {
                Report.CommentId = int.Parse(Request.Query["CommentId"]);
            }
            else
            {
                Report.CommentId = 0;
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            PostSuccess = false;
            LastError = "";
            LoggedIn = !string.IsNullOrEmpty(Request.Cookies["Token"]);

            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    var stringContent = new StringContent(JsonConvert.SerializeObject(Report), Encoding.UTF8, "application/json");
                    var methodUrl = $"{APIRoot.Url}/api/divan/comment/report";
                    var response = await secureClient.PostAsync(methodUrl, stringContent);
                    if (!response.IsSuccessStatusCode)
                    {
                        LastError = JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync());
                    }
                    else
                    {
                        PostSuccess = true;
                    }
                }
                else
                {
                    LastError = "لطفاً از دیوان خارج و مجددا به آن وارد شوید.";
                }
            }

            return Page();
        }
    }
}
