using DivanRazor.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using RMuseum.Models.Divan;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
namespace DivanRazor.Areas.Admin.Pages
{
    public class LocationsModel : PageModel
    {
        public string LastMessage { get; set; }
        public DivanGeoLocation[] Locations { get; set; }

        [BindProperty]
        public DivanGeoLocation Location { get; set; }

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
                    Locations = JsonConvert.DeserializeObject<DivanGeoLocation[]>(await response.Content.ReadAsStringAsync());

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

            await ReadLocationsAsync();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(DivanGeoLocation Location)
        {
            LastMessage = "";
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    HttpResponseMessage response = await secureClient.PostAsync($"{APIRoot.Url}/api/locations", new StringContent(JsonConvert.SerializeObject(Location), Encoding.UTF8, "application/json"));
                    if (!response.IsSuccessStatusCode)
                    {
                        LastMessage = JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync());
                    }
                    else
                    {
                        await ReadLocationsAsync();
                    }
                }
                else
                {
                    LastMessage = "لطفاً از دیوان خارج و مجددا به آن وارد شوید.";
                }
            }
            return Page();
        }

        public async Task<IActionResult> OnDeleteAsync(int id)
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    var response = await secureClient.DeleteAsync($"{APIRoot.Url}/api/locations/{id}");

                    if (!response.IsSuccessStatusCode)
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

        public async Task<IActionResult> OnPutEditAsync(int id, string name, double latitude, double longitude)
        {
            using (HttpClient secureClient = new HttpClient(new DivanReloginHandler(Request, Response)))
            {
                if (await DivanSessionChecker.PrepareClient(secureClient, Request, Response))
                {
                    DivanGeoLocation model = new DivanGeoLocation()
                    {
                        Id = id,
                        Name = name,
                        Latitude = latitude,
                        Longitude = longitude
                    };
                    HttpResponseMessage response = await secureClient.PutAsync($"{APIRoot.Url}/api/locations", new StringContent(JsonConvert.SerializeObject(model), Encoding.UTF8, "application/json"));
                    if (!response.IsSuccessStatusCode)
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
