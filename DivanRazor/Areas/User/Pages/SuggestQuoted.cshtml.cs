using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using RMuseum.Models.Divan.ViewModels;
using System.Net.Http;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;
using RMuseum.Models.Divan;
using DivanRazor.Utils;
using System.Text;
using System.Linq;
using System.Net;

namespace DivanRazor.Areas.User.Pages
{
    public class SuggestQuotedModel : PageModel
    {
        public string LastMessage { get; set; }

        /// <summary>
        /// HttpClient instance
        /// </summary>
        private readonly HttpClient _httpClient;

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="httpClient"></param>
        public SuggestQuotedModel(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public DivanPoemCompleteViewModel Poem { get; set; }

        public DivanPoemCompleteViewModel RelatedPoem { get; set; }

        public Tuple<int, string>[] Couplets { get; set; }

        public Tuple<int, string>[] RelatedCouplets { get; set; }

        [BindProperty]
        public DivanQuotedPoemViewModel DivanQuotedPoem { get; set; }



        private Tuple<int, string>[] GetCouplets(DivanVerseViewModel[] verses)
        {
            int coupetIndex = -1;
            string coupletText = "";
            List<Tuple<int, string>> couplets = new List<Tuple<int, string>>();
            int verseIndex = 0;
            bool incompleteCouplet = false;
            while (verseIndex < verses.Length)
            {
                switch (verses[verseIndex].VersePosition)
                {
                    case VersePosition.Comment:
                        incompleteCouplet = false;
                        break;
                    case VersePosition.Paragraph:
                    case VersePosition.Single:
                        incompleteCouplet = false;
                        if (!string.IsNullOrEmpty(coupletText))
                        {
                            couplets.Add(new Tuple<int, string>(coupetIndex, coupletText));
                            coupletText = "";
                        }
                        coupetIndex++;
                        couplets.Add(new Tuple<int, string>(coupetIndex, verses[verseIndex].Text));
                        break;
                    case VersePosition.Right:
                    case VersePosition.CenteredVerse1:
                        incompleteCouplet = false;
                        if (!string.IsNullOrEmpty(coupletText))
                        {
                            couplets.Add(new Tuple<int, string>(coupetIndex, coupletText));
                        }
                        coupetIndex++;
                        coupletText = verses[verseIndex].Text;
                        break;
                    case VersePosition.Left:
                    case VersePosition.CenteredVerse2:
                        incompleteCouplet = true;
                        coupletText += $" - {verses[verseIndex].Text}";
                        break;
                }
                verseIndex++;
            }


            if (incompleteCouplet && !string.IsNullOrEmpty(coupletText))
                couplets.Add(new Tuple<int, string>(coupetIndex, coupletText));

            return couplets.ToArray();
        }

        private async Task<string> Prepare(int poemId, string id)
        {
            LastMessage = "";
            
            var poemQuery = await _httpClient.GetAsync($"{APIRoot.Url}/api/divan/poem/{poemId}");
            if (!poemQuery.IsSuccessStatusCode)
            {
                LastMessage = JsonConvert.DeserializeObject<string>(await poemQuery.Content.ReadAsStringAsync());
                return LastMessage;
            }
            Poem = JObject.Parse(await poemQuery.Content.ReadAsStringAsync()).ToObject<DivanPoemCompleteViewModel>();

            Couplets = GetCouplets(Poem.Verses);
            RelatedCouplets = [];

            if (!string.IsNullOrEmpty(id))
            {

                var quoteQuery = await _httpClient.GetAsync($"{APIRoot.Url}/api/divan/quoted/{id}");
                if (!quoteQuery.IsSuccessStatusCode)
                {
                    LastMessage = JsonConvert.DeserializeObject<string>(await quoteQuery.Content.ReadAsStringAsync());
                    return LastMessage;
                }
                DivanQuotedPoem = JObject.Parse(await quoteQuery.Content.ReadAsStringAsync()).ToObject<DivanQuotedPoemViewModel>();
                if (DivanQuotedPoem.RelatedPoemId != null)
                {
                    var relPoemQuery = await _httpClient.GetAsync($"{APIRoot.Url}/api/divan/poem/{DivanQuotedPoem.RelatedPoemId}");
                    if (!relPoemQuery.IsSuccessStatusCode)
                    {
                        LastMessage = JsonConvert.DeserializeObject<string>(await relPoemQuery.Content.ReadAsStringAsync());
                        return LastMessage;
                    }
                    RelatedPoem = JObject.Parse(await relPoemQuery.Content.ReadAsStringAsync()).ToObject<DivanPoemCompleteViewModel>();

                    RelatedCouplets = GetCouplets(RelatedPoem.Verses);


                    var revQuery = await _httpClient.GetAsync($"{APIRoot.Url}/api/divan/poem/{DivanQuotedPoem.RelatedPoemId}/quoteds/{DivanQuotedPoem.PoemId}");
                    if (!revQuery.IsSuccessStatusCode)
                    {
                        LastMessage = JsonConvert.DeserializeObject<string>(await revQuery.Content.ReadAsStringAsync());
                        return LastMessage;
                    }


                }

            }
            else
            {
                DivanQuotedPoem = new DivanQuotedPoemViewModel()
                {
                    PoemId = poemId,
                    PoetId = Poem.Category.Poet.Id,
                    RelatedPoetId = null,
                    RelatedPoemId = null,
                    IsPriorToRelated = false,
                    ChosenForMainList = true,
                    CachedRelatedPoemPoetDeathYearInLHijri = 0,
                    CachedRelatedPoemPoetName = null,
                    CachedRelatedPoemPoetUrl = null,
                    CachedRelatedPoemPoetImage = null,
                    CachedRelatedPoemFullTitle = null,
                    CachedRelatedPoemFullUrl = null,
                    SortOrder = 1000,
                    Note = "",
                    Published = false,
                    ClaimedByBothPoets = false,
                    IndirectQuotation = false,
                    SamePoemsQuotedCount = 0,
                    RelatedCoupletVerse1 = null,
                    RelatedCoupletVerse1ShouldBeEmphasized = false,
                    RelatedCoupletVerse2 = null,
                    RelatedCoupletVerse2ShouldBeEmphasized = false,
                    RelatedCoupletIndex = null,
                    CoupletVerse1 = Poem.Verses[0].Text,
                    CoupletVerse1ShouldBeEmphasized = false,
                    CoupletVerse2 = Poem.Verses[1].Text,
                    CoupletVerse2ShouldBeEmphasized = false,
                    CoupletIndex = 0,

                };
            }

            
            return LastMessage;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrEmpty(Request.Cookies["Token"]))
                return Redirect("/");

           

            string poemIdString = Request.Query["p"];
            if (string.IsNullOrEmpty(poemIdString))
            {
                if (!string.IsNullOrEmpty(Request.Query["id"]))
                {
                    var quoteQuery = await _httpClient.GetAsync($"{APIRoot.Url}/api/divan/quoted/{Request.Query["id"]}");
                    if (!quoteQuery.IsSuccessStatusCode)
                    {
                        LastMessage = JsonConvert.DeserializeObject<string>(await quoteQuery.Content.ReadAsStringAsync());
                        return Page();
                    }
                    DivanQuotedPoem = JObject.Parse(await quoteQuery.Content.ReadAsStringAsync()).ToObject<DivanQuotedPoemViewModel>();
                    poemIdString = DivanQuotedPoem.PoemId.ToString();
                }
                else
                {
                    LastMessage = "شناسهٔ شعر مشخص نیست.";
                    return Page();
                }

            }

            await Prepare(int.Parse(poemIdString), Request.Query["id"]);



            return Page();
        }

