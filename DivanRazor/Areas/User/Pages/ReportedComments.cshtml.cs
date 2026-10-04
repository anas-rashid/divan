using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using DNTPersianUtils.Core;
using DivanRazor.Utils;
using GSpotifyProxy.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RMuseum.Models.Divan.ViewModels;
using RSecurityBackend.Models.Generic;
using System.Text;

namespace DivanRazor.Areas.User.Pages
{
    public class ReportedCommentsModel : PageModel
    {
        /// <summary>
        /// constructor
        /// </summary>
        public ReportedCommentsModel()
        {
        }

        /// <summary>
        /// Last Error
        /// </summary>
        public string LastError { get; set; }


        /// <summary>
        /// comments
        /// </summary>
        public List<DivanCommentAbuseReportViewModel> Reports { get; set; }

        /// <summary>
        /// pagination links
        /// </summary>
        public List<NameIdUrlImage> PaginationLinks { get; set; }

        
        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrEmpty(Request.Cookies["Token"]))
                return Redirect("/");

            LastError = "";
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    {
                        int pageNumber = 1;
                        if (!string.IsNullOrEmpty(Request.Query["page"]))
                        {
                            pageNumber = int.Parse(Request.Query["page"]);
                        }
                        var response = await secureClient.GetAsync($"{APIRoot.Url}/api/divan/comments/reported?PageNumber={pageNumber}&PageSize=20");
                        if (!response.IsSuccessStatusCode)
                        {
                            LastError = JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync());
                            return Page();
                        }

                        Reports = JArray.Parse(await response.Content.ReadAsStringAsync()).ToObject<List<DivanCommentAbuseReportViewModel>>();

                        string paginnationMetadata = response.Headers.GetValues("paging-headers").FirstOrDefault();
                        if (!string.IsNullOrEmpty(paginnationMetadata))
                        {
                            PaginationMetadata paginationMetadata = JsonConvert.DeserializeObject<PaginationMetadata>(paginnationMetadata);
                            PaginationLinks = new List<NameIdUrlImage>();
                            if (paginationMetadata.totalPages > 1)
                            {
                                if (paginationMetadata.currentPage > 3)
                                {
                                    PaginationLinks.Add
                                        (
                                        new NameIdUrlImage()
                                        {
                                            Name = "صفحهٔ اول",
                                            Url = "/User/ReportedComments/?page=1"
                                        }
                                        );
                                }
                                for (int i = (paginationMetadata.currentPage - 2); i <= (paginationMetadata.currentPage + 2); i++)
                                {
                                    if (i >= 1 && i <= paginationMetadata.totalPages)
                                    {
                                        if (i == paginationMetadata.currentPage)
                                        {

                                            PaginationLinks.Add
                                               (
                                               new NameIdUrlImage()
                                               {
                                                   Name = i.ToPersianNumbers(),
                                               }
                                               );
                                        }
                                        else
                                        {

                                            PaginationLinks.Add
                                                (
                                                new NameIdUrlImage()
                                                {
                                                    Name = i.ToPersianNumbers(),
                                                    Url = $"/User/ReportedComments/?page={i}"
                                                }
                                                );
                                        }
                                    }
                                }
                                if (paginationMetadata.totalPages > (paginationMetadata.currentPage + 2))
                                {

                                    PaginationLinks.Add
                                        (
                                        new NameIdUrlImage()
                                        {
                                            Name = "... ",
                                        }
                                        );

                                    PaginationLinks.Add
                                       (
                                       new NameIdUrlImage()
                                       {
                                           Name = "صفحهٔ آخر",
                                           Url = $"/User/ReportedComments/?page={paginationMetadata.totalPages}"
                                       }
                                       );
                                }
                            }
                        }

                    }
                }
            else
                {
                    LastError = "لطفاً از دیوان خارج و مجددا به آن وارد شوید.";
                }
            return Page();
        }

        public async Task<IActionResult> OnPostModerateComment(int id)
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {

                    var response = await secureClient.DeleteAsync($"{APIRoot.Url}/api/divan/comment/reported/moderate/{id}");

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        return Redirect($"/login?redirect={Request.Path}&error={JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync())}");
                    }

                }
            }
            return new OkResult();
        }

        public async Task<IActionResult> OnDeleteReport(int id)
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    var response = await secureClient.DeleteAsync($"{APIRoot.Url}/api/divan/comment/report/{id}");

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        return Redirect($"/login?redirect={Request.Path}&error={JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync())}");
                    }

                }
            }
            return new OkResult();
        }
    }
}
