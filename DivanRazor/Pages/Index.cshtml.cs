using DivanRazor.Utils;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RMuseum.Models.Divan.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace DivanRazor.Pages
{
    /// <summary>
    /// The site's home page only. Used to also serve every poem/poet/category page (dispatching
    /// internally on Request.Path == "/") - that responsibility moved to DivanPageModel, which now
    /// owns the site's catch-all route (see Startup.cs). This page keeps its own automatic "/" route,
    /// unaffected by that change.
    /// </summary>
    [OutputCache(PolicyName = "DivanPublicPage")]
    public class IndexModel : LoginPartialEnabledPageModel
    {
        private readonly PoetCacheService _poetCache;

        /// <summary>
        /// constructor
        /// </summary>
        public IndexModel(IConfiguration configuration,
            HttpClient httpClient,
            PoetCacheService poetCache
            ) : base(httpClient, configuration)
        {
            _poetCache = poetCache;
        }

        public bool OfflineMode => GetConfigFlag("OfflineMode");

        public bool ReadOnlyMode => GetConfigFlag("ReadOnlyMode");

        /// <summary>
        /// last error
        /// </summary>
        public string LastError { get; set; }

        /// <summary>
        /// If the response failed, stores the API's error message in <see cref="LastError"/> and
        /// returns true so the caller can short-circuit.
        /// </summary>
        private async Task<bool> CaptureErrorIfFailedAsync(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
            {
                return false;
            }
            LastError = await ReadErrorMessageAsync(response);
            return true;
        }

        /// <summary>
        /// Poets (for the home page's own search-form author dropdown)
        /// </summary>
        public List<DivanPoetViewModel> Poets { get; set; }

        private async Task<bool> preparePoets()
        {
            var (success, poets, error) = await _poetCache.GetPoetsAsync(EditorCacheBypass);
            if (!success)
            {
                LastError = error;
                return false;
            }
            Poets = poets;
            return true;
        }

        /// <summary>
        /// poets grouped by century, shown on the home page
        /// </summary>
        public List<DivanCenturyViewModel> PoetGroups { get; set; }

        private async Task<bool> _PreparePoetGroups()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{APIRoot.Url}/api/divan/centuries");
                if (await CaptureErrorIfFailedAsync(response))
                {
                    return false;
                }
                PoetGroups = JArray.Parse(await response.Content.ReadAsStringAsync()).ToObject<List<DivanCenturyViewModel>>();
                return true;
            }
            catch
            {
                LastError = "دیوان کی ویب سروس تک رسائی میں خرابی";
                return false;
            }
        }

        /// <summary>
        /// Handles the legacy "?p=&lt;id&gt;" query-string form by resolving it to the page's real
        /// URL and redirecting there. Needed here too (not just on DivanPageModel): query strings
        /// don't affect ASP.NET Core route matching, so a link like "/?p=123" always has
        /// Path == "/" and is always routed to this page regardless of the split.
        /// </summary>
        private async Task<IActionResult> RedirectByPageIdAsync(string pageId)
        {
            var pageUrlResponse = await _httpClient.GetAsync($"{APIRoot.Url}/api/divan/pageurl?id={pageId}");
            if (await CaptureErrorIfFailedAsync(pageUrlResponse))
            {
                return Page();
            }
            var pageUrl = JsonConvert.DeserializeObject<string>(await pageUrlResponse.Content.ReadAsStringAsync());
            return Redirect(pageUrl);
        }

        /// <summary>
        /// JSON endpoint for the home page's "مرور کتابها" horizontal book shelf (see bk.js's
        /// initHomeBooksShelf()). Fetched client-side via "?Handler=BookCatalog" rather than hitting
        /// the api/divan/book-catalog API route directly from browser JS, same as every other
        /// client-side data fetch on this site - the browser only ever talks to this page's own
        /// handlers, which proxy to the API server-side.
        /// </summary>
        public async Task<IActionResult> OnGetBookCatalogAsync()
        {
            var response = await _httpClient.GetAsync($"{APIRoot.Url}/api/divan/book-catalog");
            if (!response.IsSuccessStatusCode)
            {
                return new BadRequestObjectResult(await ReadErrorMessageAsync(response));
            }
            var books = JsonConvert.DeserializeObject<DivanBookViewModel[]>(await response.Content.ReadAsStringAsync());
            return new OkObjectResult(books);
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var maintenanceResult = TryGetMaintenanceModeResult();
            if (maintenanceResult != null)
            {
                return maintenanceResult;
            }

            LastError = "";
            InitializeCommonPageState();

            if (!string.IsNullOrEmpty(Request.Query["p"]))
            {
                return await RedirectByPageIdAsync(Request.Query["p"]);
            }

            if (!await preparePoets())
            {
                return Page();
            }

            if (!await _PreparePoetGroups())
            {
                return Page();
            }

            // A fresh/forked install has an empty (or nearly empty) database: no poets, or no
            // century grouping yet. The view below assumes at least the "popular poets" group
            // (Id == 0) exists — PoetGroups.Where(g => g.Id == 0).Single() — and throws on an
            // empty database instead of rendering something useful. Steer the visitor toward
            // fixing that instead of letting them hit an unhandled exception: log in first if
            // needed, then straight to the admin page that can seed real content.
            // divan: the home page is public. No poets yet -> public "being prepared" notice (admins get an
            // import link in the view); poets but no century groups yet (e.g. mid-import) -> one flat group.
            NoContentYet = IsDatabaseEffectivelyEmpty();

            ViewData["Title"] = "دیوان";

            return Page();
        }

        /// <summary>
        /// true if there are no poets to show yet
        /// </summary>
        private bool IsDatabaseEffectivelyEmpty()
        {
            return Poets == null || Poets.Count == 0 || PoetGroups == null;
        }

        /// <summary>
        /// true when there is nothing to list yet (fresh install or import not started)
        /// </summary>
        public bool NoContentYet { get; set; }

        public async Task<IActionResult> OnGetPoetInformationAsync(int id)
        {
            if (id == 0)
            {
                return new OkObjectResult(null);
            }
            var (success, poet, error) = await _poetCache.GetPoetAsync(id, EditorCacheBypass);
            if (!success)
            {
                return BadRequest(error);
            }
            return new OkObjectResult(poet);
        }
    }
}