        public async Task<IActionResult> OnPostAsync(DivanQuotedPoemViewModel DivanQuotedPoem)
        {
            try
            {

                await Prepare(DivanQuotedPoem.PoemId, DivanQuotedPoem.Id == Guid.Empty ? null : DivanQuotedPoem.Id.ToString());
                int cIndex = -1;
                foreach (var verse in Poem.Verses)
                {
                    if (verse.VersePosition != VersePosition.Left && verse.VersePosition != VersePosition.CenteredVerse2 && verse.VersePosition != VersePosition.Comment)
                        cIndex++;
                    if (verse.VersePosition != VersePosition.Comment)
                    {
                        verse.CoupletIndex = cIndex;
                    }
                    else
                    {
                        verse.CoupletIndex = null;
                    }
                }
                DivanQuotedPoem.CoupletVerse1 = Poem.Verses.Where(v => v.CoupletIndex == DivanQuotedPoem.CoupletIndex).Count() < 1 ? "" : Poem.Verses.Where(v => v.CoupletIndex == DivanQuotedPoem.CoupletIndex).ToArray()[0].Text;
                DivanQuotedPoem.CoupletVerse2 = Poem.Verses.Where(v => v.CoupletIndex == DivanQuotedPoem.CoupletIndex).Count() < 2 ? "" : Poem.Verses.Where(v => v.CoupletIndex == DivanQuotedPoem.CoupletIndex).ToArray()[1].Text;
                if (DivanQuotedPoem.RelatedPoemId != null && RelatedPoem == null)
                {
                    var relPoemQuery = await _httpClient.GetAsync($"{APIRoot.Url}/api/divan/poem/{DivanQuotedPoem.RelatedPoemId}");
                    if (!relPoemQuery.IsSuccessStatusCode)
                    {
                        LastMessage = JsonConvert.DeserializeObject<string>(await relPoemQuery.Content.ReadAsStringAsync());
                        return Page();
                    }
                    RelatedPoem = JObject.Parse(await relPoemQuery.Content.ReadAsStringAsync()).ToObject<DivanPoemCompleteViewModel>();

                    RelatedCouplets = GetCouplets(RelatedPoem.Verses);
                }
                if(RelatedPoem != null)
                {
                    cIndex = -1;
                    foreach (var verse in RelatedPoem.Verses)
                    {
                        if (verse.VersePosition != VersePosition.Left && verse.VersePosition != VersePosition.CenteredVerse2 && verse.VersePosition != VersePosition.Comment)
                            cIndex++;
                        if (verse.VersePosition != VersePosition.Comment)
                        {
                            verse.CoupletIndex = cIndex;
                        }
                        else
                        {
                            verse.CoupletIndex = null;
                        }
                    }
                }
                

                if (DivanQuotedPoem.RelatedCoupletIndex != null)
                {
                    DivanQuotedPoem.RelatedCoupletVerse1 = RelatedPoem.Verses.Where(v => v.CoupletIndex == DivanQuotedPoem.RelatedCoupletIndex).Count() < 1 ? "" : RelatedPoem.Verses.Where(v => v.CoupletIndex == DivanQuotedPoem.RelatedCoupletIndex).ToArray()[0].Text;
                    DivanQuotedPoem.RelatedCoupletVerse2 = RelatedPoem.Verses.Where(v => v.CoupletIndex == DivanQuotedPoem.RelatedCoupletIndex).Count() < 2 ? "" : RelatedPoem.Verses.Where(v => v.CoupletIndex == DivanQuotedPoem.RelatedCoupletIndex).ToArray()[1].Text;
                }
                using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
                {
                    if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                    {
                        var url = $"{APIRoot.Url}/api/divan/quoted/suggest";
                        var payload = new StringContent(JsonConvert.SerializeObject(DivanQuotedPoem), Encoding.UTF8, "application/json");
                        bool newRecord = DivanQuotedPoem.Id == Guid.Empty;
                        HttpResponseMessage response =
                            newRecord ?
                            await secureClient.PostAsync(url, payload) :
                            await secureClient.PutAsync(url, payload);
                        if (!response.IsSuccessStatusCode)
                        {
                            LastMessage = JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync());
                        }
                        else
                        {
                            Response.Redirect("/User/MySuggestedQuotes");
                        }
                    }
                    else
                    {
                        LastMessage = "لطفاً از دیوان خارج و مجددا به آن وارد شوید.";
                    }

                }
            }
            catch (Exception e)
            {
                LastMessage = e.ToString();
            }
            return Page();
        }

        public async Task<IActionResult> OnDeleteAsync(string id)
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    var response = await secureClient.DeleteAsync($"{APIRoot.Url}/api/divan/quoted?id={id}");

                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        return new BadRequestObjectResult(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }

                }
                else
                {
                    return new BadRequestObjectResult("لطفاً از دیوان خارج و مجددا به آن وارد شوید.");
                }
            }
            return new JsonResult(true);
        }

       
    }
}
