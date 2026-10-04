using DivanRazor.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RMuseum.Models.Divan;
using RMuseum.Models.Divan.ViewModels;
using RSecurityBackend.Models.Auth.ViewModels;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DivanRazor.Areas.Admin.Pages
{
    public class ModifyPageModel : PageModel
    {

        /// <summary>
        /// HttpClient instance
        /// </summary>
        private readonly HttpClient _httpClient;

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="httpClient"></param>
        public ModifyPageModel(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        /// <summary>
        /// Corresponding Ganojoor Page
        /// </summary>
        /// <summary>
        /// api model
        /// </summary>
        [BindProperty]
        public DivanModifyPageViewModel ModifyModel { get; set; }

        /// <summary>
        /// rythm
        /// </summary>
        public DivanMetre[] Rhythms { get; set; }

        /// <summary>
        /// page
        /// </summary>
        public DivanPageCompleteViewModel PageInformation { get; set; }

        /// <summary>
        /// divan toc
        /// </summary>
        [BindProperty]
        public DivanTOC DivanTOC { get; set; }
        /// <summary>
        /// last message
        /// </summary>
        public string LastMessage { get; set; }

        public string DefaultHtmlText { get; set; }

        private static string _DefHtmlText(DivanVerseViewModel[] verses)
        {
            string htmlText = "";
            int coupletIndex = 0;
            VersePosition position = VersePosition.Right;
            for (int vIndex = 0; vIndex < verses.Length; vIndex++)
            {
                DivanVerseViewModel v = verses[vIndex];
                if (position == VersePosition.Right)
                {
                    coupletIndex++;
                    htmlText += $"<div class=\"b\" id=\"bn{coupletIndex}\"><div class=\"m1\"><p>{v.Text}</p></div>{Environment.NewLine}";
                    position = VersePosition.Left;
                }
                else
                {
                    htmlText += $"<div class=\"m2\"><p>{v.Text}</p></div></div>{Environment.NewLine}";
                    position = VersePosition.Right;
                }
            }
            return htmlText.Trim();
        }

        private async Task<bool> PreparePage()
        {

            var rhythmResponse = await _httpClient.GetAsync($"{APIRoot.Url}/api/divan/rhythms");
            if (!rhythmResponse.IsSuccessStatusCode)
            {
                LastMessage = JsonConvert.DeserializeObject<string>(await rhythmResponse.Content.ReadAsStringAsync());
                return false;
            }

            Rhythms = JsonConvert.DeserializeObject<DivanMetre[]>(await rhythmResponse.Content.ReadAsStringAsync());

            if (Request.Query["id"] == "0")
            {
                ModifyModel = new DivanModifyPageViewModel();
                PageInformation = new DivanPageCompleteViewModel();
                return true;
            }

            var pageUrlResponse = await _httpClient.GetAsync($"{APIRoot.Url}/api/divan/pageurl?id={Request.Query["id"]}");
            if (!pageUrlResponse.IsSuccessStatusCode)
            {
                LastMessage = JsonConvert.DeserializeObject<string>(await pageUrlResponse.Content.ReadAsStringAsync());
                return false;
            }

            var pageUrl = JsonConvert.DeserializeObject<string>(await pageUrlResponse.Content.ReadAsStringAsync());

            var pageQuery = await _httpClient.GetAsync($"{APIRoot.Url}/api/divan/page?url={pageUrl}");
            if (!pageQuery.IsSuccessStatusCode)
            {
                LastMessage = JsonConvert.DeserializeObject<string>(await pageQuery.Content.ReadAsStringAsync());
                return false;
            }
            PageInformation = JObject.Parse(await pageQuery.Content.ReadAsStringAsync()).ToObject<DivanPageCompleteViewModel>();

            if (PageInformation.Poem != null)
            {
                DefaultHtmlText = _DefHtmlText(PageInformation.Poem.Verses);
            }

            ModifyModel = new DivanModifyPageViewModel()
            {
                Title = PageInformation.Title,
                UrlSlug = PageInformation.UrlSlug,
                HtmlText = PageInformation.HtmlText,
                Note = "",
                SourceName = PageInformation.Poem == null ? null : PageInformation.Poem.SourceName,
                SourceUrlSlug = PageInformation.Poem == null ? null : PageInformation.Poem.SourceUrlSlug,
                OldTag = PageInformation.Poem == null ? null : PageInformation.Poem.OldTag,
                OldTagPageUrl = PageInformation.Poem == null ? null : PageInformation.Poem.OldTagPageUrl,
                NoIndex = PageInformation.NoIndex,
                RedirectFromFullUrl = PageInformation.RedirectFromFullUrl,
                Language = PageInformation.Poem == null ? null : PageInformation.Poem.Language,
                MixedModeOrder = PageInformation.Poem == null ? (PageInformation.DivanPageType == DivanPageType.CatPage || PageInformation.DivanPageType == DivanPageType.PoetPage ? PageInformation.PoetOrCat.Cat.MixedModeOrder : 0) : PageInformation.Poem.MixedModeOrder,
                Published = PageInformation.Poem == null ? (PageInformation.DivanPageType == DivanPageType.CatPage || PageInformation.DivanPageType == DivanPageType.PoetPage ? PageInformation.PoetOrCat.Cat.Published : true) : PageInformation.Poem.Published,
                TableOfContentsStyle = PageInformation.DivanPageType == DivanPageType.CatPage || PageInformation.DivanPageType == DivanPageType.PoetPage ? PageInformation.PoetOrCat.Cat.TableOfContentsStyle : DivanTOC.Analyse,
                CatType = PageInformation.DivanPageType == DivanPageType.CatPage || PageInformation.DivanPageType == DivanPageType.PoetPage ? PageInformation.PoetOrCat.Cat.CatType : DivanCatType.Default,
                Description = PageInformation.DivanPageType == DivanPageType.CatPage || PageInformation.DivanPageType == DivanPageType.PoetPage ? PageInformation.PoetOrCat.Cat.Description : "",
                DescriptionHtml = PageInformation.DivanPageType == DivanPageType.CatPage || PageInformation.DivanPageType == DivanPageType.PoetPage ? PageInformation.PoetOrCat.Cat.DescriptionHtml : ""
            };
            DivanTOC = ModifyModel.TableOfContentsStyle;
            return true;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrEmpty(Request.Cookies["Token"]))
                return Redirect("/");


            LastMessage = Request.Query["edit"] == "true" ? "ویرایش انجام شد." : "";
            if (string.IsNullOrEmpty(Request.Query["id"]))
            {
                return StatusCode(StatusCodes.Status500InternalServerError, "شناسهٔ صفحه مشخص نیست.");
            }
            DivanTOC = DivanTOC.Analyse;
            await PreparePage();
            
            return Page();
        }

        public async Task<IActionResult> OnGetComputeRhymeAsync(int id)
        {
            var response = await _httpClient.GetAsync($"{APIRoot.Url}/api/divan/poem/analysisrhyme/{id}");
            if (!response.IsSuccessStatusCode)
            {
                return BadRequest(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
            }
            var rhyme = JsonConvert.DeserializeObject<DivanRhymeAnalysisResult>(await response.Content.ReadAsStringAsync());
            return new OkObjectResult(rhyme.Rhyme);
        }

        public async Task<IActionResult> OnPostAsync(DivanModifyPageViewModel ModifyModel)
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    if(Request.Query["id"] == "0")
                    {
                        var response = await secureClient.PostAsync($"{APIRoot.Url}/api/divan/page", new StringContent(JsonConvert.SerializeObject(ModifyModel), Encoding.UTF8, "application/json"));
                        if (!response.IsSuccessStatusCode)
                        {
                            LastMessage = JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync());
                        }
                        else
                        {
                            var newPage = JsonConvert.DeserializeObject<DivanPage>(await response.Content.ReadAsStringAsync());
                            return Redirect($"/Admin/ModifyPage?id={newPage.Id}&edit=true");
                        }
                    }
                    else
                    {
                        var response = await secureClient.PutAsync($"{APIRoot.Url}/api/divan/page/{Request.Query["id"]}", new StringContent(JsonConvert.SerializeObject(ModifyModel), Encoding.UTF8, "application/json"));
                        if (!response.IsSuccessStatusCode)
                        {
                            LastMessage = JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync());
                        }
                        else
                        {
                            return Redirect($"/Admin/ModifyPage?id={Request.Query["id"]}&edit=true");
                        }
                    }
                    
                }
                else
                {
                    LastMessage = "لطفاً از دیوان خارج و مجددا به آن وارد شوید.";
                }
            }
            return Page();
        }

        public async Task<IActionResult> OnPostGenerateCatPageAsync(DivanTOC DivanTOC)
        {
            if (!(await PreparePage()))
                return Page();

            var response = await _httpClient.GetAsync($"{APIRoot.Url}/api/divan/cat?url={PageInformation.FullUrl}&poems=true");
            if (!response.IsSuccessStatusCode)
            {
                LastMessage = JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync());
                return Page();
            }


            var Cat = JsonConvert.DeserializeObject<DivanPoetCompleteViewModel>(await response.Content.ReadAsStringAsync());

            if (Request.Form["directInsert"].Count == 1)
            {
                using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
                {
                    if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                    {
                        var htmlRes = await secureClient.PutAsync($"{APIRoot.Url}/api/divan/cat/toc/{Cat.Cat.Id}/{(int)DivanTOC}", null);
                        if (!htmlRes.IsSuccessStatusCode)
                        {
                            LastMessage = JsonConvert.DeserializeObject<string>(await htmlRes.Content.ReadAsStringAsync());
                            return Page();
                        }

                        return Redirect($"/Admin/ModifyPage?id={Request.Query["id"]}&edit=true");

                    }
                    else
                    {
                        LastMessage = "لطفاً از دیوان خارج و مجددا به آن وارد شوید.";
                    }
                }
                return Page();
            }
            else
            {
                using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
                {
                    if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                    {
                        var htmlRes = await secureClient.GetAsync($"{APIRoot.Url}/api/divan/cat/toc/{Cat.Cat.Id}/{(int)DivanTOC}");
                        if (!htmlRes.IsSuccessStatusCode)
                        {
                            LastMessage = JsonConvert.DeserializeObject<string>(await htmlRes.Content.ReadAsStringAsync());
                            return Page();
                        }

                        ModifyModel.HtmlText = await htmlRes.Content.ReadAsStringAsync();

                        LastMessage = $"متن تولیدی دریافت شد. لطفا آن را کپی کنید و سپس <a href=\"/Admin/ModifyPage?id={System.Net.WebUtility.HtmlEncode(Request.Query["id"])}\">اینجا</a> کلیک کنید و آن را درج نمایید.";


                    }
                    else
                    {
                        LastMessage = "لطفاً از دیوان خارج و مجددا به آن وارد شوید.";
                    }
                }
                return Page();
            }
        }

        public async Task<IActionResult> OnPostRebuildSitemapAsync()
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.PostAsync($"{APIRoot.Url}/api/divan/sitemap", null);
                    if (!response.IsSuccessStatusCode)
                    {
                        return BadRequest(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }
                    return new OkObjectResult(true);
                }
            }
            return new OkObjectResult(false);
        }


        public async Task<IActionResult> OnPostRebuildStatsAsync()
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.PutAsync($"{APIRoot.Url}/api/divan/rebuild/stats", null);
                    if (!response.IsSuccessStatusCode)
                    {
                        return BadRequest(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }
                    return new OkObjectResult(true);
                }
            }
            return new OkObjectResult(false);
        }

        /// <summary>
        /// (re)generate the public git-tracked data export (poets/categories/poems as JSON,
        /// pushed to the configured git remote) — runs as a background job, same as the other
        /// rebuild/regenerate tools on this page; check the Jobs page for progress
        /// </summary>
        public async Task<IActionResult> OnPostRegeneratePublicDataAsync()
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.PostAsync($"{APIRoot.Url}/api/divan/publicdata/batchexport", null);
                    if (!response.IsSuccessStatusCode)
                    {
                        return BadRequest(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }
                    return new OkObjectResult(true);
                }
            }
            return new OkObjectResult(false);
        }

        
        public async Task<IActionResult> OnPostRefillSectionsCoupletCountsAsync()
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.PostAsync($"{APIRoot.Url}/api/divan/fillsectioncoupletcounts", null);
                    if (!response.IsSuccessStatusCode)
                    {
                        return BadRequest(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }
                    return new OkObjectResult(true);
                }
            }
            return new OkObjectResult(false);
        }




        public async Task<IActionResult> OnPostRebuildRelatedPagesAsync()
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.PutAsync($"{APIRoot.Url}/api/divan/quoted/pages/generate", null);
                    if (!response.IsSuccessStatusCode)
                    {
                        return BadRequest(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }
                    return new OkObjectResult(true);
                }
            }
            return new OkObjectResult(false);
        }

        public async Task<IActionResult> OnPostRebuildDigitalSourcesStatsAsync()
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.PostAsync($"{APIRoot.Url}/api/divan/source/stats/rebuild", null);
                    if (!response.IsSuccessStatusCode)
                    {
                        return BadRequest(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }
                    return new OkObjectResult(true);
                }
            }
            return new OkObjectResult(false);
        }


        


        public async Task<IActionResult> OnPostCleanCacheAsync(int id)
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.DeleteAsync($"{APIRoot.Url}/api/divan/page/cache/{id}");
                    if (!response.IsSuccessStatusCode)
                    {
                        return BadRequest(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }
                    return new OkObjectResult(true);
                }
            }
            return new OkObjectResult(false);
        }

        public async Task<IActionResult> OnPostRebuildWordCountsAsync(int poetId)
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.PostAsync($"{APIRoot.Url}/api/divan/wordcounts/rebuild/{poetId}", null);
                    if (!response.IsSuccessStatusCode)
                    {
                        return BadRequest(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }
                    return new OkObjectResult(true);
                }
            }
            return new OkObjectResult(false);
        }

        public async Task<IActionResult> OnPostImportNaskbanPaperSourcesAsync(int poetId, string username, string password)
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    LoginViewModel model = new LoginViewModel()
                    {
                        Username = username,
                        Password = password,
                        ClientAppName = "DivanRazor",
                        Language = "ur-PK"
                    };
                    var stringContent = new StringContent(JsonConvert.SerializeObject(model), Encoding.UTF8, "application/json");

                    HttpResponseMessage response = await secureClient.PutAsync($"{APIRoot.Url}/api/divan/naskban/import/poetbooks/{poetId}", stringContent);
                    if (!response.IsSuccessStatusCode)
                    {
                        return BadRequest(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }
                    return new OkObjectResult(true);
                }
            }
            return new OkObjectResult(false);
        }



        public async Task<IActionResult> OnPostImportMuseumPaperSourcesAsync(int poetId)
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.PutAsync($"{APIRoot.Url}/api/divan/papersources/import/{poetId}", null);
                    if (!response.IsSuccessStatusCode)
                    {
                        return BadRequest(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }
                    return new OkObjectResult(true);
                }
            }
            return new OkObjectResult(false);
        }




    }
}
