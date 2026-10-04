using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using DivanRazor.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RMuseum.Models.Divan;
using RMuseum.Models.Divan.ViewModels;

namespace DivanRazor.Areas.User.Pages
{
    public class SectionModel : PageModel
    {
        /// <summary>
        /// fatal error
        /// </summary>
        public string FatalError { get; set; }

        /// <summary>
        /// page
        /// </summary>
        public DivanPageCompleteViewModel PageInformation { get; set; }

        /// <summary>
        /// section
        /// </summary>
        public DivanPoemSection PoemSection { get; set; }


        /// <summary>
        /// rhythms alphabetically
        /// </summary>
        public DivanMetre[] RhythmsAlphabetically { get; set; }

        /// <summary>
        /// rhythms by frequency
        /// </summary>
        public DivanMetre[] RhythmsByVerseCount { get; set; }

        /// <summary>
        /// my last edit
        /// </summary>
        public DivanPoemSectionCorrectionViewModel MyLastEdit { get; set; }

        /// <summary>
        /// verses
        /// </summary>
        public List<DivanVerseViewModel> Verses { get; set; }

        public DivanLanguage[] Languages { get; set; }

        private async Task ReadLanguagesAsync(HttpClient secureClient)
        {
            HttpResponseMessage response = await secureClient.GetAsync($"{APIRoot.Url}/api/translations/languages");
            if (!response.IsSuccessStatusCode)
            {
                FatalError = JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync());
                return;
            }

            Languages = JsonConvert.DeserializeObject<DivanLanguage[]>(await response.Content.ReadAsStringAsync());
        }


        private List<DivanVerseViewModel> _FilterSectionVerses(DivanPoemSection section, DivanVerseViewModel[] verses)
        {
            List<DivanVerseViewModel> sectionVerses = new List<DivanVerseViewModel>();
            foreach (DivanVerseViewModel verse in verses)
            {
                switch (section.VerseType)
                {
                    case VersePoemSectionType.First:
                        if (verse.SectionIndex1 == section.Index)
                            sectionVerses.Add(verse);
                        break;
                    case VersePoemSectionType.Second:
                        if (verse.SectionIndex2 == section.Index)
                            sectionVerses.Add(verse);
                        break;
                    case VersePoemSectionType.Third:
                        if (verse.SectionIndex3 == section.Index)
                            sectionVerses.Add(verse);
                        break;
                    default:
                        if (verse.SectionIndex4 == section.Index)
                            sectionVerses.Add(verse);
                        break;
                }
            }
            return sectionVerses;
        }

        public DivanPoemSection Next { get; set; }

        public DivanPoemSection Previous { get; set; }

        /// <summary>
        /// can edit
        /// </summary>
        public bool CanEdit { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrEmpty(Request.Cookies["Token"]))
                return Redirect("/");

            FatalError = "";
            CanEdit = Request.Cookies["CanEdit"] == "True";

            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    var pageUrlResponse = await secureClient.GetAsync($"{APIRoot.Url}/api/divan/pageurl?id={Request.Query["poemId"]}");
                    if (!pageUrlResponse.IsSuccessStatusCode)
                    {
                        FatalError = JsonConvert.DeserializeObject<string>(await pageUrlResponse.Content.ReadAsStringAsync());
                        return Page();
                    }
                    var pageUrl = JsonConvert.DeserializeObject<string>(await pageUrlResponse.Content.ReadAsStringAsync());

                    var pageQuery = await secureClient.GetAsync($"{APIRoot.Url}/api/divan/page?url={pageUrl}");
                    if (!pageQuery.IsSuccessStatusCode)
                    {
                        FatalError = JsonConvert.DeserializeObject<string>(await pageQuery.Content.ReadAsStringAsync());
                        return Page();
                    }
                    PageInformation = JObject.Parse(await pageQuery.Content.ReadAsStringAsync()).ToObject<DivanPageCompleteViewModel>();

                    var rhythmResponse = await secureClient.GetAsync($"{APIRoot.Url}/api/divan/rhythms?sortOnVerseCount=true");
                    if (!rhythmResponse.IsSuccessStatusCode)
                    {
                        FatalError = JsonConvert.DeserializeObject<string>(await rhythmResponse.Content.ReadAsStringAsync());
                        return Page();
                    }

