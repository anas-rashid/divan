using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using RMuseum.Models.Divan.ViewModels;
using System.Net.Http;
using System.Threading.Tasks;

namespace DivanRazor.Pages
{
    /// <summary>
    /// random couplet (divan: rendered from our own API instead of upstream's external c.ganjoor.net widget)
    /// </summary>
    public class RandomModel : PageModel
    {
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public RandomModel(IConfiguration configuration, HttpClient httpClient)
        {
            _configuration = configuration;
            _httpClient = httpClient;
        }

        /// <summary>
        /// random poem, null when the API is unreachable or empty
        /// </summary>
        public DivanPoemCompleteViewModel Poem { get; set; }

        public async Task OnGetAsync()
        {
            ViewData["TrackingScript"] = _configuration["TrackingScript"];
            var response = await _httpClient.GetAsync($"{APIRoot.Url}/api/divan/poem/random?poetId=0");
            if (response.IsSuccessStatusCode)
                Poem = JsonConvert.DeserializeObject<DivanPoemCompleteViewModel>(await response.Content.ReadAsStringAsync());
        }
    }
}
