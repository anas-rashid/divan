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
    public class EditorModel : PageModel
    {
        /// <summary>
        /// my last edit
        /// </summary>
        public DivanPoemCorrectionViewModel MyLastEdit { get; set; }

        /// <summary>
        /// camelCase JSON of MyLastEdit.GeoDateTags, pre-serialized here (rather than inline in the .cshtml
        /// script block) so the Razor page doesn't embed a C# object-initializer (with its own braces) inside
        /// a JS statement - that combination was tripping the editor's JS/TS syntax check (TS1109) even though
        /// it compiled and ran fine.
        /// </summary>
        public string MyLastEditGeoDateTagsJson =>
            JsonConvert.SerializeObject(
                MyLastEdit?.GeoDateTags ?? Array.Empty<DivanPoemGeoDateTagCorrection>(),
                new JsonSerializerSettings { ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver() }
            );

        /// <summary>
        /// page
        /// </summary>
        public DivanPageCompleteViewModel PageInformation { get; set; }

        /// <summary>
        /// couplets (for the geo/date tag suggestion couplet-selection dropdown), same shape as SuggestQuoted's
        /// </summary>
        public Tuple<int, string>[] Couplets { get; set; }

        /// <summary>
        /// rhythms alphabetically
        /// </summary>
        public DivanMetre[] RhythmsAlphabetically { get; set; }

        /// <summary>
        /// rhythms by frequency
        /// </summary>
        public DivanMetre[] RhythmsByVerseCount { get; set; }

        public bool CanAssignRhythms { get; set; }

        /// <summary>
        /// fatal error
        /// </summary>
        public string FatalError { get; set; }

        public string GetVersePosition(DivanVerseViewModel verse)
        {
            return VersePositionHelper.GetVersePositionString(verse.VersePosition);
        }

        public int GetVerseCoupletNumber(DivanVerseViewModel verse)
        {
            int n = 1;
            VersePosition pre = VersePosition.Right;
            foreach (var v in PageInformation.Poem.Verses)
            {
                if (v.Id == verse.Id)
                {
                    if (pre == VersePosition.CenteredVerse1 && v.VersePosition != VersePosition.CenteredVerse2)
                        n++;
                    return n;
                }
                if (v.VersePosition == VersePosition.Left || v.VersePosition == VersePosition.CenteredVerse2
                    || v.VersePosition == VersePosition.Single || v.VersePosition == VersePosition.Paragraph)
                    n++;
                else
                if (pre == VersePosition.CenteredVerse1)
                    n++;
                pre = v.VersePosition;
            }
            return -1;
        }

        /// <summary>
        /// total number of couplets/lines in the poem (same counting rules as GetVerseCoupletNumber,
        /// just run all the way through) - used to render the small read-progress bar next to the
        /// couplet number in the verse-editing and couplet-summary ("خلاصه و معنی") sections.
        /// </summary>
        public int TotalCoupletsCount =>
            PageInformation?.Poem?.Verses == null || PageInformation.Poem.Verses.Length == 0
            ? 0
            : GetVerseCoupletNumber(PageInformation.Poem.Verses[PageInformation.Poem.Verses.Length - 1]);

        /// <summary>
        /// 0-100 fill percentage for the read-progress bar, for a given couplet number (1-based, as
        /// returned by GetVerseCoupletNumber)
        /// </summary>
        public int GetCoupletProgressPercent(int coupletNumber)
        {
            if (TotalCoupletsCount <= 0 || coupletNumber <= 0)
                return 0;
            return Math.Min(100, coupletNumber * 100 / TotalCoupletsCount);
        }

        /// <summary>
        /// hsl() color for a 0-100 read-progress percentage - red at 0% (just started), green at
        /// 100% (reached the end), smoothly interpolated in between (same traffic-light hue sweep
        /// idea, 0deg..120deg). Used as the end color of the progress bar's fill gradient (see
        /// up-verse-progress-fill in user-panel.css), so the fill itself reads redder near the start
        /// of the poem and greener near the end.
        /// </summary>
        public string GetCoupletProgressColor(int percent)
        {
            int hue = Math.Max(0, Math.Min(100, percent)) * 120 / 100;
            return $"hsl({hue}, 70%, 42%)";
        }


        public PoemRelatedImage TextSourceImage { get; set; }

        public DivanMetre DivanMetre1 { get; set; }

        public DivanMetre DivanMetre2 { get; set; }

        public string RhymeLetters { get; set; }

        /// <summary>
        /// valid for whole poem sections
        /// </summary>
        public DivanPoemFormat? PoemFormat { get; set; }

        /// <summary>
        /// can edit
        /// </summary>
        public bool CanEdit { get; set; }


        /// <summary>
        /// show admin ops
        /// </summary>
        public bool ShowAdminOps { get; set; }

        /// <summary>
        /// locations
        /// </summary>
        public List<DivanGeoLocation> Locations { get; set; }

        /// <summary>
        /// camelCase JSON of Locations (id/name/latitude/longitude only), used client-side to warn
        /// about a new-location suggestion that's actually already in the catalog (same/near
        /// coordinates) or shares a name with a different, already-catalogued place. Pre-serialized
        /// here rather than inline in the .cshtml script block for the same reason as
        /// MyLastEditGeoDateTagsJson - avoids embedding a C# object initializer inside a JS statement.
        /// </summary>
        public string AllLocationsJson =>
            JsonConvert.SerializeObject(
                (Locations ?? new List<DivanGeoLocation>()).Select(l => new { l.Id, l.Name, l.Latitude, l.Longitude }),
                new JsonSerializerSettings { ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver() }
            );

        /// <summary>
        /// people (for the geo/date/person tag's person picker)
        /// </summary>
        public List<DivanRelatedPerson> People { get; set; }

        /// <summary>
        /// camelCase JSON of People (id/name/birthYearInLHijri/deathYearInLHijri only - enough to
        /// tell two same-named people apart in the picker), same pre-serialization rationale as
        /// AllLocationsJson
        /// </summary>
        public string AllPeopleJson =>
            JsonConvert.SerializeObject(
                (People ?? new List<DivanRelatedPerson>()).Select(p => new { p.Id, p.Name, p.BirthYearInLHijri, p.DeathYearInLHijri }),
                new JsonSerializerSettings { ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver() }
            );


        /// <summary>
        /// poem geo date tags
        /// </summary>
        public PoemGeoDateTag[] PoemGeoDateTags { get; set; }

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

        /// <summary>
        /// groups verses into couplets - same logic as SuggestQuoted.cshtml.cs's GetCouplets, duplicated here
        /// rather than shared, so this page doesn't take on a cross-file dependency on that one
        /// </summary>
        /// <param name="verses"></param>
        /// <returns></returns>
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

        /// <summary>
        /// get
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrEmpty(Request.Cookies["Token"]))
                return Redirect("/login");

            FatalError = "";
            CanEdit = Request.Cookies["CanEdit"] == "True";
            // set before any of the early "API call failed" returns below, so the view - which
            // reads Model.Couplets unconditionally near the top of its script block, before the
            // FatalError check further down - never sees a null and throws ArgumentNullException
            // out of Couplets.ToDictionary(...) on a failed request
            Couplets = Array.Empty<Tuple<int, string>>();

            ShowAdminOps = CanEdit && Request.Query["admin"] == "1";
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    var editResponse = await secureClient.GetAsync($"{APIRoot.Url}/api/divan/poem/correction/last/{Request.Query["id"]}");
                    if (!editResponse.IsSuccessStatusCode)
                    {
                        FatalError = JsonConvert.DeserializeObject<string>(await editResponse.Content.ReadAsStringAsync());
                        return Page();
                    }
                    MyLastEdit = JsonConvert.DeserializeObject<DivanPoemCorrectionViewModel>(await editResponse.Content.ReadAsStringAsync());




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

                    var pageUrlResponse = await secureClient.GetAsync($"{APIRoot.Url}/api/divan/pageurl?id={Request.Query["id"]}");
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
                    Couplets = GetCouplets(PageInformation.Poem.Verses);


                    if (PageInformation.Poem.Sections.Where(s => s.SectionType == PoemSectionType.WholePoem && !string.IsNullOrEmpty(s.RhymeLetters)).Any())
                    {
                        RhymeLetters = PageInformation.Poem.Sections.Where(s => s.SectionType == PoemSectionType.WholePoem && !string.IsNullOrEmpty(s.RhymeLetters)).OrderBy(s => s.VerseType).First().RhymeLetters;
                    }

                    if (PageInformation.Poem.Sections.Where(s => s.SectionType == PoemSectionType.WholePoem && s.DivanMetre != null).Any())
                    {
                        DivanMetre1 = PageInformation.Poem.Sections.Where(s => s.SectionType == PoemSectionType.WholePoem && s.DivanMetre != null).OrderBy(s => s.VerseType).First().DivanMetre;
                        if (PageInformation.Poem.Sections.Where(s => s.SectionType == PoemSectionType.WholePoem && s.DivanMetre != null).Count() > 1)
                        {
                            DivanMetre2 = PageInformation.Poem.Sections.Where(s => s.SectionType == PoemSectionType.WholePoem && s.DivanMetre != null).OrderBy(s => s.VerseType).ToList()[1].DivanMetre;
                        }
                    }

                    if (PageInformation.Poem.Sections.Where(s => s.SectionType == PoemSectionType.WholePoem && s.PoemFormat != null).Any())
                    {
                        PoemFormat = PageInformation.Poem.Sections.Where(s => s.SectionType == PoemSectionType.WholePoem && s.PoemFormat != null).OrderBy(s => s.VerseType).First().PoemFormat;
                    }
                    else
                    {
                        PoemFormat = DivanPoemFormat.Unknown;
                    }

                    if (PageInformation.Poem.Images.Where(i => i.IsTextOriginalSource).Any())
                    {
                        TextSourceImage = PageInformation.Poem.Images.Where(i => i.IsTextOriginalSource).First();
                    }

                    CanAssignRhythms = true;
                    if (PageInformation.Poem.Verses.Any(v => v.VersePosition == VersePosition.Paragraph))
                    {
                        CanAssignRhythms = false;
                    }
                    else
                        if (PageInformation.Poem.Sections.Count(s => s.SectionType == PoemSectionType.WholePoem && s.VerseType == VersePoemSectionType.First) > 1)
                    {
                        CanAssignRhythms = false;
                    }
                    else
                        if (PageInformation.Poem.Sections.Length == 0)
                    {
                        CanAssignRhythms = false;
                    }

                    await ReadLanguagesAsync(secureClient);

                    // Locations and PoemGeoDateTags are needed by both the admin direct-edit
                    // table (still gated by ShowAdminOps in the .cshtml) and the regular
                    // contributor-facing suggestion UI, so this fetch is no longer admin-only
                    var responseLocations = await secureClient.GetAsync($"{APIRoot.Url}/api/locations");
                    if (!responseLocations.IsSuccessStatusCode)
                    {
                        FatalError = JsonConvert.DeserializeObject<string>(await responseLocations.Content.ReadAsStringAsync());
                        return Page();
                    }

                    Locations = new List<DivanGeoLocation>();
                    Locations.Add
                        (
                        new DivanGeoLocation()
                        {
                            Id = 0,
                            Latitude = 0,
                            Longitude = 0,
                            Name = ""
                        }
                        );

                    Locations.AddRange(JsonConvert.DeserializeObject<DivanGeoLocation[]>(await responseLocations.Content.ReadAsStringAsync()));

                    var responsePeople = await secureClient.GetAsync($"{APIRoot.Url}/api/people");
                    if (!responsePeople.IsSuccessStatusCode)
                    {
                        FatalError = JsonConvert.DeserializeObject<string>(await responsePeople.Content.ReadAsStringAsync());
                        return Page();
                    }

                    People = JsonConvert.DeserializeObject<List<DivanRelatedPerson>>(await responsePeople.Content.ReadAsStringAsync());


                    var tagsResponse = await secureClient.GetAsync($"{APIRoot.Url}/api/divan/poem/{PageInformation.Id}/geotag");
                    if (!tagsResponse.IsSuccessStatusCode)
                    {
                        FatalError = JsonConvert.DeserializeObject<string>(await tagsResponse.Content.ReadAsStringAsync());
                        return Page();
                    }

                    PoemGeoDateTags = JsonConvert.DeserializeObject<PoemGeoDateTag[]>(await tagsResponse.Content.ReadAsStringAsync());
                }
                else
                {
                    FatalError = "لطفاً از دیوان خارج و مجددا به آن وارد شوید.";
                }
            }
            return Page();
        }

        public async Task<IActionResult> OnPostDeletePoemCorrectionsAsync(int poemid)
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.DeleteAsync(
                        $"{APIRoot.Url}/api/divan/poem/correction/{poemid}");
                    if (!response.IsSuccessStatusCode)
                    {
                        string rawError = await response.Content.ReadAsStringAsync();
                        string errorMessage;
                        try
                        {
                            // normal case: API returns the error as a JSON-encoded string
                            errorMessage = JsonConvert.DeserializeObject<string>(rawError);
                        }
                        catch (JsonException)
                        {
                            // API (or a proxy in front of it) returned something that isn't a
                            // JSON string - e.g. an HTML error page - so don't let that throw
                            // an unhandled exception here; fall back to a generic message.
                            errorMessage = "خطایی در سرور رخ داد. لطفاً بعداً دوباره تلاش کنید.";
                        }
                        return BadRequest(errorMessage);
                    }
                    return new OkObjectResult(true);
                }
            }
            return new BadRequestObjectResult("لطفاً از دیوان خارج و مجددا به آن وارد شوید.");
        }


        public class PoemCorrectionStructure
        {
            public int poemid { get; set; }
            public string[] verseOrderText { get; set; }
            
            public int[] verseOrderMarkedForDelete { get; set; }
            
            public string[] versePositions { get; set; }
            public string rhythm { get; set; }
            public string rhythm2 { get; set; }
            public string rhyme { get; set; }
            public string format { get; set; }
            public string[] verseOrderSummaries { get; set; }
            public string[] verseLanguages { get; set; }
            public string note { get; set; }
            public bool hideMyName { get; set; }
            public RMuseum.Models.Divan.DivanPoemGeoDateTagCorrection[] geoDateTags { get; set; }
        }

        public async Task<IActionResult> OnPostSendPoemCorrectionsAsync([FromBody] PoemCorrectionStructure pcs)
        {
            try
            {
                if (pcs == null)
                {
                    return new BadRequestObjectResult("خطای پیش‌بینی نشده: لطفاً نشانی این شعر را به divan@ganjoor.net ارسال بفرمایید تا بررسی بیشتری انجام شود.");
                }

                using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
                {
                    if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                    {
                        var pageUrlResponse = await secureClient.GetAsync($"{APIRoot.Url}/api/divan/pageurl?id={pcs.poemid}");
                        if (!pageUrlResponse.IsSuccessStatusCode)
                        {
                            FatalError = JsonConvert.DeserializeObject<string>(await pageUrlResponse.Content.ReadAsStringAsync());
                            return new BadRequestObjectResult(FatalError);
                        }
                        var pageUrl = JsonConvert.DeserializeObject<string>(await pageUrlResponse.Content.ReadAsStringAsync());

                        var pageQuery = await secureClient.GetAsync($"{APIRoot.Url}/api/divan/page?url={pageUrl}");
                        if (!pageQuery.IsSuccessStatusCode)
                        {
                            FatalError = JsonConvert.DeserializeObject<string>(await pageQuery.Content.ReadAsStringAsync());
                            return new BadRequestObjectResult(FatalError);
                        }
                        var pageInformation = JObject.Parse(await pageQuery.Content.ReadAsStringAsync()).ToObject<DivanPageCompleteViewModel>();

                        string title = null;
                        List<DivanVerseVOrderText> vOrderTexts = new List<DivanVerseVOrderText>();
                        List<VersePosition?> versePositions = new List<VersePosition?>();
                        foreach (var versePosition in pcs.versePositions)
                        {
                            if (versePosition != null)
                            {
                                versePositions.Add((VersePosition)Enum.Parse(typeof(VersePosition), versePosition));
                            }
                        }
                        foreach (string v in pcs.verseOrderText)
                        {
                            var vParts = v.Split("TextSeparator", StringSplitOptions.RemoveEmptyEntries);
                            int vOrder = int.Parse(vParts[0]);
                            if (vOrder == 0)
                                title = vParts[1].Replace("ۀ", "هٔ").Replace("ك", "ک");
                            else
                            {
                                int? langaugeId = null;
                                if (pcs.verseLanguages[vOrder] != null && !(int.Parse(pcs.verseLanguages[vOrder]) == 1 && pageInformation.Poem.Verses.First(v => v.VOrder == vOrder).LanguageId == null))
                                {
                                    langaugeId = int.Parse(pcs.verseLanguages[vOrder]);
                                }
                                var verse = pageInformation.Poem.Verses.Where(verse => verse.VOrder == vOrder).FirstOrDefault();
                                if (verse == null)
                                {
                                    continue;
                                }
                                string verseText = vParts.Length < 2 || verse.Text == vParts[1] ? null : vParts[1].Replace("ۀ", "هٔ").Replace("ك", "ک");
                                VersePosition? versePos = pageInformation.Poem.Verses.First(v => v.VOrder == vOrder).VersePosition == versePositions[vOrder - 1] ? null : versePositions[vOrder - 1];
                                bool markedForDelete = pcs.verseOrderMarkedForDelete.Any(v => v == vOrder);

                                if (verseText != null || markedForDelete || versePos != null || langaugeId != null)
                                {
                                    vOrderTexts.Add
                                    (
                                    new DivanVerseVOrderText()
                                    {
                                        VORder = vOrder,
                                        Text = verseText,
                                        MarkForDelete = markedForDelete,
                                        VersePosition = versePos,
                                        LanguageId = langaugeId,
                                    }
                                    );
                                }

                            }
                        }

                        string poemSummary = null;
                        foreach (string v in pcs.verseOrderSummaries)
                        {
                            var vParts = v.Split("TextSeparator", System.StringSplitOptions.RemoveEmptyEntries);
                            int vOrder = int.Parse(vParts[0]);
                            if (vOrder == 0)
                            {
                                poemSummary = vParts.Length > 1 ? vParts[1].Replace("ۀ", "هٔ").Replace("ك", "ک") : "";
                                if (poemSummary.Contains("هوش مصنوعی"))
                                {
                                    return new BadRequestObjectResult("لطفاً در صورت ویرایش خروجی‌های هوش مصنوعی عبارت هوش مصنوعی را از متن برگردان یا خلاصه حذف کنید.");
                                }
                            }
                            else
                            {
                                if (vOrderTexts.Where(t => t.VORder == vOrder).Any())
                                {
                                    var existingEntryCoupletSummary = vParts.Length > 1 ? vParts[1].Replace("ۀ", "هٔ").Replace("ك", "ک") : "";
                                    if (existingEntryCoupletSummary.Contains("هوش مصنوعی"))
                                    {
                                        return new BadRequestObjectResult("لطفاً در صورت ویرایش خروجی‌های هوش مصنوعی عبارت هوش مصنوعی را از متن برگردان یا خلاصه حذف کنید.");
                                    }
                                    vOrderTexts.First(t => t.VORder == vOrder).CoupletSummary = existingEntryCoupletSummary;
                                }
                                else
                                {
                                    var coupletSummary = vParts.Length > 1 ? vParts[1].Replace("ۀ", "هٔ").Replace("ك", "ک") : "";
                                    if(coupletSummary.Contains("هوش مصنوعی"))
                                    {
                                        return new BadRequestObjectResult("لطفاً در صورت ویرایش خروجی‌های هوش مصنوعی عبارت هوش مصنوعی را از متن برگردان یا خلاصه حذف کنید.");
                                    }
                                    vOrderTexts.Add
                                    (
                                    new DivanVerseVOrderText()
                                    {
                                        VORder = vOrder,
                                        Text = null,
                                        MarkForDelete = false,
                                        VersePosition = null,
                                        CoupletSummary = coupletSummary,
                                    }
                                    );
                                    
                                }
                            }
                        }

                        if (title == null && poemSummary == null && vOrderTexts.Count == 0 && pcs.rhythm == null && pcs.rhythm2 == null && pcs.rhyme == null && pcs.format == null && (pcs.geoDateTags == null || pcs.geoDateTags.Length == 0))
                            return new BadRequestObjectResult("شما هیچ تغییری در اطلاعات نداده‌اید!");

                        if (pcs.rhythm == "null")
                            pcs.rhythm = "";

                        if (pcs.rhythm2 == "null")
                            pcs.rhythm2 = "";

                        if (pcs.rhythm2 != null)
                        {
                            if (pcs.rhythm == pcs.rhythm2)
                                return new BadRequestObjectResult("وزن اول و دوم یکسانند!");
                        }



                        DivanPoemCorrectionViewModel correction = new DivanPoemCorrectionViewModel()
                        {
                            PoemId = pcs.poemid,
                            Title = title,
                            VerseOrderText = vOrderTexts.ToArray(),
                            Rhythm = pcs.rhythm,
                            Rhythm2 = pcs.rhythm2,
                            RhymeLetters = pcs.rhyme,
                            PoemFormat = string.IsNullOrEmpty(pcs.format) ? (DivanPoemFormat?)null : (DivanPoemFormat)Enum.Parse(typeof(DivanPoemFormat), pcs.format),
                            PoemSummary = poemSummary,
                            Note = pcs.note,
                            HideMyName = pcs.hideMyName,
                            GeoDateTags = pcs.geoDateTags
                        };

                        HttpResponseMessage response = await secureClient.PostAsync(
                            $"{APIRoot.Url}/api/divan/poem/correction",
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
            catch (Exception exp)
            {
                return new BadRequestObjectResult(exp.ToString());
            }
            
        }


        public async Task<IActionResult> OnPostBreakPoemAsync(int poemId, int vOrder)
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.PostAsync(
                        $"{APIRoot.Url}/api/divan/poem/break",
                        new StringContent(JsonConvert.SerializeObject
                        (
                            new PoemVerseOrder()
                            {
                                PoemId = poemId,
                                VOrder = vOrder
                            }
                        ),
                        Encoding.UTF8,
                        "application/json"));
                    if (!response.IsSuccessStatusCode)
                    {
                        return new BadRequestObjectResult(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }
                    return new OkObjectResult(JsonConvert.DeserializeObject<int>(await response.Content.ReadAsStringAsync()));
                }
                else
                {
                    return new BadRequestObjectResult("لطفاً از دیوان خارج و مجددا به آن وارد شوید.");
                }
            }
        }

        public async Task<IActionResult> OnPostUpdateRelatedSectionsAsync(int meterId, string rhyme)
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.PostAsync(
                        $"{APIRoot.Url}/api/divan/sections/updaterelated?metreId={meterId}&rhyme={rhyme}",
                        null
                        );
                    if (!response.IsSuccessStatusCode)
                    {
                        return new BadRequestObjectResult(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }
                    return new OkResult();
                }
                else
                {
                    return new BadRequestObjectResult("لطفاً از دیوان خارج و مجددا به آن وارد شوید.");
                }
            }
        }

        public async Task<IActionResult> OnPostRefillCoupletIndicesAsync(int id)
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.PostAsync(
                        $"{APIRoot.Url}/api/divan/refillcoupletindices/{id}",
                        null
                        );
                    if (!response.IsSuccessStatusCode)
                    {
                        return new BadRequestObjectResult(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }
                    return new OkResult();
                }
                else
                {
                    return new BadRequestObjectResult("لطفاً از دیوان خارج و مجددا به آن وارد شوید.");
                }
            }
        }

        public async Task<IActionResult> OnPostRegeneratePoemSectionsAsync(int id)
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.PostAsync(
                        $"{APIRoot.Url}/api/divan/poem/{id}/sections/regenerate",
                        null
                        );
                    if (!response.IsSuccessStatusCode)
                    {
                        return new BadRequestObjectResult(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }
                    return new OkResult();
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
                    var response = await secureClient.GetAsync($"{APIRoot.Url}/api/divan/poem/analysisrhyme/{id}");
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

        public async Task<IActionResult> OnPostNewGeoDateTagAsync(int poemId, int locationId, string year, int month, string day, bool verifiedDate, bool ignoreInCategory)
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.PostAsync(
                        $"{APIRoot.Url}/api/divan/poem/geotag",
                         new StringContent(JsonConvert.SerializeObject(
                             new PoemGeoDateTag()
                             {
                                 PoemId = poemId,
                                 LocationId = locationId == 0 ? null : locationId,
                                 LunarYear = string.IsNullOrEmpty(year) ? null : int.Parse(year),
                                 LunarMonth = month == 0 ? null : month,
                                 LunarDay = string.IsNullOrEmpty(day) ? null : int.Parse(day),
                                 VerifiedDate = verifiedDate,
                                 IgnoreInCategory = ignoreInCategory,
                             }
                             ),
                        Encoding.UTF8,
                        "application/json")
                        );
                    if (!response.IsSuccessStatusCode)
                    {
                        return new BadRequestObjectResult(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }
                    return new OkResult();
                }
                else
                {
                    return new BadRequestObjectResult("لطفاً از دیوان خارج و مجددا به آن وارد شوید.");
                }
            }
        }

        public async Task<IActionResult> OnDeleteGeoDateTagAsync(int id)
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.DeleteAsync(
                        $"{APIRoot.Url}/api/divan/poem/geotag/{id}"
                        );
                    if (!response.IsSuccessStatusCode)
                    {
                        return new BadRequestObjectResult(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }
                    return new OkResult();
                }
                else
                {
                    return new BadRequestObjectResult("لطفاً از دیوان خارج و مجددا به آن وارد شوید.");
                }
            }
        }

        public async Task<IActionResult> OnPostSaveMetaAsync(int poemId, bool noindex, string redirectfromurl, int mixedmodeorder)
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.PutAsync(
                        $"{APIRoot.Url}/api/divan/poem/adminedit/{poemId}",
                         new StringContent(JsonConvert.SerializeObject(
                             new DivanModifyPageViewModel()
                             {
                                 NoIndex = noindex,
                                 RedirectFromFullUrl = redirectfromurl,
                                 MixedModeOrder = mixedmodeorder,
                             }
                             ),
                        Encoding.UTF8,
                        "application/json")
                        );
                    if (!response.IsSuccessStatusCode)
                    {
                        return new BadRequestObjectResult(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }
                    return new OkResult();
                }
                else
                {
                    return new BadRequestObjectResult("لطفاً از دیوان خارج و مجددا به آن وارد شوید.");
                }
            }
        }
    }
}