                    RhythmsByVerseCount = JsonConvert.DeserializeObject<DivanMetre[]>(await rhythmResponse.Content.ReadAsStringAsync());

                    List<DivanMetre> rhythmsByVerseCount = new List<DivanMetre>(RhythmsByVerseCount);
                    rhythmsByVerseCount.Sort((a, b) => a.Rhythm.CompareTo(b.Rhythm));
                    rhythmsByVerseCount.Insert(0, new DivanMetre()
                    {
                        Rhythm = "null"
                    }
                    );
                    rhythmsByVerseCount.Insert(0, new DivanMetre()
                    {
                        Rhythm = ""
                    }
                    );

                    RhythmsAlphabetically = rhythmsByVerseCount.ToArray();

                    PoemSection = PageInformation.Poem.Sections.Where(s => s.Index == int.Parse(Request.Query["index"])).SingleOrDefault();
                    if(PoemSection == null)
                    {
                        FatalError = "این قطعه به طور مستقل قابل ویرایش نیست.";
                        return Page();
                    }
                    if(string.IsNullOrEmpty(PoemSection.Language))
                    {
                        PoemSection.Language = "ur-PK";
                    }

                    int index = Array.IndexOf(PageInformation.Poem.Sections, PoemSection);
                    if(index > 0)
                    {
                        Previous = PageInformation.Poem.Sections[index - 1];
                    }
                    if(index != (PageInformation.Poem.Sections.Length - 1))
                    {
                        Next = PageInformation.Poem.Sections[index + 1];
                    }

                    var editResponse = await secureClient.GetAsync($"{APIRoot.Url}/api/divan/section/correction/last/{PoemSection.Id}");
                    if (!editResponse.IsSuccessStatusCode)
                    {
                        FatalError = JsonConvert.DeserializeObject<string>(await editResponse.Content.ReadAsStringAsync());
                        return Page();
                    }
                    MyLastEdit = JsonConvert.DeserializeObject<DivanPoemSectionCorrectionViewModel>(await editResponse.Content.ReadAsStringAsync());

                    Verses = _FilterSectionVerses(PoemSection, PageInformation.Poem.Verses);

