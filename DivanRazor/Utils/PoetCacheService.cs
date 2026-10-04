using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RMuseum.Models.Divan.ViewModels;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace DivanRazor.Utils
{
    /// <summary>
    /// Fetches the poet list and individual poet details from the Divan API, optionally caching
    /// the results in-memory. This logic (fetch-or-return-cached, deserialize, cache if enabled) was
    /// previously copy-pasted as three near-identical private methods (a "get all poets" fetcher, a
    /// "get one poet by id and store on Model.Poet" fetcher, and an "get one poet by id and return as
    /// JSON" AJAX handler) in IndexModel, ContribsModel, HashiehaModel, SearchModel, and SimiModel.
    ///
    /// Registered as a scoped service in Startup.cs; inject via constructor like any other service.
    /// </summary>
    public class PoetCacheService
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _memoryCache;

        public PoetCacheService(HttpClient httpClient, IMemoryCache memoryCache)
        {
            _httpClient = httpClient;
            _memoryCache = memoryCache;
        }

        /// <summary>
        /// Gets the full poet list. Returns (true, poets, null) on success, or (false, null, error)
        /// on failure - callers decide what to do with the error (set LastError and return Page(),
        /// return BadRequest(error), etc.) since that varies per caller.
        ///
        /// Caching is on by default (poet metadata changes rarely and isn't personalized, so this
        /// never makes an ordinary visitor - logged in or not - see stale content). Pass
        /// <paramref name="bypassCache"/> = true (callers pass their page's EditorCacheBypass,
        /// i.e. the CanEdit cookie) so an editor sees their own just-made poet edits immediately
        /// instead of waiting out the cache TTL.
        /// </summary>
        public async Task<(bool success, List<DivanPoetViewModel> poets, string error)> GetPoetsAsync(bool bypassCache = false)
        {
            const string cacheKey = "/api/divan/poets";
            if (!bypassCache && _memoryCache.TryGetValue(cacheKey, out List<DivanPoetViewModel> poets))
            {
                return (true, poets, null);
            }

            var response = await _httpClient.GetAsync($"{APIRoot.Url}/api/divan/poets");
            if (!response.IsSuccessStatusCode)
            {
                return (false, null, JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
            }

            poets = JArray.Parse(await response.Content.ReadAsStringAsync()).ToObject<List<DivanPoetViewModel>>();
            if (!bypassCache)
            {
                _memoryCache.Set(cacheKey, poets, TimeSpan.FromHours(1));
            }
            return (true, poets, null);
        }

        /// <summary>
        /// Gets a single poet's full details by id. Returns (true, poet, null) on success, or
        /// (false, null, error) on failure. See <see cref="GetPoetsAsync"/> for the caching/bypass
        /// rules - identical here.
        /// </summary>
        public async Task<(bool success, DivanPoetCompleteViewModel poet, string error)> GetPoetAsync(int poetId, bool bypassCache = false)
        {
            var cacheKey = $"/api/divan/poet/{poetId}";
            if (!bypassCache && _memoryCache.TryGetValue(cacheKey, out DivanPoetCompleteViewModel poet))
            {
                return (true, poet, null);
            }

            var response = await _httpClient.GetAsync($"{APIRoot.Url}/api/divan/poet/{poetId}");
            if (!response.IsSuccessStatusCode)
            {
                return (false, null, JsonConvert.DeserializeObject<string>(await response.Content.ReadAsStringAsync()));
            }

            poet = JObject.Parse(await response.Content.ReadAsStringAsync()).ToObject<DivanPoetCompleteViewModel>();
            if (!bypassCache)
            {
                _memoryCache.Set(cacheKey, poet, TimeSpan.FromHours(1));
            }
            return (true, poet, null);
        }
    }
}
