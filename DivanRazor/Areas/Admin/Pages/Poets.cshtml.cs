using DivanRazor.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using RMuseum.Models.Divan;
using RMuseum.Models.Divan.ViewModels;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DivanRazor.Areas.Admin.Pages
{
    public class PoetsModel : PageModel
    {
        /// <summary>
        /// last message
        /// </summary>
        public string LastMessage { get; set; }

        /// <summary>
        /// poets
        /// </summary>
        public DivanPoetViewModel[] Poets { get; set; }

        public List<DivanGeoLocation> Locations { get; set; }
        private async Task ReadLocationsAsync()
        {
            LastMessage = "";

            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    var response = await secureClient.GetAsync($"{APIRoot.Url}/api/locations");
                    if (!response.IsSuccessStatusCode)
                    {
                        LastMessage = JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync());
                        return;
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

                    Locations.AddRange(JsonConvert.DeserializeObject<DivanGeoLocation[]>(await response.Content.ReadAsStringAsync()));

                }
                else
                {
                    LastMessage = "لطفاً از دیوان خارج و مجددا به آن وارد شوید.";
                }

            }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (string.IsNullOrEmpty(Request.Cookies["Token"]))
                return Redirect("/");

            LastMessage = "";

            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    var response = await secureClient.GetAsync($"{APIRoot.Url}/api/divan/poets/secure");
                    if (!response.IsSuccessStatusCode)
                    {
                        LastMessage = JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync());
                        return Page();
                    }

                    await ReadLocationsAsync();

                    Poets = JsonConvert.DeserializeObject<DivanPoetViewModel[]>(await response.Content.ReadAsStringAsync());

                }
                else
                {
                    LastMessage = "لطفاً از دیوان خارج و مجددا به آن وارد شوید.";
                }

            }

            return Page();
        }

        public async Task<IActionResult> OnPostExportAllAsync()
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    var response = await secureClient.PostAsync($"{APIRoot.Url}/api/divan/sqlite/batchexport", null);
                    if (!response.IsSuccessStatusCode)
                    {
                        return BadRequest(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }
                    return new OkObjectResult(true);
                }
            }

            return new OkObjectResult(false);
        }

        public async Task<ActionResult> OnPostSavePoetMetaAsync(int id, int birth, int death, int pinorder, bool validbirth, bool validdeath, string birthlocation, string deathlocation)
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    var poet = new DivanPoetViewModel()
                    {
                        Id = id,
                        BirthYearInLHijri = birth,
                        DeathYearInLHijri = death,
                        PinOrder = pinorder,
                        ValidBirthDate = validbirth,
                        ValidDeathDate = validdeath,
                        BirthPlace = birthlocation,
                        DeathPlace = deathlocation
                    };
                    var response = await secureClient.PutAsync($"{APIRoot.Url}/api/divan/poet/{id}", new StringContent(JsonConvert.SerializeObject(poet), Encoding.UTF8, "application/json"));
                    if (!response.IsSuccessStatusCode)
                    {
                        return BadRequest(JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
                    }

                    _memoryCache.Remove($"/api/divan/poets");
                    _memoryCache.Remove($"/api/divan/poet/{id}");

                    return new OkObjectResult(true);


                }
            }

            return new OkObjectResult(false);
        }

        public async Task<IActionResult> OnPostUpdatePeriodsAsync()
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    var response = await secureClient.PostAsync($"{APIRoot.Url}/api/divan/periods", null);
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
        /// memory cache
        /// </summary>
        private readonly IMemoryCache _memoryCache;

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="memoryCache"></param>
        public PoetsModel(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }
    }
}
