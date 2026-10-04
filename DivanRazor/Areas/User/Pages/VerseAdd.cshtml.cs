using DivanRazor.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using RMuseum.Models.Divan.ViewModels;
using System.Net.Http;
using System.Threading.Tasks;
using DivanRazor.Models;
using RMuseum.Models.Divan;
using System.Collections.Generic;
using System;
using System.Text;
using System.Linq;

namespace DivanRazor.Areas.User.Pages
{
    public class VerseAddPageModel : PageModel
    {
        /// <summary>
        /// HttpClient instance
        /// </summary>
        private readonly HttpClient _httpClient;

        /// <summary>
        /// configration file reader (appsettings.json)
        /// </summary>
        private readonly IConfiguration Configuration;

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="httpClient"></param>
        /// <param name="configuration"></param>
        public VerseAddPageModel(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            Configuration = configuration;
        }

        /// <summary>
        /// is logged on
        /// </summary>
        public bool LoggedIn { get; set; }

        /// <summary>
        /// Last Error
        /// </summary>
        public string LastError { get; set; }

        public bool PostSuccess { get; set; }

        [BindProperty]
        public NewVersesModel NewVerses { get; set; }

        public DivanPageCompleteViewModel PageInformation { get; set; }


        public async Task<IActionResult> OnGetAsync()
        {
            if (bool.Parse(Configuration["MaintenanceMode"]))
            {
                return StatusCode(503);
            }

            PostSuccess = false;
            LastError = "";
            LoggedIn = !string.IsNullOrEmpty(Request.Cookies["Token"]);


            if (!string.IsNullOrEmpty(Request.Query["id"]))
            {
                var pageUrlResponse = await _httpClient.GetAsync($"{APIRoot.Url}/api/divan/pageurl?id={Request.Query["id"]}");
                if (!pageUrlResponse.IsSuccessStatusCode)
                {
                    LastError = JsonConvert.DeserializeObject<string>(await pageUrlResponse.Content.ReadAsStringAsync());
                    return Page();
                }
                var pageUrl = JsonConvert.DeserializeObject<string>(await pageUrlResponse.Content.ReadAsStringAsync());

                var pageQuery = await _httpClient.GetAsync($"{APIRoot.Url}/api/divan/page?url={pageUrl}");
                if (!pageQuery.IsSuccessStatusCode)
                {
                    LastError = JsonConvert.DeserializeObject<string>(await pageQuery.Content.ReadAsStringAsync());
                    return Page();
                }
                PageInformation = JObject.Parse(await pageQuery.Content.ReadAsStringAsync()).ToObject<DivanPageCompleteViewModel>();

                NewVerses = new NewVersesModel()
                {
                    PoemId = PageInformation.Id,
                    VOrder = 0,
                    Lines = "",
                };
                
            }
            else
            {
                LastError = "missing parameter: id";
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {

            LastError = "";
            LoggedIn = !string.IsNullOrEmpty(Request.Cookies["Token"]);


            using (HttpClient _httpClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(_httpClient, Request, Response))
                {
                    var pageUrlResponse = await _httpClient.GetAsync($"{APIRoot.Url}/api/divan/pageurl?id={NewVerses.PoemId}");
                    if (!pageUrlResponse.IsSuccessStatusCode)
                    {
                        LastError = JsonConvert.DeserializeObject<string>(await pageUrlResponse.Content.ReadAsStringAsync());
                        return Page();
                    }
                    var pageUrl = JsonConvert.DeserializeObject<string>(await pageUrlResponse.Content.ReadAsStringAsync());

                    var pageQuery = await _httpClient.GetAsync($"{APIRoot.Url}/api/divan/page?url={pageUrl}");
                    if (!pageQuery.IsSuccessStatusCode)
                    {
                        LastError = JsonConvert.DeserializeObject<string>(await pageQuery.Content.ReadAsStringAsync());
                        return Page();
                    }
                    PageInformation = JObject.Parse(await pageQuery.Content.ReadAsStringAsync()).ToObject<DivanPageCompleteViewModel>();
                    VersePosition versePosition = VersePosition.Right;
                    if(PageInformation.Poem.Verses.Any(v => v.VOrder == NewVerses.VOrder))
                    {  
                        if (
                            PageInformation.Poem.Verses.Single(v => v.VOrder == NewVerses.VOrder).VersePosition == VersePosition.Paragraph
                            ||
                            PageInformation.Poem.Verses.Single(v => v.VOrder == NewVerses.VOrder).VersePosition == VersePosition.Single
                            )
                        {
                            versePosition = PageInformation.Poem.Verses.Single(v => v.VOrder == NewVerses.VOrder).VersePosition;
                        }
                    }

                    List<DivanVerseVOrderText> vOrderTexts = new List<DivanVerseVOrderText>();

                    int vOrderNext = 0;
                    foreach (string v in NewVerses.Lines.Split(new char[] { '\r', '\n'}, StringSplitOptions.RemoveEmptyEntries))
                    {
                        vOrderTexts.Add
                                (
                                new DivanVerseVOrderText()
                                {
                                    VORder = NewVerses.VOrder + vOrderNext,
                                    Text = v.Replace("ۀ", "هٔ").Replace("ك", "ک"),
                                    NewVerse= true,
                                    VersePosition = versePosition,
                                }
                                );
                        if(versePosition != VersePosition.Paragraph && versePosition != VersePosition.Single)
                        {
                            versePosition = versePosition == VersePosition.Right ? VersePosition.Left : VersePosition.Right;
                        }
                        vOrderNext++;
                    }
                    DivanPoemCorrectionViewModel correction = new DivanPoemCorrectionViewModel()
                    {
                        PoemId = NewVerses.PoemId,
                        VerseOrderText = vOrderTexts.ToArray(),
                        Note = "پیشنهاد مصرع‌های جاافتاده"
                    };
                    var stringContent = new StringContent(JsonConvert.SerializeObject(correction), Encoding.UTF8, "application/json");
                    var methodUrl = $"{APIRoot.Url}/api/divan/poem/correction";
                    var response = await _httpClient.PostAsync(methodUrl, stringContent);
                    if (!response.IsSuccessStatusCode)
                    {
                        LastError = JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync());
                    }
                    else
                    {
                        PostSuccess = true;
                        return Redirect($"/User/Editor?id={PageInformation.Id}");
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