                    await ReadLanguagesAsync(secureClient);
                }
                else
                {
                    FatalError = "لطفاً از دیوان خارج و مجددا به آن وارد شوید.";
                }
            }
            return Page();
        }

        public async Task<IActionResult> OnPostSendSectionCorrectionsAsync(int sectionId, string rhythm, string rhyme, int[] breakFromVIndices, string note, string lang, DivanPoemFormat? format)
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {

                    

                    int? breakFromVerse1VOrder = null;
                    int? breakFromVerse2VOrder = null;
                    int? breakFromVerse3VOrder = null;
                    int? breakFromVerse4VOrder = null;
                    int? breakFromVerse5VOrder = null;
                    int? breakFromVerse6VOrder = null;
                    int? breakFromVerse7VOrder = null;
                    int? breakFromVerse8VOrder = null;
                    int? breakFromVerse9VOrder = null;
                    int? breakFromVerse10VOrder = null;

                    if (breakFromVIndices.Length > 0)
                    {
                        var sectionResponse = await secureClient.GetAsync($"{APIRoot.Url}/api/divan/section/{sectionId}");
                        if (!sectionResponse.IsSuccessStatusCode)
                        {
                            FatalError = JsonConvert.DeserializeObject<string>(await sectionResponse.Content.ReadAsStringAsync());
                            return Page();
                        }
                        PoemSection = JsonConvert.DeserializeObject<DivanPoemSection>(await sectionResponse.Content.ReadAsStringAsync());

                        var pageUrlResponse = await secureClient.GetAsync($"{APIRoot.Url}/api/divan/pageurl?id={PoemSection.PoemId}");
                        if (!pageUrlResponse.IsSuccessStatusCode)
                        {
                            FatalError = JsonConvert.DeserializeObject<string>(await pageUrlResponse.Content.ReadAsStringAsync());
                            return Page();
                        }
                        var pageUrl = JsonConvert.DeserializeObject<string>(await pageUrlResponse.Content.ReadAsStringAsync());

                        var pageQuery = await secureClient.GetAsync($"{APIRoot.Url}/api/divan/page?url={pageUrl}");
                        if (!pageQuery.IsSuccessStatusCode)
                        {
                            FatalError = JsonConvert.DeserializeObject<string>(await pageQuery.Content.ReadAsStringAsync());
                            return Page();
                        }
                        PageInformation = JObject.Parse(await pageQuery.Content.ReadAsStringAsync()).ToObject<DivanPageCompleteViewModel>();

                        Verses = _FilterSectionVerses(PoemSection, PageInformation.Poem.Verses);



                        breakFromVerse1VOrder = Verses[breakFromVIndices[0]].VOrder;

                        if (breakFromVIndices.Length > 1)
                        {
                            breakFromVerse2VOrder = Verses[breakFromVIndices[1]].VOrder;
                        }

                        if (breakFromVIndices.Length > 2)
                        {
                            breakFromVerse3VOrder = Verses[breakFromVIndices[2]].VOrder;
                        }

                        if (breakFromVIndices.Length > 3)
                        {
                            breakFromVerse4VOrder = Verses[breakFromVIndices[3]].VOrder;
                        }

                        if (breakFromVIndices.Length > 4)
                        {
                            breakFromVerse5VOrder = Verses[breakFromVIndices[4]].VOrder;
                        }

                        if (breakFromVIndices.Length > 5)
                        {
                            breakFromVerse6VOrder = Verses[breakFromVIndices[5]].VOrder;
                        }

                        if (breakFromVIndices.Length > 6)
                        {
                            breakFromVerse7VOrder = Verses[breakFromVIndices[6]].VOrder;
                        }

                        if (breakFromVIndices.Length > 7)
                        {
                            breakFromVerse8VOrder = Verses[breakFromVIndices[7]].VOrder;
                        }

                        if (breakFromVIndices.Length > 8)
                        {
                            breakFromVerse9VOrder = Verses[breakFromVIndices[8]].VOrder;
                        }

                        if (breakFromVIndices.Length > 9)
                        {
                            breakFromVerse10VOrder = Verses[breakFromVIndices[9]].VOrder;
                        }
                    }

                    if (rhythm == "null")
                        rhythm = "";


                    DivanPoemSectionCorrectionViewModel correction = new DivanPoemSectionCorrectionViewModel()
                    {
                        SectionId = sectionId,
                        Rhythm = rhythm,
                        RhymeLetters = rhyme,
                        BreakFromVerse1VOrder = breakFromVerse1VOrder,
                        BreakFromVerse2VOrder = breakFromVerse2VOrder,
                        BreakFromVerse3VOrder = breakFromVerse3VOrder,
                        BreakFromVerse4VOrder = breakFromVerse4VOrder,
                        BreakFromVerse5VOrder = breakFromVerse5VOrder,
                        BreakFromVerse6VOrder = breakFromVerse6VOrder,
                        BreakFromVerse7VOrder = breakFromVerse7VOrder,
                        BreakFromVerse8VOrder = breakFromVerse8VOrder,
                        BreakFromVerse9VOrder = breakFromVerse9VOrder,
                        BreakFromVerse10VOrder = breakFromVerse10VOrder,
                        Note = note,
                        Language = lang,
                        PoemFormat = format
                    };

                    HttpResponseMessage response = await secureClient.PostAsync(
                        $"{APIRoot.Url}/api/divan/section/correction",
                        new StringContent(JsonConvert.SerializeObject(correction),
                        Encoding.UTF8,
                        "application/json"));
                    if (!response.IsSuccessStatusCode)
                    {
                        return new BadRequestObjectResult(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }
                    return new OkObjectResult(true);
                }
                else
                {
                    return new BadRequestObjectResult("لطفاً از دیوان خارج و مجددا به آن وارد شوید.");
                }
            }
        }

        public async Task<IActionResult> OnGetComputeRhymeAsync(int id)
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    var response = await secureClient.GetAsync($"{APIRoot.Url}/api/divan/section/analyserhyme/{id}");
                    if (!response.IsSuccessStatusCode)
                    {
                        return new BadRequestObjectResult(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }
                    var rhyme = JsonConvert.DeserializeObject<DivanRhymeAnalysisResult>(await response.Content.ReadAsStringAsync());
                    return new OkObjectResult(rhyme.Rhyme);
                }
                else
                {
                    return new BadRequestObjectResult("لطفاً از دیوان خارج و مجددا به آن وارد شوید.");
                }
            }
        }
    }
}
