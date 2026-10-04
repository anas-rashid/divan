using DivanRazor.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using RMuseum.Models.Divan.ViewModels;
using RSecurityBackend.Models.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;
using RMuseum.Models.Divan;
using System.Text;
using System;
using RMuseum.Utils;
using System.Collections.Generic;
using DivanRazor.Models;

namespace DivanRazor.Areas.Admin.Pages
{
    public class ReviewCatEditsModel : PageModel
    {
        /// <summary>
        /// correction
        /// </summary>
        public DivanCatCorrectionViewModel Correction { get; set; }
        /// <summary>
        /// fatal error
        /// </summary>
        public string FatalError { get; set; }

        /// <summary>
        /// skip
        /// </summary>
        public int Skip { get; set; }

        /// <summary>
        /// total count
        /// </summary>
        public int TotalCount { get; set; }

        /// <summary>
        /// links
        /// </summary>
        public List<string> Links { get; set; }

        /// <summary>
        /// page
        /// </summary>
        public DivanPageCompleteViewModel PageInformation { get; set; }
        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrEmpty(Request.Cookies["Token"]))
                return Redirect("/");

            FatalError = "";
            TotalCount = 0;
            Skip = string.IsNullOrEmpty(Request.Query["skip"]) ? 0 : int.Parse(Request.Query["skip"]);
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    var nextResponse = await secureClient.GetAsync($"{APIRoot.Url}/api/divan/cat/correction/next?skip={Skip}");
                    if (!nextResponse.IsSuccessStatusCode)
                    {
                        FatalError = JsonConvert.DeserializeObject<string>(await nextResponse.Content.ReadAsStringAsync());
                        return Page();
                    }

                    string paginnationMetadata = nextResponse.Headers.GetValues("paging-headers").FirstOrDefault();
                    if (!string.IsNullOrEmpty(paginnationMetadata))
                    {
                        TotalCount = JsonConvert.DeserializeObject<PaginationMetadata>(paginnationMetadata).totalCount;
                    }
                    Correction = JsonConvert.DeserializeObject<DivanCatCorrectionViewModel>(await nextResponse.Content.ReadAsStringAsync());
                    if (Correction != null)
                    {
                        var catQuery = await secureClient.GetAsync($"{APIRoot.Url}/api/divan/cat/{Correction.CatId}");
                        if (!catQuery.IsSuccessStatusCode)
                        {
                            FatalError = JsonConvert.DeserializeObject<string>(await catQuery.Content.ReadAsStringAsync());
                            return Page();
                        }
                        var cat = JsonConvert.DeserializeObject<DivanPoetCompleteViewModel>(await catQuery.Content.ReadAsStringAsync());
                        var pageQuery = await secureClient.GetAsync($"{APIRoot.Url}/api/divan/page?url={cat.Cat.FullUrl}");
                        if (!pageQuery.IsSuccessStatusCode)
                        {
                            FatalError = JsonConvert.DeserializeObject<string>(await pageQuery.Content.ReadAsStringAsync());
                            return Page();
                        }
                        PageInformation = JObject.Parse(await pageQuery.Content.ReadAsStringAsync()).ToObject<DivanPageCompleteViewModel>();

                        Links = DivanHtmlTools.ExtractLinksWithRegex(Correction.DescriptionHtml);

                        Correction.Result = cat.Cat.DescriptionHtml == Correction.DescriptionHtml ? CorrectionReviewResult.NotChanged : CorrectionReviewResult.Approved;

                    }

                }
                else
                {
                    FatalError = "لطفاً از دیوان خارج و مجددا به آن وارد شوید.";
                }
            }
            return Page();
        }

        public IActionResult OnPost()
        {
            Skip = string.IsNullOrEmpty(Request.Query["skip"]) ? 0 : int.Parse(Request.Query["skip"]);
            if (Request.Form["next"].Count == 1)
            {
                return Redirect($"/Admin/ReviewCatEdits/?skip={Skip + 1}");
            }
            return Page();
        }

        public async Task<IActionResult> OnPostSendCorrectionsModerationAsync([FromBody] PoemMoerationStructure pms)
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    var correctionResponse = await secureClient.GetAsync($"{APIRoot.Url}/api/divan/cat/correction/{pms.correctionId}");
                    if (!correctionResponse.IsSuccessStatusCode)
                    {
                        return new BadRequestObjectResult(JsonConvert.DeserializeObject<string>(await correctionResponse.Content.ReadAsStringAsync()));
                    }

                    Correction = JsonConvert.DeserializeObject<DivanCatCorrectionViewModel>(await correctionResponse.Content.ReadAsStringAsync());

                    if (Correction.DescriptionHtml != null)
                    {
                        if (pms.titleReviewResult == null)
                        {
                            return new BadRequestObjectResult("لطفاً تغییرات متن را بازبینی کنید.");
                        }
                        else
                        {
                            Correction.Result = (CorrectionReviewResult)Enum.Parse(typeof(CorrectionReviewResult), pms.titleReviewResult);
                            Correction.ReviewNote = pms.titleReviewNote;
                        }
                    }
                    

                    var moderationResponse = await secureClient.PostAsync($"{APIRoot.Url}/api/divan/cat/correction/moderate",
                        new StringContent(JsonConvert.SerializeObject(Correction), Encoding.UTF8, "application/json"
                        ));

                    if (!moderationResponse.IsSuccessStatusCode)
                    {
                        string err = await moderationResponse.Content.ReadAsStringAsync();
                        if (string.IsNullOrEmpty(err))
                        {
                            if (!string.IsNullOrEmpty(moderationResponse.ReasonPhrase))
                            {
                                err = moderationResponse.ReasonPhrase;
                            }
                            else
                            {
                                err = $"Error Code: {moderationResponse.StatusCode}";
                            }
                        }
                        else
                        {
                            err = JsonConvert.DeserializeObject<string>(err);
                        }
                        return new BadRequestObjectResult(err);
                    }

                    return new OkObjectResult(true);
                }
                else
                {
                    return new BadRequestObjectResult("لطفاً از دیوان خارج و مجدداً به آن وارد شوید.");
                }
            }
        }
    }
}
