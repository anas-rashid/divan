using Audit.WebApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using RMuseum.Models.Auth.Memory;
using RMuseum.Models.Auth.ViewModel;
using RMuseum.Models.Divan;
using RMuseum.Models.Divan.PublicExport;
using RMuseum.Models.Divan.ViewModels;
using RMuseum.Models.DivanAudio.ViewModels;
using RMuseum.Models.DivanIntegration;
using RMuseum.Services;
using RMuseum.Services.Implementation;
using RSecurityBackend.Models.Auth.Memory;
using RSecurityBackend.Models.Auth.ViewModels;
using RSecurityBackend.Models.Generic;
using RSecurityBackend.Models.Image;
using RSecurityBackend.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace RMuseum.Controllers
{
    [Produces("application/json")]
    [Route("api/divan")]
    public class DivanController : Controller
    {
        /// <summary>
        /// get list of published poets without their biography
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("poets")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPoetViewModel[]))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetPoets()
        {
            var cacheKey = $"divan/poets";
            if (!_memoryCache.TryGetValue(cacheKey, out DivanPoetViewModel[] poets))
            {
                RServiceResult<DivanPoetViewModel[]> res =
                await _divanService.GetPoets(true, false);
                if (!string.IsNullOrEmpty(res.ExceptionString))
                    return BadRequest(res.ExceptionString);

                poets = res.Result;
                if (AggressiveCacheEnabled)
                    _memoryCache.Set(cacheKey, poets, TimeSpan.FromHours(1));

            }
            return Ok(poets);
        }

        /// <summary>
        /// get list of books (DivanCat entries whose CatType is Book, sorted alphabetically by name),
        /// optionally filtered by (part of) name and/or poet id. Not to be confused with
        /// GetBooksAsync()/"books" above, which lists DivanCat entries by their (separate,
        /// legacy) BookName field for cover-image generation.
        /// </summary>
        /// <param name="name">optional, only books whose name contains this</param>
        /// <param name="poetId">optional, only books belonging to this poet</param>
        /// <returns></returns>
        [HttpGet]
        [Route("book-catalog")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanBookViewModel[]))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetBookCatalog(string name = null, int? poetId = null)
        {
            // only the common, unfiltered call (the one the home page's book search will use to load
            // the full list once and filter client-side) is worth caching - a filtered call is cheap
            // on a table this small (well under 200 rows) and caching every name/poetId combination
            // would just grow the cache for no benefit.
            if (string.IsNullOrEmpty(name) && poetId == null)
            {
                var cacheKey = $"divan/book-catalog";
                if (!_memoryCache.TryGetValue(cacheKey, out DivanBookViewModel[] cachedBooks))
                {
                    RServiceResult<DivanBookViewModel[]> cachedRes =
                        await _divanService.GetBookCatalogAsync();
                    if (!string.IsNullOrEmpty(cachedRes.ExceptionString))
                        return BadRequest(cachedRes.ExceptionString);

                    cachedBooks = cachedRes.Result;
                    if (AggressiveCacheEnabled)
                        _memoryCache.Set(cacheKey, cachedBooks, TimeSpan.FromHours(1));
                }
                return Ok(cachedBooks);
            }

            RServiceResult<DivanBookViewModel[]> res =
                await _divanService.GetBookCatalogAsync(name, poetId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// gets list of poets grouped by centuries (first one is the pinned ones)
        /// </summary>
        /// <returns></returns>

        [HttpGet]
        [Route("centuries")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanCenturyViewModel[]))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetCenturiesAsync()
        {
            var cacheKey = $"divan/centuries";
            if (!_memoryCache.TryGetValue(cacheKey, out DivanCenturyViewModel[] centuries))
            {
                RServiceResult<DivanCenturyViewModel[]> res =
                await _divanService.GetCenturiesAsync();
                if (!string.IsNullOrEmpty(res.ExceptionString))
                    return BadRequest(res.ExceptionString);

                centuries = res.Result;
                if (AggressiveCacheEnabled)
                    _memoryCache.Set(cacheKey, centuries, TimeSpan.FromHours(1));

            }
            return Ok(centuries);
        }


        /// <summary>
        /// get list of all poets (including unpublished ones) with their bio
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("poets/secure")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPoetViewModel[]))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetAllPoets()
        {
            RServiceResult<DivanPoetViewModel[]> res =
                 await _divanService.GetPoets(false, true);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// poet by id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="catPoems"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("poet/{id}")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPoetCompleteViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetPoetById(int id, bool catPoems = false)
        {
            var cacheKey = $"poet/byid/{id}";
            if (!_memoryCache.TryGetValue(cacheKey, out DivanPoetCompleteViewModel poet))
            {
                RServiceResult<DivanPoetCompleteViewModel> res =
                await _divanService.GetPoetById(id, catPoems);
                if (!string.IsNullOrEmpty(res.ExceptionString))
                    return BadRequest(res.ExceptionString);
                if (res.Result == null)
                    return NotFound();
                poet = res.Result;
                if (AggressiveCacheEnabled)
                    _memoryCache.Set(cacheKey, poet, TimeSpan.FromHours(1));
            }
            return Ok(poet);
        }

        /// <summary>
        /// poet by url
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("poet")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPoetCompleteViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetPoetByUrl(string url)
        {
            var cacheKey = $"poet/byurl/{url}";
            if (!_memoryCache.TryGetValue(cacheKey, out DivanPoetCompleteViewModel poet))
            {
                RServiceResult<DivanPoetCompleteViewModel> res =
                 await _divanService.GetPoetByUrl(url);
                if (!string.IsNullOrEmpty(res.ExceptionString))
                    return BadRequest(res.ExceptionString);
                if (res.Result == null)
                    return NotFound();
                poet = res.Result;
                if (AggressiveCacheEnabled)
                    _memoryCache.Set(cacheKey, poet, TimeSpan.FromHours(1));
            }
            return Ok(poet);
        }

        /// <summary>
        /// update poet info (except for image)
        /// </summary>
        /// <param name="id"></param>
        /// <param name="poet"></param>
        /// <returns></returns>
        [HttpPut]
        [Route("poet/{id}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(bool))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> ModifyPoet(int id, [FromBody] DivanPoetViewModel poet)
        {

            if (!string.IsNullOrEmpty(poet.ImageUrl))
            {
                return BadRequest("Please send an empty image url, if you are trying to change poet image this is not the right method to do it.");
            }

            Guid userId =
             new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

            poet.Id = id;

            var res = await _divanService.UpdatePoetAsync(poet, userId);

            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);

            return Ok(res.Result);
        }

        /// <summary>
        /// regenerate half centuries
        /// </summary>
        /// <returns></returns>

        [HttpPost]
        [Route("periods")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPoetCompleteViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> RegenerateHalfCenturiesAsync()
        {
            var res = await _divanService.RegenerateHalfCenturiesAsync();

            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);

            _memoryCache.Remove($"divan/centuries");

            return Ok(res.Result);
        }

        /// <summary>
        /// create new poet
        /// </summary>
        /// <param name="poet"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("poet")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPoetCompleteViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> CreatePoet([FromBody] DivanPoetViewModel poet)
        {

            if (!string.IsNullOrEmpty(poet.ImageUrl))
            {
                return BadRequest("Please send an empty image url, if you are trying to change poet image this is not the right method to do it.");
            }

            Guid userId =
             new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

            var res = await _divanService.AddPoetAsync(poet, userId);

            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);

            return Ok(res.Result);
        }

        /// <summary>
        /// starts deleting poet job
        /// `</summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("poet/{id}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public IActionResult StartDeletePoet(int id)
        {
            var res = _divanService.StartDeletePoet(id);

            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);

            return Ok();
        }

        private const string PoetImagePlaceholderSvg =
            "<svg xmlns='http://www.w3.org/2000/svg' width='200' height='200' viewBox='0 0 200 200'>" +
            "<rect width='200' height='200' fill='#efe7d6'/>" +
            "<circle cx='100' cy='78' r='34' fill='#c9b896'/>" +
            "<path d='M40 176c6-38 32-58 60-58s54 20 60 58z' fill='#c9b896'/></svg>";

        /// <summary>
        /// get poet image with png ext
        /// </summary>
        /// <param name="url">sample: hafez</param>
        /// <returns></returns>
        [HttpGet("poet/image/{url}.png")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(FileStreamResult))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetPoetImageInPng(string url)
        {
            return await GetPoetImage(url.Replace(".png", ".gif"));
        }

        /// <summary>
        /// get poet image
        /// </summary>
        /// <param name="url">sample: hafez</param>
        /// <param name="nocache"></param>
        /// <returns></returns>
        [HttpGet("poet/image/{url}.gif")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(FileStreamResult))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetPoetImage(string url, bool nocache = false)
        {

            var cacheKey = $"poet/image/{url}.gif";
            var cacheKeyForLastModified = $"{cacheKey}/lastModified";
            if (nocache && _memoryCache.TryGetValue(cacheKey, out string oldImagePath))
            {
                _memoryCache.Remove(cacheKey);
            }
            if (!_memoryCache.TryGetValue(cacheKey, out string imagePath) || !_memoryCache.TryGetValue(cacheKeyForLastModified, out DateTime lastModified))
            {
                RServiceResult<Guid> poet = await _divanService.GetPoetImageIdByUrl($"/{url}");
                if (!string.IsNullOrEmpty(poet.ExceptionString))
                    return BadRequest(poet.ExceptionString);

                if (poet.Result == Guid.Empty)
                {
                    // divan: portraits are optional (deferred); serve a neutral placeholder instead of a broken image
                    Response.Headers.CacheControl = "public,max-age=86400";
                    return Content(PoetImagePlaceholderSvg, "image/svg+xml");
                }


                RServiceResult<RImage> img =
                    await _imageFileService.GetImage(poet.Result);

                if (!string.IsNullOrEmpty(img.ExceptionString))
                    return BadRequest(img.ExceptionString);

                if (img.Result == null)
                    return NotFound();

                lastModified = img.Result.LastModified;
                lastModified = new DateTime(lastModified.Year, lastModified.Month, lastModified.Day, lastModified.Hour, lastModified.Minute, lastModified.Second, lastModified.Kind);



                RServiceResult<string> imgPath = _imageFileService.GetImagePath(img.Result);
                if (!string.IsNullOrEmpty(imgPath.ExceptionString))
                    return BadRequest(imgPath.ExceptionString);

                imagePath = imgPath.Result;


                _memoryCache.Set(cacheKey, imagePath, TimeSpan.FromHours(1));
                _memoryCache.Set(cacheKeyForLastModified, lastModified, TimeSpan.FromHours(1));

            }

            Response.GetTypedHeaders().LastModified = lastModified;
            Response.Headers.CacheControl = nocache ? "no-cache" : "public,max-age=86400";

            var requestHeaders = Request.GetTypedHeaders();
            if (requestHeaders.IfModifiedSince.HasValue &&
                requestHeaders.IfModifiedSince.Value >= lastModified)
            {
                return StatusCode(StatusCodes.Status304NotModified);
            }

            return new FileStreamResult(new FileStream(imagePath, FileMode.Open, FileAccess.Read), "image/gif");

        }

        /// <summary>
        /// set poet image
        /// </summary>
        /// <param name="id">poet image</param>
        /// <returns></returns>
        [HttpPost("poet/image/{id}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(bool))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType((int)HttpStatusCode.Forbidden, Type = typeof(string))]
        public async Task<IActionResult> UploadPoetImage(int id)
        {
            try
            {
                var poet = await _divanService.GetPoetById(id, false);
                IFormFile file = Request.Form.Files[0];
                RServiceResult<RImage> image = await _imageFileService.Add(file, null, file.FileName, Path.Combine(Configuration.GetSection("PictureFileService")["StoragePath"], "PoetImages"));
                if (!string.IsNullOrEmpty(image.ExceptionString))
                {
                    return BadRequest(image.ExceptionString);
                }
                image = await _imageFileService.Store(image.Result);
                if (!string.IsNullOrEmpty(image.ExceptionString))
                {
                    return BadRequest(image.ExceptionString);
                }

                var res = await _divanService.ChangePoetImageAsync(id, image.Result.Id);

                if (!string.IsNullOrEmpty(res.ExceptionString))
                    return BadRequest(res.ExceptionString);

                if (res.Result)
                {
                    var cacheKey = $"poet/image/{poet.Result.Cat.UrlSlug}.gif";
                    var cacheKeyForLastModified = $"{cacheKey}/lastModified";
                    if (_memoryCache.TryGetValue(cacheKey, out string imagePath))
                    {
                        _memoryCache.Remove(cacheKey);
                    }
                    if (_memoryCache.TryGetValue(cacheKeyForLastModified, out DateTime cachedLastModified))
                    {
                        _memoryCache.Remove(cacheKeyForLastModified);
                    }
                }

                return Ok(res.Result);
            }
            catch (Exception exp)
            {
                return BadRequest(exp.ToString());
            }
        }

        /// <summary>
        /// cat by id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="poems"></param>
        /// <param name="mainSections"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("cat/{id}")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPoetCompleteViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetCatById(int id, bool poems = true, bool mainSections = false)
        {
            var cacheKey = $"cat/byid/{id}/{poems}";
            if (!_memoryCache.TryGetValue(cacheKey, out DivanPoetCompleteViewModel cat))
            {
                RServiceResult<DivanPoetCompleteViewModel> res =
               await _divanService.GetCatById(id, poems, mainSections);
                if (!string.IsNullOrEmpty(res.ExceptionString))
                    return BadRequest(res.ExceptionString);
                if (res.Result == null)
                    return NotFound();
                cat = res.Result;
                if (AggressiveCacheEnabled)
                {
                    _memoryCache.Set(cacheKey, cat, TimeSpan.FromHours(1));
                }
            }
            return Ok(cat);
        }

        /// <summary>
        /// cat by full url
        /// </summary>
        /// <param name="url"></param>
        /// <param name="poems"></param>
        /// <param name="mainSections"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("cat")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPoetCompleteViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetCatByUrl(string url, bool poems = true, bool mainSections = false)
        {
            var cacheKey = $"cat/byurl/{url}/{poems}";
            if (!_memoryCache.TryGetValue(cacheKey, out DivanPoetCompleteViewModel cat))
            {
                RServiceResult<DivanPoetCompleteViewModel> res =
                 await _divanService.GetCatByUrl(url, poems, mainSections);
                if (!string.IsNullOrEmpty(res.ExceptionString))
                    return BadRequest(res.ExceptionString);
                if (res.Result == null)
                    return NotFound();
                cat = res.Result;
                if (AggressiveCacheEnabled)
                    _memoryCache.Set(cacheKey, cat, TimeSpan.FromHours(1));
            }


            return Ok(cat);
        }

        /// <summary>
        /// set category extra info
        /// </summary>
        /// <param name="id"></param>
        /// <returns>only RImageId field is valid</returns>
        [HttpPut("cat/extra/{id}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanCatViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        [ProducesResponseType((int)HttpStatusCode.Forbidden, Type = typeof(string))]
        public async Task<IActionResult> SetCategoryExtraInfo(int id)
        {
            try
            {
                Guid? imageId = null;
                if (Request.Form.Files.Count > 0)
                {
                    IFormFile file = Request.Form.Files[0];
                    RServiceResult<RImage> image = await _imageFileService.Add(file, null, file.FileName, Path.Combine(Configuration.GetSection("PictureFileService")["StoragePath"], "CategoryImages"));
                    if (!string.IsNullOrEmpty(image.ExceptionString))
                    {
                        return BadRequest(image.ExceptionString);
                    }
                    image = await _imageFileService.Store(image.Result);
                    if (!string.IsNullOrEmpty(image.ExceptionString))
                    {
                        return BadRequest(image.ExceptionString);
                    }
                    imageId = image.Result.Id;
                }

                var res = await _divanService.SetCategoryExtraInfo(id, Request.Form["bookName"], imageId, bool.Parse(Request.Form["sumUpSubsGeoLocations"]), Request.Form["mapName"]);

                if (!string.IsNullOrEmpty(res.ExceptionString))
                {
                    return BadRequest(res.ExceptionString);
                }

                if (res.Result == null)
                {
                    return NotFound();
                }


                return Ok(res.Result);
            }
            catch (Exception exp)
            {
                return BadRequest(exp.ToString());
            }
        }

        /// <summary>
        /// generate missing book covers
        /// </summary>
        /// <returns></returns>
        [HttpPut("generatemissingbookcovers")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType((int)HttpStatusCode.Forbidden, Type = typeof(string))]
        public async Task<IActionResult> GenerateMissingBookCoversAsync()
        {
            var res = await _divanService.GenerateMissingBookCoversAsync();
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// list of books
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("books")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanCatViewModel[]))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetBooksAsync()
        {
            var books = await _divanService.GetBooksAsync();
            if (!string.IsNullOrEmpty(books.ExceptionString))
            {
                return BadRequest(books.ExceptionString);
            }
            return Ok(books.Result);
        }

        /// <summary>
        /// batch rename cat poems
        /// </summary>
        /// <param name="id"></param>
        /// <param name="model"></param>
        /// <returns></returns>

        [HttpPut]
        [Route("cat/recaptionpoems/{id}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPageCompleteViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> BatchRenameCatPoemTitles(int id, [FromBody] DivanBatchNamingModel model)
        {
            Guid userId =
               new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

            RServiceResult<string[]> res =
                await _divanService.BatchRenameCatPoemTitles(id, model, userId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// batch resulg category poems
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPut]
        [Route("cat/reslugpoems/{id}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> BatchReSlugCatPoems(int id)
        {

            RServiceResult<bool> res =
                await _divanService.BatchReSlugCatPoems(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }


        [HttpPut]
        [Route("cat/startassigningrhymes/{id}/{retag}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(bool))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult FindCategoryPoemsRhymes(int id, bool retag)
        {

            RServiceResult<bool> res =
                _divanService.FindCategoryPoemsRhymes(id, retag);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// set category poems language tag
        /// </summary>
        /// <param name="id"></param>
        /// <param name="language"></param>
        /// <returns></returns>

        [HttpPut]
        [Route("cat/language/{id}/{language}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> SetCategoryLanguageTagAsync(int id, string language)
        {

            var res =
                await _divanService.SetCategoryLanguageTagAsync(id, language);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// set category poems language tag
        /// </summary>
        /// <param name="id"></param>
        /// <param name="poemformat"></param>
        /// <returns></returns>

        [HttpPut]
        [Route("cat/poemformat/{id}/{poemformat}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> SetCategoryPoemFormatAsync(int id, DivanPoemFormat? poemformat)
        {

            var res =
                await _divanService.SetCategoryPoemFormatAsync(id, poemformat);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// fill missing poem formats
        /// </summary>
        /// <returns></returns>
        [HttpPut("poemformats/fillmissing")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult StartFillingSectionsPoemFormats()
        {
            var res = _divanService.StartFillingSectionsPoemFormats();

            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);

            return Ok();
        }

        /// <summary>
        /// start assigning poem rhythms
        /// </summary>
        /// <param name="id"></param>
        /// <param name="retag"></param>
        /// <param name="rhythm"></param>
        /// <returns></returns>
        [HttpPut]
        [Route("cat/startassigningrhythms/{id}/{retag}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(bool))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult FindCategoryPoemsRhythms(int id, bool retag, string rhythm = "")
        {

            RServiceResult<bool> res =
                _divanService.FindCategoryPoemsRhythms(id, retag, rhythm);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// delete a category
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>

        [HttpDelete]
        [Route("cat/{id}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPageCompleteViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> DeleteCategoryAsync(int id)
        {
            var res = await _divanService.DeleteCategoryAsync(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// Start Finding Missing Rhythms
        /// </summary>
        /// <param name="onlyPoemsWithRhymes"></param>
        /// <param name="poemsNum"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("startfindingmissingrhythms")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(bool))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> StartFindingMissingRhythms(bool onlyPoemsWithRhymes = true, int poemsNum = 1000)
        {
            string systemEmail = $"{Configuration.GetSection("Divan")["SystemEmail"]}";
            var systemUserId = (Guid)(await _appUserService.FindUserByEmail(systemEmail)).Result.Id;
            string deletedUserEmail = $"{Configuration.GetSection("Divan")["DeleteUserEmail"]}";
            var deletedUserId = (Guid)(await _appUserService.FindUserByEmail(deletedUserEmail)).Result.Id;
            RServiceResult<bool> res =
                _divanService.StartFindingMissingRhythms(systemUserId, deletedUserId, onlyPoemsWithRhymes, poemsNum);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// generate category toc
        /// </summary>
        /// <param name="id"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("cat/toc/{id}/{options}")]
        [Produces("text/plain")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GenerateTableOfContents(int id, DivanTOC options = DivanTOC.Analyse)
        {
            var res = await _divanService.GenerateTableOfContents(new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value), id, options);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// directly insert generated TOC
        /// </summary>
        /// <param name="id"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        [HttpPut]
        [Route("cat/toc/{id}/{options}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> DirectInsertGeneratedTableOfContents(int id, DivanTOC options = DivanTOC.Analyse)
        {
            Guid userId =
              new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            var res = await _divanService.DirectInsertGeneratedTableOfContents(id, userId, options);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// start generating sub cats TOC
        /// </summary>
        /// <param name="id"></param>
        /// <param name="divanTOC"></param>
        /// <returns></returns>

        [HttpPut("cat/subcats/startgentoc/{id}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult StartGeneratingSubCatsTOC(int id, DivanTOC divanTOC = DivanTOC.TitlesAndFirstVerse)
        {
            Guid userId =
               new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            var res = _divanService.StartGeneratingSubCatsTOC(userId, id, divanTOC);

            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);

            return Ok();
        }

        /// <summary>
        /// regenerate TOCs
        /// </summary>
        /// <returns></returns>
        [HttpPut("cat/toc/regen")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult StartRegeneratingTOCs()
        {
            Guid userId =
               new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            var res = _divanService.StartRegeneratingTOCs(userId);

            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);

            return Ok();
        }

        /// <summary>
        /// page by url
        /// </summary>
        /// <param name="url"></param>
        /// <param name="catPoems"></param>
        /// <param name="commentSortOrder">TopRated, Oldest, or Newest. Only applies to poem pages.</param>
        /// <returns></returns>
        [HttpGet]
        [Route("page")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPageCompleteViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetPageByUrl(string url, bool catPoems = false, DivanCommentSortOrder commentSortOrder = DivanCommentSortOrder.TopRated)
        {
            RServiceResult<DivanPageCompleteViewModel> res =
                await _divanService.GetPageByUrl(url, catPoems, commentSortOrder);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            if (res.Result == null)
                return NotFound();
            return Ok(res.Result);
        }


        /// <summary>
        /// modify page
        /// </summary>
        /// <param name="id"></param>
        /// <param name="page"></param>
        /// <returns></returns>

        [HttpPut]
        [Route("page/{id}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPageCompleteViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> ModifyPage(int id, [FromBody] DivanModifyPageViewModel page)
        {
            Guid userId =
               new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

            RServiceResult<DivanPageCompleteViewModel> res =
                await _divanService.UpdatePageAsync(id, userId, page);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            if (res.Result == null)
                return NotFound();
            return Ok(res.Result);
        }

        /// <summary>
        /// modify poem => only these fields: NoIndex, RedirectFromFullUrl, MixedModeOrder
        /// </summary>
        /// <param name="id"></param>
        /// <param name="page"></param>
        /// <returns></returns>
        [HttpPut]
        [Route("poem/adminedit/{id}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPageCompleteViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> AdminEditPoem(int id, [FromBody] DivanModifyPageViewModel page)
        {
            Guid userId =
               new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

            RServiceResult<DivanPageCompleteViewModel> res =
                await _divanService.UpdatePoemAsync(id, userId, page);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            if (res.Result == null)
                return NotFound();
            return Ok(res.Result);
        }


        /// <summary>
        /// clean cache by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete]
        [Route("page/cache/{id}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPageCompleteViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> CacheCleanForPageById(int id)
        {
            await _divanService.CacheCleanForPageById(id);
            return Ok();
        }


        /// <summary>
        /// delete page
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete]
        [Route("page/{id}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPageCompleteViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> DeletePageAsync(int id)
        {
            var res = await _divanService.DeletePageAsync(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }


        /// <summary>
        /// older versions of a page (modifications history except for current version)
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("page/oldversions/{id}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPageSnapshotSummaryViewModel[]))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetOlderVersionsOfPage(int id)
        {
            RServiceResult<DivanPageSnapshotSummaryViewModel[]> res =
                await _divanService.GetOlderVersionsOfPage(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// get old version of page
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("oldversion/{id}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanModifyPageViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetOldVersionOfPage(int id)
        {
            RServiceResult<DivanModifyPageViewModel> res =
                await _divanService.GetOldVersionOfPage(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// page url by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("pageurl")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetPageUrlById(int id)
        {
            RServiceResult<string> res =
                await _divanService.GetPageUrlById(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            if (string.IsNullOrEmpty(res.Result))
                return NotFound();
            return Ok(res.Result);
        }

        /// <summary>
        /// get redirect url for a url
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("redirecturl")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetRedirectAddressForPageUrl(string url)
        {
            RServiceResult<string> res =
                await _divanService.GetRedirectAddressForPageUrl(url);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            if (string.IsNullOrEmpty(res.Result))
                return NotFound();
            return Ok(res.Result);
        }

        /// <summary>
        /// get poem by id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="catInfo"></param>
        /// <param name="catPoems"></param>
        /// <param name="rhymes"></param>
        /// <param name="recitations"></param>
        /// <param name="images"></param>
        /// <param name="songs"></param>
        /// <param name="comments">not implemented yet</param>
        /// <param name="verseDetails"></param>
        /// <param name="navigation">next/previous</param>
        /// <param name="relatedpoems"></param>
        /// <param name="commentSortOrder"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("poem/{id}")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPoemCompleteViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetPoemById(int id, bool catInfo = true, bool catPoems = false, bool rhymes = true, bool recitations = true, bool images = true, bool songs = true, bool comments = true, bool verseDetails = true, bool navigation = true, bool relatedpoems = true, DivanCommentSortOrder commentSortOrder = DivanCommentSortOrder.TopRated)
        {
            RServiceResult<DivanPoemCompleteViewModel> res =
                await _divanService.GetPoemById(id, catInfo, catPoems, rhymes, recitations, images, songs, comments, verseDetails, navigation, relatedpoems, true, commentSortOrder );
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            if (res.Result == null)
                return NotFound();
            return Ok(res.Result);
        }

        /// <summary>
        /// get poem verses by id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="coupletIndex"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("poem/{id}/verses")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanVerseViewModel[]))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetPoemVerses(int id, int coupletIndex = -1)
        {
            RServiceResult<DivanVerseViewModel[]> res =
                await _divanService.GetPoemVersesAsync(id, coupletIndex);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            if (res.Result == null)
                return NotFound();
            return Ok(res.Result);
        }

        /// <summary>
        /// get poem by url
        /// </summary>
        /// <param name="url"></param>
        /// <param name="catInfo"></param>
        /// <param name="catPoems"></param>
        /// <param name="rhymes">not implemented yet</param>
        /// <param name="recitations"></param>
        /// <param name="images"></param>
        /// <param name="songs">not implemented yet</param>
        /// <param name="comments">not implemented yet</param>
        /// <param name="verseDetails"></param>
        /// <param name="navigation">next/previous</param>
        /// <returns></returns>
        [HttpGet]
        [Route("poem")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPoemCompleteViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetPoemByUrl(string url, bool catInfo = true, bool catPoems = false, bool rhymes = true, bool recitations = true, bool images = true, bool songs = true, bool comments = true, bool verseDetails = true, bool navigation = true)
        {
            RServiceResult<DivanPoemCompleteViewModel> res =
                await _divanService.GetPoemByUrl(url, catInfo, catPoems, rhymes, recitations, images, songs, comments, verseDetails, navigation);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            if (res.Result == null)
                return NotFound();
            return Ok(res.Result);
        }

        /// <summary>
        ///  get poem recitations  (PlainText/HtmlText are intentionally empty)
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("poem/{id}/recitations")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(PublicRecitationViewModel[]))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetPoemRecitations(int id)
        {
            RServiceResult<PublicRecitationViewModel[]> res =
                await _divanService.GetPoemRecitations(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// get user upvoted recitations of a poem
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("poem/{id}/recitations/upvotes")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(int[]))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetUserPoemRecitationsUpVotes(int id)
        {
            var loggedOnUserId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            RServiceResult<int[]> res =
                await _divanService.GetUserPoemRecitationsUpVotes(id, loggedOnUserId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        ///  get poem images  (PlainText/HtmlText are intentionally empty)
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("poem/{id}/images")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(PoemRelatedImage[]))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetPoemImages(int id)
        {
            RServiceResult<PoemRelatedImage[]> res =
                await _divanService.GetPoemImages(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// Get Poem Comments
        /// </summary>
        /// <param name="id"></param>
        /// <param name="coupletIndex"></param>
        /// <param name="sortOrder">TopRated, Oldest, or Newest</param>
        /// <returns></returns>

        [HttpGet]
        [Route("poem/{id}/comments")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanCommentSummaryViewModel[]))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetPoemComments(int id, int? coupletIndex, DivanCommentSortOrder sortOrder = DivanCommentSortOrder.TopRated)
        {
            RServiceResult<DivanCommentSummaryViewModel[]> res =
                await _divanService.GetPoemComments(id, Guid.Empty, coupletIndex, sortOrder);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// Rate a comment (like / dislike / clear rating)
        /// </summary>
        /// <param name="id">comment id</param>
        /// <param name="value">+1: like, -1: dislike, 0: remove previous rating</param>
        /// <returns></returns>
        [HttpPost]
        [Route("poem/comment/{id}/rate")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanCommentRatingResultViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> RateComment(int id, short value)
        {
            Guid userId = new Guid(User.Claims.First(c => c.Type == "UserId").Value);
            RServiceResult<DivanCommentRatingResultViewModel> res =
                await _divanService.RateCommentAsync(userId, id, value);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// get the logged on user's own rating values for a poem's comments
        /// (meant to be merged client-side into an already fetched, anonymously cacheable comment list)
        /// </summary>
        /// <param name="id">poem id</param>
        /// <returns></returns>
        [HttpGet]
        [Route("poem/{id}/comments/myratings")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanCommentUserRatingViewModel[]))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetUserCommentRatings(int id)
        {
            var loggedOnUserId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            RServiceResult<DivanCommentUserRatingViewModel[]> res =
                await _divanService.GetUserCommentRatings(id, loggedOnUserId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// Get Section Related ones
        /// </summary>
        /// <param name="poemId"></param>
        /// <param name="sectionIndex"></param>
        /// <param name="skip"></param>
        /// <param name="itemsCount">zero or less than it means all</param>
        /// <returns></returns>
        [HttpGet]
        [Route("section/{poemId}/{sectionIndex}/related")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanCachedRelatedSection[]))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetRelatedSections(int poemId, int sectionIndex, int skip = 0, int itemsCount = 0)
        {
            RServiceResult<DivanCachedRelatedSection[]> res =
                await _divanService.GetRelatedSections(poemId, sectionIndex, skip, itemsCount);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// find poem section rhyming letters
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>

        [HttpGet]
        [Route("section/analyserhyme/{id}")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanRhymeAnalysisResult))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> FindSectionRhyme(int id)
        {
            var res = await _divanService.FindSectionRhyme(id);

            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);

            return Ok(res.Result);
        }

        /// <summary>
        /// delete a poem section (section should not be linked to a poem verse of types other than paragraphs or comments)
        /// </summary>
        /// <param name="poemId"></param>
        /// <param name="sectionIndex"></param>
        /// <param name="convertVerses"></param>
        /// <returns></returns>
        [HttpDelete]
        [Route("section/{poemId}/{sectionIndex}/{convertVerses}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> DeletePoemSectionByPoemIdAndIndexAsync(int poemId, int sectionIndex, bool convertVerses)
        {
            var res = await _divanService.DeletePoemSectionByPoemIdAndIndexAsync(poemId, sectionIndex, convertVerses);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            if (res.Result == false)
                return NotFound();
            return Ok();
        }

        /// <summary>
        /// delete a poem
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete]
        [Route("poem/{id}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPageCompleteViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> DeletePoemAsync(int id)
        {
            var res = await _divanService.DeletePoemAsync(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// send poem corrections
        /// </summary>
        /// <param name="correction"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("poem/correction")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPoemCorrectionViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> SuggestPoemCorrection([FromBody] DivanPoemCorrectionViewModel correction)
        {
            if (ReadOnlyMode)
                return BadRequest("سایت به دلایل فنی مثل انتقال سرور موقتاً در حالت فقط خواندنی قرار دارد. لطفاً ساعاتی دیگر مجدداً تلاش کنید.");

            correction.UserId =
               new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

            RServiceResult<DivanPoemCorrectionViewModel> res =
                await _divanService.SuggestPoemCorrection(correction);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            if (res.Result == null)
                return NotFound();
            return Ok(res.Result);
        }

        /// <summary>
        /// delete unreviewed user corrections for a poem
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete]
        [Route("poem/correction/{id}")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPoemCorrectionViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> DeletePoemCorrections(int id)
        {
            if (ReadOnlyMode)
                return BadRequest("سایت به دلایل فنی مثل انتقال سرور موقتاً در حالت فقط خواندنی قرار دارد. لطفاً ساعاتی دیگر مجدداً تلاش کنید.");

            var userId =
               new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

            var res =
                await _divanService.DeletePoemCorrections(userId, id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// returns last unreviewed correction from the user for a poem
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("poem/correction/last/{id}")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPoemCorrectionViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetLastUnreviewedUserCorrectionForPoem(int id)
        {
            Guid userId =
               new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

            RServiceResult<DivanPoemCorrectionViewModel> res =
                await _divanService.GetLastUnreviewedUserCorrectionForPoem(userId, id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);//might be null
        }

        /// <summary>
        /// get list of user suggested corrections
        /// </summary>
        /// <param name="paging"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("corrections/mine")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<DivanPoemCorrectionViewModel>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetUserCorrections([FromQuery] PagingParameterModel paging)
        {
            Guid userId =
               new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

            var res =
                await _divanService.GetUserCorrections(userId, paging);

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(res.Result.PagingMeta));

            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result.Items);
        }

        /// <summary>
        /// get list of all suggested corrections
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="userId">userId</param>
        /// <returns></returns>
        [HttpGet]
        [Route("corrections/all")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<DivanPoemCorrectionViewModel>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetAllCorrections([FromQuery] PagingParameterModel paging, Guid? userId = null)
        {

            var res =
                await _divanService.GetUserCorrections(userId ?? Guid.Empty, paging);

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(res.Result.PagingMeta));

            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result.Items);
        }

        /// <summary>
        /// effective corrections for poem
        /// </summary>
        /// <param name="id"></param>
        /// <param name="paging"></param>
        /// <returns></returns>

        [HttpGet]
        [Route("poem/{id}/corrections/effective")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<DivanPoemCorrectionViewModel>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetPoemEffectiveCorrections(int id, [FromQuery] PagingParameterModel paging)
        {
            var res =
                await _divanService.GetPoemEffectiveCorrections(id, paging);

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(res.Result.PagingMeta));

            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result.Items);
        }

        /// <summary>
        /// get correction by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("correction/{id}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPoemCorrectionViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetCorrectionById(int id)
        {
            Guid userId =
               new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

            RServiceResult<DivanPoemCorrectionViewModel> res =
                await _divanService.GetCorrectionById(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);//might be null
        }

        /// <summary>
        /// get next unreviewed correction
        /// </summary>
        /// <param name="skip"></param>
        /// <param name="onlyUserCorrections"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("poem/correction/next")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPoemCorrectionViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetNextUnreviewedCorrection(int skip = 0, bool onlyUserCorrections = false)
        {
            RServiceResult<DivanPoemCorrectionViewModel> res =
                await _divanService.GetNextUnreviewedCorrection(skip, onlyUserCorrections);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);

            var resCount = await _divanService.GetUnreviewedCorrectionCount(onlyUserCorrections);
            if (!string.IsNullOrEmpty(resCount.ExceptionString))
                return BadRequest(resCount.ExceptionString);

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers",
                JsonConvert.SerializeObject(
                    new PaginationMetadata()
                    {
                        totalCount = resCount.Result,
                        pageSize = -1,
                        currentPage = -1,
                        hasNextPage = false,
                        hasPreviousPage = false,
                        totalPages = -1
                    })
                );

            return Ok(res.Result);//might be null
        }

        /// <summary>
        /// moderate poem correction
        /// </summary>
        /// <param name="correction"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("correction/moderate")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPoemCorrectionViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> ModeratePoemCorrection([FromBody] DivanPoemCorrectionViewModel correction)
        {
            if (ReadOnlyMode)
                return BadRequest("سایت به دلایل فنی مثل انتقال سرور موقتاً در حالت فقط خواندنی قرار دارد. لطفاً ساعاتی دیگر مجدداً تلاش کنید.");

            Guid userId =
               new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

            RServiceResult<DivanPoemCorrectionViewModel> res =
                await _divanService.ModeratePoemCorrection(userId, correction);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            if (res.Result == null)
                return NotFound();
            return Ok(res.Result);
        }

        /// <summary>
        /// break a poem from a verse forward
        /// </summary>
        /// <param name="verse"></param>
        /// <returns>id of new poem</returns>
        [HttpPost]
        [Route("poem/break")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(int))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> BreakPoemAsync([FromBody] PoemVerseOrder verse)
        {
            if (ReadOnlyMode)
                return BadRequest("سایت به دلایل فنی مثل انتقال سرور موقتاً در حالت فقط خواندنی قرار دارد. لطفاً ساعاتی دیگر مجدداً تلاش کنید.");

            var userId =
               new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

            RServiceResult<int> res =
                await _divanService.BreakPoemAsync(verse.PoemId, verse.VOrder, userId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// suggest song for poem
        /// </summary>
        /// <param name="song"></param>
        /// <returns></returns>

        /// <summary>
        /// user suggested songs
        /// </summary>
        /// <param name="paging"></param>
        /// <returns></returns>

        /// <summary>
        ///  get a random poem from hafez
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("hafez/faal")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPoemCompleteViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> Faal()
        {
            RServiceResult<DivanPoemCompleteViewModel> res =
                await _divanService.Faal(2, true);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// get a random poem from a poet (c.ganjoor.net replacement), 0 means random poet, 
        /// حافظ (2)، خیام (3)، ابوسعید ابوالخیر (26)، صائب (22)، سعدی (7)، باباطاهر (28)، مولوی (5)، اوحدی (19)، خواجو (20)، شهریار (35)، عراقی (21)، فروغی بسطامی (32)، سلمان ساوجی (40)، محتشم کاشانی (29)، امیرخسرو دهلوی (34)، سیف فرغانی (31)، عبید زاکانی (33)، هاتف اصفهانی (25) یا رهی معیری (41)
        /// </summary>
        /// <param name="poetId"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("poem/random")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPoemCompleteViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetARandomPoem(int poetId)
        {
            RServiceResult<DivanPoemCompleteViewModel> res =
                await _divanService.Faal(poetId, false);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// get a single comment information (only published comments, replies are not included)
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("comments/{id}")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanCommentFullViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetCommentByIdAsync(int id)
        {
            var res = await _divanService.GetCommentByIdAsync(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }
            if(res.Result == null)
            {
                return NotFound();
            }
            return Ok(res.Result);
        }

        /// <summary>
        /// get recent comments
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="filterUserId"></param>
        /// <param name="term"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("comments")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<DivanCommentFullViewModel>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> GetRecentComments([FromQuery] PagingParameterModel paging, Guid? filterUserId = null, string term = null)
        {
            var comments = await _divanService.GetRecentComments(paging, filterUserId == null ? Guid.Empty : (Guid)filterUserId, true, false, term);
            if (!string.IsNullOrEmpty(comments.ExceptionString))
            {
                return BadRequest(comments.ExceptionString);
            }

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(comments.Result.PagingMeta));

            return Ok(comments.Result.Items);
        }

        /// <summary>
        /// get logged on users recent comments
        /// </summary>
        /// <param name="paging"></param>
        /// <returns></returns>

        [HttpGet]
        [Route("comments/mine")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<DivanCommentFullViewModel>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> GetMyRecentComments([FromQuery] PagingParameterModel paging)
        {
            Guid userId =
                 new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            Guid sessionId =
                new Guid(User.Claims.FirstOrDefault(c => c.Type == "SessionId").Value);
            RServiceResult<bool> sessionCheckResult = await _appUserService.SessionExists(userId, sessionId);
            if (!string.IsNullOrEmpty(sessionCheckResult.ExceptionString))
            {
                return StatusCode((int)HttpStatusCode.Forbidden);
            }

            var comments = await _divanService.GetRecentComments(paging, userId, false);
            if (!string.IsNullOrEmpty(comments.ExceptionString))
            {
                return BadRequest(comments.ExceptionString);
            }

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(comments.Result.PagingMeta));

            return Ok(comments.Result.Items);
        }

        /// <summary>
        /// get awaiting comments
        /// </summary>
        /// <param name="paging"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("comments/awaiting")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ModerateOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<DivanCommentFullViewModel>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> GetAwaitingComments([FromQuery] PagingParameterModel paging)
        {

            var comments = await _divanService.GetRecentComments(paging, Guid.Empty, false, true);
            if (!string.IsNullOrEmpty(comments.ExceptionString))
            {
                return BadRequest(comments.ExceptionString);
            }

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(comments.Result.PagingMeta));

            return Ok(comments.Result.Items);
        }

        /// <summary>
        /// delete awaiting comment
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete]
        [Route("comment/awaiting/delete")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ModerateOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        [ProducesResponseType((int)HttpStatusCode.Forbidden)]
        public async Task<IActionResult> DeleteAnybodyComment(int id)
        {
            if (ReadOnlyMode)
                return BadRequest("سایت به دلایل فنی مثل انتقال سرور موقتاً در حالت فقط خواندنی قرار دارد. لطفاً ساعاتی دیگر مجدداً تلاش کنید.");
            var res =
                await _divanService.DeleteAnybodyComment(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            if (!res.Result)
                return NotFound();
            return Ok();
        }

        /// <summary>
        /// publish awaiting comment
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPut]
        [Route("comment/awaiting/publish")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ModerateOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        [ProducesResponseType((int)HttpStatusCode.Forbidden)]
        public async Task<IActionResult> PublishAwaitingComment([FromBody] int id)
        {
            if (ReadOnlyMode)
                return BadRequest("سایت به دلایل فنی مثل انتقال سرور موقتاً در حالت فقط خواندنی قرار دارد. لطفاً ساعاتی دیگر مجدداً تلاش کنید.");
            var res =
                await _divanService.PublishAwaitingComment(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            if (!res.Result)
                return NotFound();
            return Ok();
        }



        /// <summary>
        /// post new comment
        /// </summary>
        /// <param name="comment"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("comment")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanCommentSummaryViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.Forbidden)]
        public async Task<IActionResult> NewComment([FromBody] DivanCommentPostViewModel comment)
        {
            if (ReadOnlyMode)
                return BadRequest("سایت به دلایل فنی مثل انتقال سرور موقتاً در حالت فقط خواندنی قرار دارد. لطفاً ساعاتی دیگر مجدداً تلاش کنید.");

            string clientIPAddress = _httpContextAccessor.HttpContext.Connection.RemoteIpAddress.ToString();

            Guid userId =
                 new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            Guid sessionId =
                new Guid(User.Claims.FirstOrDefault(c => c.Type == "SessionId").Value);
            RServiceResult<bool> sessionCheckResult = await _appUserService.SessionExists(userId, sessionId);
            if (!string.IsNullOrEmpty(sessionCheckResult.ExceptionString))
            {
                return StatusCode((int)HttpStatusCode.Forbidden);
            }

            var res =
                await _divanService.NewComment(userId, clientIPAddress, comment.PoemId, comment.HtmlComment, comment.InReplyToId, comment.CoupletIndex);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// edit user's own comment
        /// </summary>
        /// <param name="id"></param>
        /// <param name="comment"></param>
        /// <returns></returns>
        [HttpPut]
        [Route("comment/{id}")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        [ProducesResponseType((int)HttpStatusCode.Forbidden)]
        public async Task<IActionResult> EditMyComment(int id, [FromBody] string comment)
        {
            if (ReadOnlyMode)
                return BadRequest("سایت به دلایل فنی مثل انتقال سرور موقتاً در حالت فقط خواندنی قرار دارد. لطفاً ساعاتی دیگر مجدداً تلاش کنید.");
            Guid userId =
                 new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            Guid sessionId =
                new Guid(User.Claims.FirstOrDefault(c => c.Type == "SessionId").Value);
            RServiceResult<bool> sessionCheckResult = await _appUserService.SessionExists(userId, sessionId);
            if (!string.IsNullOrEmpty(sessionCheckResult.ExceptionString))
            {
                return StatusCode((int)HttpStatusCode.Forbidden);
            }

            var res =
                await _divanService.EditMyComment(userId, id, comment);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            if (!res.Result)
                return NotFound();
            return Ok();
        }

        /// <summary>
        /// link or unlink user's own comment to a coupletIndex
        /// </summary>
        /// <param name="id"></param>
        /// <param name="coupletIndex"></param>
        /// <returns>couplet summary for linked comment</returns>
        [HttpPut]
        [Route("comment/{id}/editlink/{coupletIndex}")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        [ProducesResponseType((int)HttpStatusCode.Forbidden)]
        public async Task<IActionResult> LinkUnLinkMyComment(int id, int? coupletIndex)
        {
            if (ReadOnlyMode)
                return BadRequest("سایت به دلایل فنی مثل انتقال سرور موقتاً در حالت فقط خواندنی قرار دارد. لطفاً ساعاتی دیگر مجدداً تلاش کنید.");
            Guid userId =
                 new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            Guid sessionId =
                new Guid(User.Claims.FirstOrDefault(c => c.Type == "SessionId").Value);
            RServiceResult<bool> sessionCheckResult = await _appUserService.SessionExists(userId, sessionId);
            if (!string.IsNullOrEmpty(sessionCheckResult.ExceptionString))
            {
                return StatusCode((int)HttpStatusCode.Forbidden);
            }

            var res =
                await _divanService.LinkUnLinkMyComment(userId, id, coupletIndex);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            if (res.Result == null)
                return NotFound();
            return Ok(res.Result);
        }

        /// <summary>
        /// delete user's own comment
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete]
        [Route("comment")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        [ProducesResponseType((int)HttpStatusCode.Forbidden)]
        public async Task<IActionResult> DeleteMyComment(int id)
        {
            if (ReadOnlyMode)
                return BadRequest("سایت به دلایل فنی مثل انتقال سرور موقتاً در حالت فقط خواندنی قرار دارد. لطفاً ساعاتی دیگر مجدداً تلاش کنید.");
            Guid userId =
                 new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            Guid sessionId =
                new Guid(User.Claims.FirstOrDefault(c => c.Type == "SessionId").Value);
            RServiceResult<bool> sessionCheckResult = await _appUserService.SessionExists(userId, sessionId);
            if (!string.IsNullOrEmpty(sessionCheckResult.ExceptionString))
            {
                return StatusCode((int)HttpStatusCode.Forbidden);
            }

            var res =
                await _divanService.DeleteMyComment(userId, id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            if (!res.Result)
                return NotFound();
            return Ok();
        }

        /// <summary>
        /// report a comment
        /// </summary>
        /// <param name="report"></param>
        /// <returns>id of reported record</returns>
        [HttpPost]
        [Route("comment/report")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(int))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.Forbidden)]
        public async Task<IActionResult> ReportComment([FromBody] DivanPostReportCommentViewModel report)
        {
            if (ReadOnlyMode)
                return BadRequest("سایت به دلایل فنی مثل انتقال سرور موقتاً در حالت فقط خواندنی قرار دارد. لطفاً ساعاتی دیگر مجدداً تلاش کنید.");

            Guid userId =
                 new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            Guid sessionId =
                new Guid(User.Claims.FirstOrDefault(c => c.Type == "SessionId").Value);
            RServiceResult<bool> sessionCheckResult = await _appUserService.SessionExists(userId, sessionId);
            if (!string.IsNullOrEmpty(sessionCheckResult.ExceptionString))
            {
                return StatusCode((int)HttpStatusCode.Forbidden);
            }

            RServiceResult<int> res = await _divanService.ReportComment(userId, report);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }
            return Ok(res.Result);
        }

        /// <summary>
        /// delete a report (without deleting corresponding comment)
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete]
        [Route("comment/report/{id}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ModerateOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(int))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.Forbidden)]
        public async Task<IActionResult> DeleteReport(int id)
        {
            if (ReadOnlyMode)
                return BadRequest("سایت به دلایل فنی مثل انتقال سرور موقتاً در حالت فقط خواندنی قرار دارد. لطفاً ساعاتی دیگر مجدداً تلاش کنید.");
            RServiceResult<bool> res = await _divanService.DeleteReport(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }
            if (!res.Result)
            {
                return NotFound();
            }
            return Ok();
        }


        /// <summary>
        /// get list of reported comments
        /// </summary>
        /// <param name="paging"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("comments/reported")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ModerateOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<DivanCommentAbuseReportViewModel>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetReportedComments([FromQuery] PagingParameterModel paging)
        {
            var comments = await _divanService.GetReportedComments(paging);
            if (!string.IsNullOrEmpty(comments.ExceptionString))
            {
                return BadRequest(comments.ExceptionString);
            }

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(comments.Result.PagingMeta));

            return Ok(comments.Result.Items);
        }

        /// <summary>
        /// delete reported other users comment
        /// </summary>
        /// <param name="reportid"></param>
        /// <returns></returns>
        [HttpDelete]
        [Route("comment/reported/moderate/{reportid}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ModerateOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        [ProducesResponseType((int)HttpStatusCode.Forbidden)]
        public async Task<IActionResult> DeleteModerateComment(int reportid)
        {
            if (ReadOnlyMode)
                return BadRequest("سایت به دلایل فنی مثل انتقال سرور موقتاً در حالت فقط خواندنی قرار دارد. لطفاً ساعاتی دیگر مجدداً تلاش کنید.");
            var res =
                await _divanService.DeleteModerateComment(reportid);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            if (!res.Result)
                return NotFound();
            return Ok();
        }

        /// <summary>
        /// imports data from divan SQLite database (form file)
        /// </summary>
        /// <param name="poetId"></param>
        /// <returns></returns>

        [HttpPost]
        [Route("sqlite/import/{poetId}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ImportOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(bool))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> ImportLocalSQLiteDb(int poetId)
        {
            IFormFile file = Request.Form.Files[0];

            RServiceResult<bool> res =
                await _divanService.ImportFromSqlite(poetId, file);
            if (res.Result)
                return Ok();
            return BadRequest(res.ExceptionString);
        }

        /// <summary>
        /// import category data from divan SQLite database (form file)
        /// </summary>
        /// <param name="catId"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("sqlite/import/cat/{catId}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ImportOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(bool))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> ImportCategoryFromSqlite(int catId)
        {
            IFormFile file = Request.Form.Files[0];

            RServiceResult<bool> res =
                await _divanService.ImportCategoryFromSqlite(catId, file);
            if (res.Result)
                return Ok();
            return BadRequest(res.ExceptionString);
        }

        /// <summary>
        /// Apply corrections from sqlite
        /// </summary>
        /// <param name="poetId"></param>
        /// <param name="note"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("sqlite/update/{poetId}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ImportOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(bool))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> ApplyCorrectionsFromSqlite(int poetId, string note)
        {
            IFormFile file = Request.Form.Files[0];

            RServiceResult<bool> res =
                await _divanService.ApplyCorrectionsFromSqlite(poetId, file, note);
            if (res.Result)
                return Ok();
            return BadRequest(res.ExceptionString);
        }

        /// <summary>
        /// export a poet to sqlite database
        /// </summary>
        /// <param name="poetId"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("sqlite/export/{poetId}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ImportOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(FileStreamResult))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> ExportToSqlite(int poetId)
        {
            RServiceResult<string> res =
                await _divanService.ExportToSqlite(poetId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return new FileStreamResult(new FileStream(res.Result, FileMode.Open, FileAccess.Read), "application/octet-stream");
        }

        /// <summary>
        /// start exporting all poets
        /// </summary>
        /// <returns></returns>
        [HttpPost("sqlite/batchexport")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ReviewSongs)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        public IActionResult StartBatchGenerateGDBFiles()
        {
            try
            {

                var res = _divanService.StartBatchGenerateGDBFiles();
                if (!string.IsNullOrEmpty(res.ExceptionString))
                    return BadRequest(res.ExceptionString);
                return Ok();
            }
            catch (Exception exp)
            {
                return BadRequest(exp.ToString());
            }
        }

        /// <summary>
        /// start exporting all published Divan data to the public git-tracked JSON data set
        /// </summary>
        /// <returns></returns>
        [HttpPost("publicdata/batchexport")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ImportOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        public IActionResult StartBatchExportPublicGitData()
        {
            try
            {
                var res = _divanService.StartBatchExportPublicGitData();
                if (!string.IsNullOrEmpty(res.ExceptionString))
                    return BadRequest(res.ExceptionString);
                return Ok();
            }
            catch (Exception exp)
            {
                return BadRequest(exp.ToString());
            }
        }

        /// <summary>
        /// (re)build local Divan content from a public data export tree (local folder or HTTP) —
        /// intended for local development use, not for production servers
        /// </summary>
        /// <returns></returns>
        [HttpPost("publicdata/import")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ImportOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        public IActionResult StartImportFromPublicDataRepo([FromBody] PublicDataImportRequestDto request)
        {
            try
            {
                Guid userId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
                var res = _divanService.StartImportFromPublicDataRepo(request.UseHttp, request.Location, request.PoetId, userId);
                if (!string.IsNullOrEmpty(res.ExceptionString))
                    return BadRequest(res.ExceptionString);
                return Ok();
            }
            catch (Exception exp)
            {
                return BadRequest(exp.ToString());
            }
        }

        /// <summary>
        /// Get user public profile
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet("user/profile/{id}")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanUserPublicProfile))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetUserPublicProfile(Guid id)
        {
            RServiceResult<PublicRAppUser> userInfo = await _appUserService.GetUserInformation(id);
            if (userInfo.Result == null)
            {
                if (string.IsNullOrEmpty(userInfo.ExceptionString))
                    return NotFound();
                return BadRequest(userInfo.ExceptionString);
            }
            return Ok
                (
                new DivanUserPublicProfile()
                {
                    Id = id,
                    NickName = userInfo.Result.NickName,
                    Bio = userInfo.Result.Bio,
                    Website = userInfo.Result.Website,
                    RImageId = userInfo.Result.RImageId
                }
                );
        }

        /// <summary>
        /// Get Similar Poems accroding to prosody and rhyme informations
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="metre">cannot be empty</param>
        /// <param name="rhyme">can be empty</param>
        /// <param name="poetId">send 0 for all</param>
        /// <param name="catId">send 0 for all</param>
        /// <param name="language"></param>
        /// <param name="format"></param>
        /// <param name="term"></param>
        /// <param name="coupletCountsFrom"></param>
        /// <param name="coupletCountsTo"></param>
        /// <param name="e"></param>
        /// <returns>return value is not complete or valid for some parts, you should use only the valid parts!</returns>

        [HttpGet]
        [Route("poems/similar")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<DivanPoemCompleteViewModel>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> GetSimilarPoemsAsync([FromQuery] PagingParameterModel paging, string metre, string rhyme, int poetId = 0, int catId = 0, string language = "ur-PK", DivanPoemFormat format = DivanPoemFormat.Unknown, string term = null, int coupletCountsFrom = 0, int coupletCountsTo = 0, int[] e = null)
        {
            var pagedResult = await _divanService.GetSimilarPoemsAsync(paging, metre, rhyme, poetId == 0 ? null : poetId, catId == 0 ? null : catId, language, format, term, coupletCountsFrom, coupletCountsTo, e == null ? [] : e);
            if (!string.IsNullOrEmpty(pagedResult.ExceptionString))
                return BadRequest(pagedResult.ExceptionString);

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(pagedResult.Result.PagingMeta));

            return Ok(pagedResult.Result.Items);
        }

        /// <summary>
        /// language tagged poem sections
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="language">fa-IR, ar, ...</param>
        /// <param name="poetId">0 means all poets</param>
        /// <returns></returns>

        [HttpGet]
        [Route("sections/tagged/language")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<DivanPoemCompleteViewModel>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetLanguageTaggedPoemSections([FromQuery] PagingParameterModel paging, string language, int poetId = 0)
        {
            var pagedResult = await _divanService.GetLanguageTaggedPoemSections(paging, language, poetId == 0 ? (int?)null : poetId);
            if (!string.IsNullOrEmpty(pagedResult.ExceptionString))
                return BadRequest(pagedResult.ExceptionString);

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(pagedResult.Result.PagingMeta));

            return Ok(pagedResult.Result.Items);
        }


        /// <summary>
        /// search
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="term"></param>
        /// <param name="poetId"></param>
        /// <param name="catId"></param>
        /// <param name="e"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("poems/search")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<DivanPoemCompleteViewModel>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> Search([FromQuery] PagingParameterModel paging, string term, int poetId = 0, int catId = 0, int[] e = null)
        {
            var pagedResult = await _divanService.Search(paging, term, poetId == 0 ? (int?)null : poetId, catId == 0 ? (int?)null : catId, e == null ?[] : e);
            if (!string.IsNullOrEmpty(pagedResult.ExceptionString))
                return BadRequest(pagedResult.ExceptionString);

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(pagedResult.Result.PagingMeta));

            return Ok(pagedResult.Result.Items);
        }

        /// <summary>
        /// returns divan metre list ordered by rhythm
        /// </summary>
        /// <param name="sortOnVerseCount"></param>
        /// <returns></returns>

        [HttpGet]
        [Route("rhythms")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<DivanMetre>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> GetDivanMetres(bool sortOnVerseCount = false)
        {
            var res = await _divanService.GetDivanMetres(sortOnVerseCount);

            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);

            return Ok(res.Result);
        }

        /// <summary>
        /// find poem rhyme
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("poem/analysisrhyme/{id}")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanRhymeAnalysisResult))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> FindPoemRhyme(int id)
        {
            var res = await _divanService.FindPoemMainSectionRhyme(id);

            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);

            return Ok(res.Result);
        }

        /// <summary>
        /// analysis poem to find its prosody information
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("poem/analysisrhythm/{id}")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> FindPoemRhythm(int id)
        {
            var res = await _divanService.FindPoemMainSectionRhythm(id);

            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);

            return Ok(res.Result);
        }



        /// <summary>
        /// examine site pages for broken links
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("healthcheck")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ImportOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(bool))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult HealthCheckContents()
        {
            RServiceResult<bool> res =
                 _divanService.HealthCheckContents();
            if (res.Result)
                return Ok();
            return BadRequest(res.ExceptionString);
        }

        /// <summary>
        /// examine comments for long links
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("fixlongurlsincomments")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ImportOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(bool))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult FindAndFixLongUrlsInComments()
        {
            RServiceResult<bool> res =
                 _divanService.FindAndFixLongUrlsInComments();
            if (res.Result)
                return Ok();
            return BadRequest(res.ExceptionString);
        }

        /// <summary>
        /// start filling poems couplet indices
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("fillpoemscoupletindices")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ImportOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(bool))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult StartFillingPoemsCoupletIndices()
        {
            RServiceResult<bool> res =
                 _divanService.StartFillingPoemsCoupletIndices();
            if (res.Result)
                return Ok();
            return BadRequest(res.ExceptionString);
        }

        /// <summary>
        /// refill couplet indices for poem
        /// </summary>
        /// <param name="poemId"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("refillcoupletindices/{poemId}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ImportOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(bool))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> RefillCoupletIndicesAsync(int poemId)
        {
            RServiceResult<bool> res =
                 await _divanService.RefillCoupletIndicesAsync(poemId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                return BadRequest(res.ExceptionString);
            }
            return Ok();
        }

        /// <summary>
        /// fill section couplet counts
        /// </summary>
        /// <returns></returns>

        [HttpPost]
        [Route("fillsectioncoupletcounts")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ImportOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(bool))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult StartFillingSectionCoupletCounts()
        {
            RServiceResult<bool> res =
                 _divanService.StartFillingSectionCoupletCounts();
            if (res.Result)
                return Ok();
            return BadRequest(res.ExceptionString);
        }

        /// <summary>
        /// regenerate poem full titles to fix an old bug
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("maintenance/regenfulltitles")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ImportOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(bool))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult RegeneratePoemsFullTitles()
        {
            RServiceResult<bool> res =
                 _divanService.RegeneratePoemsFullTitles();
            if (res.Result)
                return Ok();
            return BadRequest(res.ExceptionString);
        }

        /// <summary>
        /// start finding rhymes
        /// </summary>
        /// <returns></returns>
        [HttpPost]
        [Route("singlecouplets/startfindingrhymes")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ImportOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(bool))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult StartFindingRhymesForSingleCouplets()
        {
            RServiceResult<bool> res =
                 _divanService.StartFindingRhymesForSingleCouplets();
            if (res.Result)
                return Ok();
            return BadRequest(res.ExceptionString);
        }

        /// <summary>
        /// separate verses in poem.PlainText with  Environment.NewLine instead of SPACE
        /// </summary>
        /// <param name="catId">if it is 0 it is ignored</param>
        /// <returns></returns>
        [HttpPost("regenplaintext/{catId}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ImportOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(bool))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult RegerneratePoemsPlainText(int catId)
        {
            RServiceResult<bool> res =
                 _divanService.RegerneratePoemsPlainText(catId);
            if (res.Result)
                return Ok();
            return BadRequest(res.ExceptionString);
        }

        /// <summary>
        /// start building sitemap
        /// </summary>
        /// <returns></returns>

        [HttpPost("sitemap")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public IActionResult StartBuildingSitemap()
        {
            var res = _divanService.StartBuildingSitemap();

            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);

            return Ok();
        }


        /// <summary>
        /// regenerate stats page
        /// </summary>
        /// <returns></returns>
        [HttpPut("rebuild/stats")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        public IActionResult StartUpdatingStatsPage()
        {
            try
            {
                Guid userId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

                var res = _divanService.StartUpdatingStatsPage(userId);
                if (!string.IsNullOrEmpty(res.ExceptionString))
                    return BadRequest(res.ExceptionString);
                return Ok();
            }
            catch (Exception exp)
            {
                return BadRequest(exp.ToString());
            }
        }



        /// <summary>
        /// switch bookmark
        /// </summary>
        /// <param name="poemId"></param>
        /// <param name="coupletIndex">if you send a negative number it means you are trying to bookmark a comment</param>
        /// <returns></returns>
        [HttpPost]
        [Route("bookmark/switch/{poemId}/{coupletIndex}")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(bool))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> SwitchCoupletBookmark(int poemId, int coupletIndex)
        {
            if (ReadOnlyMode)
                return BadRequest("سایت به دلایل فنی مثل انتقال سرور موقتاً در حالت فقط خواندنی قرار دارد. لطفاً ساعاتی دیگر مجدداً تلاش کنید.");
            Guid loggedOnUserId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            RServiceResult<DivanUserBookmark> res = await _divanService.SwitchCoupletBookmark(loggedOnUserId, poemId, coupletIndex);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                if (res.ExceptionString == "verse not found")
                    return NotFound();
                return BadRequest(res.ExceptionString);
            }
            return Ok(res.Result != null);
        }
        /// <summary>
        /// switch bookmark and return bookmark id ('0' for switching off a bookmark)
        /// </summary>
        /// <param name="poemId"></param>
        /// <param name="coupletIndex">if you send a negative number it means you are trying to bookmark a comment</param>
        /// <returns></returns>
        [HttpPost]
        [Route("bookmark/switch/ret/{poemId}/{coupletIndex}")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> SwitchCoupletBookmarkReturnId(int poemId, int coupletIndex)
        {
            if (ReadOnlyMode)
                return BadRequest("سایت به دلایل فنی مثل انتقال سرور موقتاً در حالت فقط خواندنی قرار دارد. لطفاً ساعاتی دیگر مجدداً تلاش کنید.");
            Guid loggedOnUserId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            RServiceResult<DivanUserBookmark> res = await _divanService.SwitchCoupletBookmark(loggedOnUserId, poemId, coupletIndex);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                if (res.ExceptionString == "verse not found")
                    return NotFound();
                return BadRequest(res.ExceptionString);
            }
            return Ok(res.Result == null ? "0" : res.Result.Id.ToString());
        }

        /// <summary>
        /// Bookmark couplet if it is not
        /// </summary>
        /// <param name="poemId"></param>
        /// <param name="coupletIndex"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("bookmark/{poemId}/{coupletIndex}")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(bool))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> BookmarkCoupletIfNotBookmarked(int poemId, int coupletIndex)
        {
            if (ReadOnlyMode)
                return BadRequest("سایت به دلایل فنی مثل انتقال سرور موقتاً در حالت فقط خواندنی قرار دارد. لطفاً ساعاتی دیگر مجدداً تلاش کنید.");
            Guid loggedOnUserId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            RServiceResult<DivanUserBookmark> res = await _divanService.BookmarkCoupletIfNotBookmarked(loggedOnUserId, poemId, coupletIndex);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                if (res.ExceptionString == "verse not found")
                    return NotFound();
                return BadRequest(res.ExceptionString);
            }
            return Ok(res.Result != null);
        }

        /// <summary>
        /// delete bookmark
        /// </summary>
        /// <param name="bookmarkId"></param>
        /// <returns></returns>
        [HttpDelete]
        [Route("bookmark/{bookmarkId}")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> DeleteBookmark(Guid bookmarkId)
        {
            if (ReadOnlyMode)
                return BadRequest("سایت به دلایل فنی مثل انتقال سرور موقتاً در حالت فقط خواندنی قرار دارد. لطفاً ساعاتی دیگر مجدداً تلاش کنید.");
            Guid loggedOnUserId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            RServiceResult<bool> res = await _divanService.DeleteDivanBookmark(bookmarkId, loggedOnUserId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// modify bookmark private note
        /// </summary>
        /// <param name="bookmarkId"></param>
        /// <param name="note"></param>
        /// <returns></returns>
        [HttpPut]
        [Route("bookmark/{bookmarkId}")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> ModifyBookmarkPrivateNoteAsync(Guid bookmarkId, [FromBody] string note)
        {
            if (ReadOnlyMode)
                return BadRequest("سایت به دلایل فنی مثل انتقال سرور موقتاً در حالت فقط خواندنی قرار دارد. لطفاً ساعاتی دیگر مجدداً تلاش کنید.");
            Guid loggedOnUserId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            RServiceResult<bool> res = await _divanService.ModifyBookmarkPrivateNoteAsync(bookmarkId, loggedOnUserId, note);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }


        /// <summary>
        /// get poem user bookmarks (only Id, CoupletIndex and DateTime are valid in the output view model)
        /// </summary>
        /// <param name="poemId"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("bookmark/{poemId}")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanUserBookmarkViewModel[]))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetPoemUserBookmarks(int poemId)
        {
            Guid loggedOnUserId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            RServiceResult<DivanUserBookmarkViewModel[]> res = await _divanService.GetPoemUserBookmarks(loggedOnUserId, poemId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// is the poem couplet is bookmarked by user
        /// </summary>
        /// <param name="poemId"></param>
        /// <param name="coupletIndex"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("bookmark/{poemId}/{coupletIndex}")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(bool))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> IsCoupletBookmarked(int poemId, int coupletIndex)
        {
            Guid loggedOnUserId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            RServiceResult<bool> res = await _divanService.IsCoupletBookmarked(loggedOnUserId, poemId, coupletIndex);
            if (!string.IsNullOrEmpty(res.ExceptionString))
            {
                if (res.ExceptionString == "verse not found")
                    return NotFound();
                return BadRequest(res.ExceptionString);
            }
            return Ok(res.Result);
        }

        /// <summary>
        /// user bookmarks
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="q">
        /// a phrase to be searched through user private notes
        /// </param>
        /// <returns></returns>
        [HttpGet]
        [Route("bookmark")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanUserBookmarkViewModel[]))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetUserBookmarks([FromQuery] PagingParameterModel paging, string q)
        {
            Guid loggedOnUserId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

            RServiceResult<(PaginationMetadata PagingMeta, DivanUserBookmarkViewModel[] Bookmarks)> res = await _divanService.GetUserBookmarks(paging, loggedOnUserId, q);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(res.Result.PagingMeta));
            return Ok(res.Result.Bookmarks);
        }

        /// <summary>
        /// start generating related sections info for wholepoem sections
        /// </summary>
        /// <param name="regenerate"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("generaterelatedsectionsinfo")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ImportOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(bool))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult StartGeneratingRelatedSectionsInfo(bool regenerate = false)
        {
            RServiceResult<bool> res =
                 _divanService.StartGeneratingRelatedSectionsInfo(regenerate);
            if (res.Result)
                return Ok();
            return BadRequest(res.ExceptionString);
        }

        /// <summary>
        /// get next divan poem probable metre
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        [Route("probablemetre/next")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPoemSection))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetNextDivanPoemProbableMetre()
        {
            RServiceResult<DivanPoemSection> res =
                await _divanService.GetNextDivanPoemProbableMetre();
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            if (res.Result == null)
                return NotFound();
            return Ok(res.Result);
        }

        /// <summary>
        /// get a list of divan poems probable metres
        /// </summary>
        /// <param name="paging"></param>
        /// <returns></returns>

        [HttpGet]
        [Route("probablemetre/list")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<DivanPoemSection>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetUnreviewedDivanPoemProbableMetres([FromQuery] PagingParameterModel paging)
        {
            var res =
                await _divanService.GetUnreviewedDivanPoemProbableMetres(paging);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(res.Result.PagingMeta));
            return Ok(res.Result.Items);
        }

        /// <summary>
        /// save divan poem probable metre
        /// </summary>
        /// <param name="id"></param>
        /// <param name="metre"></param>
        /// <returns></returns>
        [HttpPut]
        [Route("probablemetre/save/{id}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> SaveDivanPoemProbableMetre(int id, [FromBody] string metre)
        {
            RServiceResult<bool> res =
                await _divanService.SaveDivanPoemProbableMetre(id, metre);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// dismiss divan poem probable metre
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>

        [HttpDelete]
        [Route("probablemetre/dismiss/{id}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> DismissDivanPoemProbableMetre(int id)
        {
            RServiceResult<bool> res =
                await _divanService.SaveDivanPoemProbableMetre(id, "dismissed");
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// Finding Category Poems Duplicates
        /// </summary>
        /// <param name="srcCatId"></param>
        /// <param name="destCatId"></param>
        /// <param name="hardTry"></param>
        /// <returns></returns>
        [HttpPost("duplicates/{srcCatId}/{destCatId}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult StartFindingCategoryPoemsDuplicates(int srcCatId, int destCatId, bool hardTry = false)
        {
            var res = _divanService.StartFindingCategoryPoemsDuplicates(srcCatId, destCatId, hardTry);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// manually add a duplicate for a poems
        /// </summary>
        /// <param name="srcCatId"></param>
        /// <param name="srcPoemId"></param>
        /// <param name="destPoemId"></param>
        /// <returns></returns>
        [HttpPost("duplicates/manual/{srcCatId}/{srcPoemId}/{destPoemId}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> AdDuplicateAsync(int srcCatId, int srcPoemId, int destPoemId)
        {
            var res = await _divanService.AdDuplicateAsync(srcCatId, srcPoemId, destPoemId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// delete a duplicate
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("duplicates/{id}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> DeleteDuplicateAsync(int id)
        {
            var res = await _divanService.DeleteDuplicateAsync(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// list of category saved duplicated poems
        /// </summary>
        /// <param name="catId"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("duplicates/{catId}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<DivanDuplicateViewModel>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetCategoryDuplicates(int catId)
        {
            var res =
                await _divanService.GetCategoryDuplicates(catId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// start removing category duplicates
        /// </summary>
        /// <param name="srcCatId"></param>
        /// <param name="destCatId"></param>
        /// <returns></returns>
        [HttpPut("duplicates/finish/{srcCatId}/{destCatId}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult StartRemovingCategoryDuplicates(int srcCatId, int destCatId)
        {
            var res = _divanService.StartRemovingCategoryDuplicates(srcCatId, destCatId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// get couplet sections
        /// </summary>
        /// <param name="poemId"></param>
        /// <param name="coupletIndex"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("couplet/{poemId}/{coupletIndex}/sections")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<DivanPoemSection[]>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetCoupletSectionsAsync(int poemId, int coupletIndex)
        {
            var res =
                await _divanService.GetCoupletSectionsAsync(poemId, coupletIndex);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            if (res.Result == null)
                return NotFound();
            return Ok(res.Result);
        }

        /// <summary>
        /// get all poem sections
        /// </summary>
        /// <param name="poemId"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("sections/{poemId}")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<DivanPoemSection[]>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetPoemSectionsAsync(int poemId)
        {
            var res =
                await _divanService.GetPoemSectionsAsync(poemId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// regenerate poem sections (dangerous: wipes out existing data)
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("poem/{id}/sections/regenerate")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> RegeneratePoemSections(int id)
        {
            var res = await _divanService.RegeneratePoemSections(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }


        /// <summary>
        /// update related sections manually
        /// </summary>
        /// <param name="metreId"></param>
        /// <param name="rhyme"></param>
        /// <returns></returns>

        [HttpPost]
        [Route("sections/updaterelated")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult UpdateRelatedSections(int metreId, string rhyme)
        {
            _divanService.UpdateRelatedSections(metreId, rhyme);
            return Ok();
        }

        /// <summary>
        /// regenerate category related sections
        /// </summary>
        /// <param name="id">category id</param>
        /// <returns></returns>

        [HttpPut]
        [Route("cat/{id}/regenrelatedsections")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(bool))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult StartRegeneratingCateoryRelatedSections(int id)
        {

            RServiceResult<bool> res =
                _divanService.StartRegeneratingCateoryRelatedSections(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// get a specific poem section
        /// </summary>
        /// <param name="sectionId"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("section/{sectionId}")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<DivanPoemSection>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetPoemSectionByIdAsync(int sectionId)
        {
            var res =
                await _divanService.GetPoemSectionByIdAsync(sectionId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            if (res.Result == null)
                return NotFound();
            return Ok(res.Result);
        }

        /// <summary>
        /// start band couplets fix
        /// </summary>
        /// <returns></returns>

        [HttpPost("ontime/fixbandcouplets")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult StartOnTimeBandCoupletsFix()
        {
            var res = _divanService.StartOneTimeBandCoupletsFix();
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// returns last unreviewed correction from the user for a section
        /// </summary>
        /// <param name="id">section id</param>
        /// <returns></returns>
        [HttpGet]
        [Route("section/correction/last/{id}")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPoemSectionCorrectionViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetLastUnreviewedUserCorrectionForSection(int id)
        {
            Guid userId =
               new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

            RServiceResult<DivanPoemSectionCorrectionViewModel> res =
                await _divanService.GetLastUnreviewedUserCorrectionForSection(userId, id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);//might be null
        }

        /// <summary>
        /// send a correction for a section
        /// </summary>
        /// <param name="correction"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("section/correction")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPoemSectionCorrectionViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> SuggestPoemSectionCorrection([FromBody] DivanPoemSectionCorrectionViewModel correction)
        {
            if (ReadOnlyMode)
                return BadRequest("سایت به دلایل فنی مثل انتقال سرور موقتاً در حالت فقط خواندنی قرار دارد. لطفاً ساعاتی دیگر مجدداً تلاش کنید.");

            correction.UserId =
               new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

            RServiceResult<DivanPoemSectionCorrectionViewModel> res =
                await _divanService.SuggestPoemSectionCorrection(correction);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            if (res.Result == null)
                return NotFound();
            return Ok(res.Result);
        }

        /// <summary>
        /// moderate poem section correction
        /// </summary>
        /// <param name="correction"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("section/moderate")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPoemSectionCorrectionViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> ModeratePoemSectionCorrection([FromBody] DivanPoemSectionCorrectionViewModel correction)
        {
            if (ReadOnlyMode)
                return BadRequest("سایت به دلایل فنی مثل انتقال سرور موقتاً در حالت فقط خواندنی قرار دارد. لطفاً ساعاتی دیگر مجدداً تلاش کنید.");

            Guid userId =
               new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

            RServiceResult<DivanPoemSectionCorrectionViewModel> res =
                await _divanService.ModeratePoemSectionCorrection(userId, correction);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            if (res.Result == null)
                return NotFound();
            return Ok(res.Result);
        }

        /// <summary>
        /// delete unreviewed user corrections for a poem section
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete]
        [Route("section/correction/{id}")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> DeletePoemSectionCorrections(int id)
        {
            if (ReadOnlyMode)
                return BadRequest("سایت به دلایل فنی مثل انتقال سرور موقتاً در حالت فقط خواندنی قرار دارد. لطفاً ساعاتی دیگر مجدداً تلاش کنید.");

            var userId =
               new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

            var res =
                await _divanService.DeletePoemSectionCorrections(userId, id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// get section correction by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("section/correction/{id}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPoemSectionCorrectionViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetSectionCorrectionById(int id)
        {
            Guid userId =
               new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

            RServiceResult<DivanPoemSectionCorrectionViewModel> res =
                await _divanService.GetSectionCorrectionById(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            if (res.Result == null)
                return NotFound();
            return Ok(res.Result);//might be null
        }

        /// <summary>
        /// get next unreviewed correction for poem sections
        /// </summary>
        /// <param name="skip"></param>
        /// <param name="deletedUserSections"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("section/correction/next")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPoemSectionCorrectionViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetNextUnreviewedPoemSectionCorrection(int skip = 0, bool deletedUserSections = false)
        {
            RServiceResult<DivanPoemSectionCorrectionViewModel> res =
                await _divanService.GetNextUnreviewedPoemSectionCorrection(skip, deletedUserSections);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);

            var resCount = await _divanService.GetUnreviewedPoemSectionCorrectionCount(deletedUserSections);
            if (!string.IsNullOrEmpty(resCount.ExceptionString))
                return BadRequest(resCount.ExceptionString);

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers",
                JsonConvert.SerializeObject(
                    new PaginationMetadata()
                    {
                        totalCount = resCount.Result,
                        pageSize = -1,
                        currentPage = -1,
                        hasNextPage = false,
                        hasPreviousPage = false,
                        totalPages = -1
                    })
                );

            return Ok(res.Result);//might be null
        }

        /// <summary>
        /// get list of user suggested corrections
        /// </summary>
        /// <param name="paging"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("section/corrections/mine")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<DivanPoemSectionCorrectionViewModel>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetUserSectionCorrections([FromQuery] PagingParameterModel paging)
        {
            Guid userId =
               new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

            var res =
                await _divanService.GetUserSectionCorrections(userId, paging);

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(res.Result.PagingMeta));

            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result.Items);
        }

        /// <summary>
        /// get list of all suggested corrections
        /// </summary>
        /// <param name="paging"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("section/corrections/all")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<DivanPoemSectionCorrectionViewModel>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetAllSectionCorrections([FromQuery] PagingParameterModel paging)
        {

            var res =
                await _divanService.GetUserSectionCorrections(Guid.Empty, paging);

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(res.Result.PagingMeta));

            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result.Items);
        }

        /// <summary>
        /// effective corrections for section
        /// </summary>
        /// <param name="id"></param>
        /// <param name="paging"></param>
        /// <returns></returns>

        [HttpGet]
        [Route("section/{id}/corrections/effective")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<DivanPoemSectionCorrectionViewModel>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetSectionEffectiveCorrections(int id, [FromQuery] PagingParameterModel paging)
        {
            var res =
                await _divanService.GetSectionEffectiveCorrections(id, paging);

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(res.Result.PagingMeta));

            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result.Items);
        }


        /// <summary>
        /// transfer poems and sections from a meter to another one and delete the source meter
        /// </summary>
        /// <param name="srcId"></param>
        /// <param name="destId"></param>
        /// <returns></returns>
        [HttpPut]
        [Route("prosody/transfer/{srcId}/{destId}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> TransferMeterAsync(int srcId, int destId)
        {
            RServiceResult<bool> res =
                await _divanService.TransferMeterAsync(srcId, destId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// get poem tags ordered by LunarDateTotalNumber then by Id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("poem/{id}/geotag")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<PoemGeoDateTag>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetPoemGeoDateTagsAsync(int id)
        {
            var res =
                await _divanService.GetPoemGeoDateTagsAsync(id);

            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }


        /// <summary>
        /// add poem geo tag
        /// </summary>
        /// <param name="tag"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("poem/geotag")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(PoemGeoDateTag))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> AddPoemGeoDateTagAsync([FromBody] PoemGeoDateTag tag)
        {
            RServiceResult<PoemGeoDateTag> res =
                await _divanService.AddPoemGeoDateTagAsync(tag);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// update poem tag
        /// </summary>
        /// <param name="tag"></param>
        /// <returns></returns>
        [HttpPut]
        [Route("poem/geotag")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> UpdatePoemGeoDateTagAsync([FromBody] PoemGeoDateTag tag)
        {
            RServiceResult<bool> res =
                await _divanService.UpdatePoemGeoDateTagAsync(tag);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// delete poem tag
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete]
        [Route("poem/geotag/{id}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> DeletePoemGeoDateTagAsync(int id)
        {
            RServiceResult<bool> res =
                await _divanService.DeletePoemGeoDateTagAsync(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// get a categoty poem tags
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>

        [HttpGet]
        [Route("cat/{id}/geotag")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<PoemGeoDateTag>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetCatPoemGeoDateTagsAsync(int id)
        {
            var res =
                await _divanService.GetCatPoemGeoDateTagsAsync(id);

            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// get the network of people relevant to this category/work (every person tagged in a poem
        /// under its subtree, plus their relatives/affiliates one hop out) - for the "شخصیت‌ها" tab
        /// on a category/poet page
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("cat/{id}/persongraph")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPersonGraphViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetCatPersonGraphAsync(int id)
        {
            var res =
                await _personService.GetCatPersonGraphAsync(id);

            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// synchronize https:://naskban.ir links (logs in and then out to naskban.ir using auth info)
        /// </summary>
        /// <param name="loginViewModel"></param>
        /// <returns>number of synched links</returns>
        [HttpPost("naskban")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ModerateOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult SynchronizeNaskbanLinks(
            [AuditIgnore]
            [FromBody]
            LoginViewModel loginViewModel
            )
        {
            var userId =
               new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            _divanService.SynchronizeNaskbanLinks(new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value), loginViewModel.Username, loginViewModel.Password);
            return Ok();
        }

        [HttpPut("naskban")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ModerateOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult JustifyNaskbanPageNumbers(
           [AuditIgnore]
            [FromBody]
            LoginViewModel loginViewModel
           )
        {
            _divanService.JustifyNaskbanPageNumbers(loginViewModel.Username, loginViewModel.Password);
            return Ok();
        }

        /// <summary>
        /// mark naskban links for poems of a categiory and its children as human reviewed
        /// </summary>
        /// <param name="naskbanBookId"></param>
        /// <param name="catId"></param>
        /// <param name="humanReviewed"></param>
        /// <returns></returns>
        [HttpPut("naskban/humanreviewed/{naskbanBookId}/{catId}/{humanReviewed}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ModerateOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult MarkNaskbanLinksAsHumanReviewed(int naskbanBookId, int catId, bool humanReviewed)
        {
            _divanService.MarkNaskbanLinksAsHumanReviewed(naskbanBookId, catId, humanReviewed);
            return Ok();
        }

        /// <summary>
        /// mark naskban links a text original for a category
        /// </summary>
        /// <param name="naskbanBookId"></param>
        /// <param name="catId"></param>
        /// <param name="textOriginal"></param>
        /// <returns></returns>

        [HttpPut("naskban/textoriginal/{naskbanBookId}/{catId}/{textOriginal}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ModerateOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult MarkNaskbanLinksAsTextOriginal(int naskbanBookId, int catId, bool textOriginal)
        {
            _divanService.MarkNaskbanLinksAsTextOriginal(naskbanBookId, catId, textOriginal);
            return Ok();
        }

        /// <summary>
        /// import naskban divan matchings
        /// </summary>
        /// <param name="loginViewModel"></param>
        /// <returns></returns>
        [HttpPut("naskban/import/matchingbooks")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ModerateOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult ImportNaskbanDivanPoemMatchFindings(
           [AuditIgnore]
            [FromBody]
            LoginViewModel loginViewModel
           )
        {
            _divanService.ImportNaskbanDivanPoemMatchFindings(loginViewModel.Username, loginViewModel.Password);
            return Ok();
        }

        /// <summary>
        /// discover poet naskban paper sources
        /// </summary>
        /// <param name="poetId"></param>
        /// <param name="loginViewModel"></param>
        /// <returns></returns>

        [HttpPut("naskban/import/poetbooks/{poetId}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ModerateOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult DiscoverPoetNaskbanPaperSources(
            int poetId,
           [AuditIgnore]
            [FromBody]
            LoginViewModel loginViewModel
           )
        {
            _divanService.DiscoverPoetNaskbanPaperSources(poetId, loginViewModel.Username, loginViewModel.Password);
            return Ok();
        }

        /// <summary>
        /// delete poem related naskban images by url
        /// </summary>
        /// <param name="naskbanUrl"></param>
        /// <returns></returns>
        [HttpDelete("naskban")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> DeletePoemRelatedNaskbanImagesByNaskbanUrlAsync(string naskbanUrl)
        {
            var res = await _divanService.DeletePoemRelatedNaskbanImagesByNaskbanUrlAsync(naskbanUrl);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// import paper sources from museum
        /// </summary>
        /// <returns></returns>
        [HttpPut]
        [Route("papersources/import/{poetid}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ModerateOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public IActionResult ImportPaperSourcesFromMuseum(int poetid = 0)
        {
            _divanService.ImportPaperSourcesFromMuseum(poetid);
            return Ok();
        }

        /// <summary>
        /// get paper sources for a catgeory
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("cat/{id}/papersources")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPaperSource[]))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetCategoryPaperSourcesAsync(int id)
        {
            var res = await _divanService.GetCategoryPaperSourcesAsync(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// get category poem related images
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("cat/{id}/images")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(PoemRelatedImageEx[]))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetCatPoemImagesAsync(int id)
        {
            var res = await _divanService.GetCatPoemImagesAsync(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }


        /// <summary>
        /// extracting quoted poems
        /// </summary>
        /// <returns></returns>
        [HttpPost("quoted/extract")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult StartExtractingQuotedPoems()
        {
            var res = _divanService.StartExtractingQuotedPoems();

            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);

            return Ok();
        }

        /// <summary>
        /// regenerate related poems pages
        /// </summary>
        /// <returns></returns>
        [HttpPut("quoted/pages/generate")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult StartRegeneratingRelatedPoemsPages()
        {
            var loggedOnUserId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            var res = _divanService.StartRegeneratingRelatedPoemsPages(loggedOnUserId);

            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);

            return Ok();
        }

        /// <summary>
        /// regenerate two poets similar page
        /// </summary>
        /// <param name="poetId"></param>
        /// <param name="relatedPoetId"></param>
        /// <returns></returns>
        [HttpPut("quoted/pages/generate/{poetId}/{relatedPoetId}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult StartRegeneratingRelatedPoemsPageAsync(int poetId, int relatedPoetId)
        {
            var loggedOnUserId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            var res = _divanService.StartRegeneratingRelatedPoemsPageAsync(loggedOnUserId, poetId, relatedPoetId);

            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);

            return Ok();
        }

        /// <summary>
        /// discover related poems
        /// </summary>
        /// <param name="poetId"></param>
        /// <param name="relatedPoetId"></param>
        /// <param name="breakOnFirstSimilar"></param>
        /// <param name="relatedSubCatId"></param>
        /// <param name="insertReverse"></param>
        /// <returns></returns>
        [HttpPost("quoted/discover/{poetId}/{relatedPoetId}/{breakOnFirstSimilar}/{relatedSubCatId}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult StartDiscoverRelatedPoems(int poetId, int relatedPoetId, bool breakOnFirstSimilar = false, int relatedSubCatId = 0, bool insertReverse = false)
        {
            
            var res = _divanService.StartDiscoverRelatedPoems(poetId, relatedPoetId, breakOnFirstSimilar, relatedSubCatId == 0 ? null :  relatedSubCatId, insertReverse);

            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);

            return Ok();
        }

        /// <summary>
        /// get quoted poems for a poem
        /// </summary>
        /// <param name="id"></param>
        /// <param name="skip"></param>
        /// <param name="itemsCount"></param>
        /// <param name="onlyClaimedByBothPoets"></param>
        /// <param name="published"></param>
        /// <param name="chosenForMainList"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("poem/{id}/quoteds")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanQuotedPoemViewModel[]))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetDivanQuotedPoemsForPoemAsync(int id, int skip, int itemsCount, bool? onlyClaimedByBothPoets = null, bool? published = null, bool? chosenForMainList = null)
        {
            RServiceResult<DivanQuotedPoemViewModel[]> res =
                await _divanService.GetDivanQuotedPoemsForPoemAsync(id, skip, itemsCount, onlyClaimedByBothPoets, published, chosenForMainList);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// two poems quoted records
        /// </summary>
        /// <param name="id"></param>
        /// <param name="relatedId"></param>
        /// <param name="published"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("poem/{id}/quoteds/{relatedId}")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanQuotedPoemViewModel[]))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetDivanQuotedPoemsForRelatedAsync(int id, int relatedId, bool? published = null)
        {
            RServiceResult<DivanQuotedPoemViewModel[]> res =
                await _divanService.GetDivanQuotedPoemsForRelatedAsync(id, relatedId, published);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// get quoted poems
        /// </summary>
        /// <param name="poetId"></param>
        /// <param name="relatedPoetId"></param>
        /// <param name="chosen"></param>
        /// <param name="published"></param>
        /// <param name="claimed"></param>
        /// <param name="indirect"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("quoted")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanQuotedPoemViewModel[]))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetDivanQuotedPoemsAsync(int? poetId, int? relatedPoetId, bool? chosen, bool? published, bool? claimed, bool? indirect)
        {
            RServiceResult<DivanQuotedPoemViewModel[]> res =
                await _divanService.GetDivanQuotedPoemsAsync(poetId, relatedPoetId, chosen, published, claimed, indirect);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }


        /// <summary>
        /// get quoted by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("quoted/{id}")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanQuotedPoemViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetDivanQuotedPoemByIdAsync(Guid id)
        {
            RServiceResult<DivanQuotedPoemViewModel> res =
                await _divanService.GetDivanQuotedPoemByIdAsync(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// insert quoted
        /// </summary>
        /// <param name="quoted"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("quoted")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanQuotedPoemViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> InsertDivanQuotedPoemAsync([FromBody] DivanQuotedPoemViewModel quoted)
        {
            var loggedOnUserId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            RServiceResult<DivanQuotedPoemViewModel> res =
                await _divanService.InsertDivanQuotedPoemAsync(quoted, loggedOnUserId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// update quoted
        /// </summary>
        /// <param name="quoted"></param>
        /// <returns></returns>
        [HttpPut]
        [Route("quoted")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> UpdateDivanQuotedPoemsAsync([FromBody] DivanQuotedPoemViewModel quoted)
        {
            var loggedOnUserId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            RServiceResult<bool> res =
                await _divanService.UpdateDivanQuotedPoemsAsync(quoted, loggedOnUserId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// delete quoted
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete]
        [Route("quoted")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> DeleteDivanQuotedPoemByIdAsync(Guid id)
        {
            var loggedOnUserId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            RServiceResult<bool> res =
                await _divanService.DeleteDivanQuotedPoemByIdAsync(id, loggedOnUserId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// suggest new quote (for normal users)
        /// </summary>
        /// <param name="quoted"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("quoted/suggest")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanQuotedPoemViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> SuggestDivanQuotedPoemAsync([FromBody] DivanQuotedPoemViewModel quoted)
        {
            var loggedOnUserId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            RServiceResult<DivanQuotedPoemViewModel> res =
                await _divanService.SuggestDivanQuotedPoemAsync(quoted, loggedOnUserId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// get list of user suggested corrections
        /// </summary>
        /// <param name="paging"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("quoted/suggest")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<DivanQuotedPoemViewModel>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetUserQuotedSuggestionsAsync([FromQuery] PagingParameterModel paging)
        {
            Guid userId =
               new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

            var res =
                await _divanService.GetUserQuotedSuggestionsAsync(userId, paging);

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(res.Result.PagingMeta));

            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result.Items);
        }

        /// <summary>
        /// next unmoderated quoted poem
        /// </summary>
        /// <param name="skip"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("quoted/suggestion/next")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanQuotedPoemViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetNextUnmoderatedDivanQuotedPoemAsync(int skip = 0)
        {
            RServiceResult<DivanQuotedPoemViewModel> res =
                await _divanService.GetNextUnmoderatedDivanQuotedPoemAsync(skip);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);

            var resCount = await _divanService.GetUnmoderatedDivanQuotedsCountAsync();
            if (!string.IsNullOrEmpty(resCount.ExceptionString))
                return BadRequest(resCount.ExceptionString);

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers",
                JsonConvert.SerializeObject(
                    new PaginationMetadata()
                    {
                        totalCount = resCount.Result,
                        pageSize = -1,
                        currentPage = -1,
                        hasNextPage = false,
                        hasPreviousPage = false,
                        totalPages = -1
                    })
                );

            return Ok(res.Result);//might be null
        }

        /// <summary>
        /// moderate quoted poems
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [HttpPut]
        [Route("quoted/moderate")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> ModerateDivanQuotedPoemAsync([FromBody] DivanQuotedPoemModerationViewModel model)
        {
            var loggedOnUserId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            RServiceResult<bool> res =
                await _divanService.ModerateDivanQuotedPoemAsync(model, loggedOnUserId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// digital source from tag
        /// </summary>
        /// <param name="sourceUrlSlug"></param>
        /// <returns></returns>

        [HttpGet]
        [Route("source")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DigitalSource))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetDigitalSourceFromTagAsync(string sourceUrlSlug)
        {
            RServiceResult<DigitalSource> res =
                await _divanService.GetDigitalSourceFromTagAsync(sourceUrlSlug);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);

            return Ok(res.Result);//might be null
        }

        /// <summary>
        /// tag category with source
        /// </summary>
        /// <param name="catId"></param>
        /// <param name="source"></param>
        /// <returns></returns>
        [HttpPut("source/{catId}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult TagCategoryWithSource(int catId, [FromBody]DigitalSource source)
        {
            _divanService.TagCategoryWithSource(catId, source);
            return Ok();
        }

        /// <summary>
        /// update digital sources stats
        /// </summary>
        /// <returns></returns>
        [HttpPost("source/stats/rebuild")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult UpdateDigitalSourcesStats()
        {
            Guid userId =
               new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            _divanService.UpdateDigitalSourcesStats(userId);
            return Ok();
        }

        /// <summary>
        /// add new page
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>

        [HttpPost]
        [Route("page")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPage))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> AddPageAsync([FromBody]DivanPage page)
        {
            var res = await _divanService.AddPageAsync(page);

            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// build word counts
        /// </summary>
        /// <param name="reset"></param>
        /// <param name="poetId"></param>
        /// <returns></returns>

        [HttpPost("wordcounts/rebuild/{poetId}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> BuildCategoryWordCountsAsync(bool reset = false, int poetId = 0)
        {
            await _divanService.BuildCategoryWordCountsAsync(reset, poetId);
            return Ok();
        }

        /// <summary>
        /// one time fixer for word counts new RowNmbrInCat fields
        /// </summary>
        /// <returns></returns>
        [HttpPut("wordcounts/fillwordcounts")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult FillCategoryWordCountsRowNmbrInCat()
        {
            _divanService.FillCategoryWordCountsRowNmbrInCat();
            return Ok();
        }
        /// <summary>
        /// one time fixer for category word count summries
        /// </summary>
        /// <returns></returns>

        [HttpPut("wordcounts/fillwordcountsummeries")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult FillCategoryWordCountSummaries()
        {
            _divanService.FillCategoryWordCountSummaries();
            return Ok();
        }


        /// <summary>
        /// category word counts
        /// </summary>
        /// <param name="catId"></param>
        /// <param name="term">can be empty</param>
        /// <param name="paging"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("wordcounts/{catId}")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<CategoryWordCount>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> GetCategoryWordCountsAsync(int catId, string term, [FromQuery] PagingParameterModel paging)
        {
            var pagedResult = await _divanService.GetCategoryWordCountsAsync(catId, term, paging);
            if (!string.IsNullOrEmpty(pagedResult.ExceptionString))
                return BadRequest(pagedResult.ExceptionString);

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(pagedResult.Result.PagingMeta));

            return Ok(pagedResult.Result.Items);
        }

        /// <summary>
        /// category words summary
        /// </summary>
        /// <param name="catId"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("wordsums/{catId}")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(CategoryWordCountSummary))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> GetCategoryWordCountSummaryAsync(int catId)
        {
            var res = await _divanService.GetCategoryWordCountSummaryAsync(catId);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);

           return Ok(res.Result);
        }

        /// <summary>
        /// comparison of word counts for poets
        /// </summary>
        /// <param name="term"></param>
        /// <param name="paging"></param>
        /// <param name="catId">can be null or can be extracted from poetId</param>
        /// <param name="poetId">is not necessary unless you mean a specific poet and do not provide catId</param>
        /// <returns></returns>
        [HttpGet]
        [Route("wordcounts/bycat")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<PoetOrCatWordStat>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> GetCategoryWordCountsByPoetsAsync(string term, [FromQuery] PagingParameterModel paging, int? catId, int? poetId)
        {
            var pagedResult = await _divanService.GetCategoryWordCountsBySubCatsAsync(term, paging, catId, poetId);
            if (!string.IsNullOrEmpty(pagedResult.ExceptionString))
                return BadRequest(pagedResult.ExceptionString);

            var wordCountResult = await _divanService.GetCategoryWordCountByTermAsync(term, catId, poetId);
            if(!string.IsNullOrEmpty(wordCountResult.ExceptionString))
                return BadRequest(wordCountResult.ExceptionString);

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(pagedResult.Result.PagingMeta));
            HttpContext.Response.Headers.Append("items-count", JsonConvert.SerializeObject(wordCountResult.Result.Count));

            return Ok(pagedResult.Result.Items);
        }

        /// <summary>
        /// CategoryWordCount for a specific term in a category
        /// </summary>
        /// <param name="term"></param>
        /// <param name="catId"></param>
        /// <param name="poetId"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("wordcounts/bycat/count")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(CategoryWordCount))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]

        public async Task<IActionResult> GetCategoryWordCountByTermAsync(string term, int? catId, int? poetId)
        {
            var countResult = await _divanService.GetCategoryWordCountByTermAsync(term, catId, poetId);
            if (!string.IsNullOrEmpty(countResult.ExceptionString))
                return BadRequest(countResult.ExceptionString);

            return Ok(countResult.Result);
        }



        /// <summary>
        /// fill couplet summaries using open ai
        /// </summary>
        /// <param name="startFrom"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        [HttpPut("ai/generate/summaries")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult OpenAIStartFillingCoupletSummaries(int startFrom = 0, int count = 0)
        {
            _divanService.OpenAIStartFillingCoupletSummaries(startFrom, count);
            return Ok();
        }

        /// <summary>
        /// fill poem summaries using open ai
        /// </summary>
        /// <param name="startFrom"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        [HttpPut("ai/generate/poem/summaries")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult OpenAIStartFillingPoemSummaries(int startFrom = 0, int count = 0)
        {
            _divanService.OpenAIStartFillingPoemSummaries(startFrom, count);
            return Ok();
        }

        /// <summary>
        /// geo tag poems using AI
        /// </summary>
        /// <param name="startFrom"></param>
        /// <param name="count"></param>
        /// <returns></returns>
        [HttpPut("ai/generate/poem/geo")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public IActionResult OpenAIStartFillingGeoLocations(int startFrom = 0, int count = 0)
        {
            _divanService.OpenAIStartFillingGeoLocations(startFrom, count);
            return Ok();
        }


        /// <summary>
        /// send cat corrections
        /// </summary>
        /// <param name="correction"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("cat/correction")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanCatCorrectionViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> SuggestCatCorrectionAsync([FromBody] DivanCatCorrectionViewModel correction)
        {
            if (ReadOnlyMode)
                return BadRequest("سایت به دلایل فنی مثل انتقال سرور موقتاً در حالت فقط خواندنی قرار دارد. لطفاً ساعاتی دیگر مجدداً تلاش کنید.");

            correction.UserId =
               new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

            RServiceResult<DivanCatCorrectionViewModel> res =
                await _divanService.SuggestCatCorrectionAsync(correction);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            if (res.Result == null)
                return NotFound();
            return Ok(res.Result);
        }

        /// <summary>
        /// delete unreviewed user cat corrections for a poem
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete]
        [Route("cat/correction/{id}")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> DeleteCatCorrectionsAsync(int id)
        {
            if (ReadOnlyMode)
                return BadRequest("سایت به دلایل فنی مثل انتقال سرور موقتاً در حالت فقط خواندنی قرار دارد. لطفاً ساعاتی دیگر مجدداً تلاش کنید.");

            var userId =
               new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

            var res =
                await _divanService.DeleteCatCorrectionsAsync(userId, id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok();
        }

        /// <summary>
        /// returns last unreviewed correction from the user for a cat
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("cat/correction/last/{id}")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanCatCorrectionViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetLastUnreviewedUserCorrectionForCatAsync(int id)
        {
            Guid userId =
               new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

            RServiceResult<DivanCatCorrectionViewModel> res =
                await _divanService.GetLastUnreviewedUserCorrectionForCatAsync(userId, id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);//might be null
        }

        /// <summary>
        /// get list of user suggested cat corrections
        /// </summary>
        /// <param name="paging"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("cat/corrections/mine")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<DivanCatCorrectionViewModel>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetUserCatCorrectionsAsync([FromQuery] PagingParameterModel paging)
        {
            Guid userId =
               new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

            var res =
                await _divanService.GetUserCatCorrectionsAsync(userId, paging);

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(res.Result.PagingMeta));

            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result.Items);
        }

        /// <summary>
        /// get list of all suggested cat corrections
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="userId">userId</param>
        /// <returns></returns>
        [HttpGet]
        [Route("cat/corrections/all")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<DivanCatCorrectionViewModel>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetAllCatCorrectionsAsync([FromQuery] PagingParameterModel paging, Guid? userId = null)
        {

            var res =
                await _divanService.GetUserCatCorrectionsAsync(userId ?? Guid.Empty, paging);

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(res.Result.PagingMeta));

            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result.Items);
        }

        /// <summary>
        /// effective corrections for a cat
        /// </summary>
        /// <param name="id"></param>
        /// <param name="paging"></param>
        /// <returns></returns>

        [HttpGet]
        [Route("cat/{id}/corrections/effective")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(IEnumerable<DivanCatCorrectionViewModel>))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetCatEffectiveCorrectionsAsync(int id, [FromQuery] PagingParameterModel paging)
        {
            var res =
                await _divanService.GetCatEffectiveCorrectionsAsync(id, paging);

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers", JsonConvert.SerializeObject(res.Result.PagingMeta));

            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result.Items);
        }

        /// <summary>
        /// get cat correction by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("cat/correction/{id}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanCatCorrectionViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetCatCorrectionByIdAsync(int id)
        {
            Guid userId =
               new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

            RServiceResult<DivanCatCorrectionViewModel> res =
                await _divanService.GetCatCorrectionByIdAsync(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);//might be null
        }

        /// <summary>
        /// get next unreviewed cat correction
        /// </summary>
        /// <param name="skip"></param>
        /// <param name="onlyUserCorrections"></param>
        /// <returns></returns>
        [HttpGet]
        [Route("cat/correction/next")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanCatCorrectionViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetNextUnreviewedCatCorrectionAsync(int skip = 0, bool onlyUserCorrections = false)
        {
            RServiceResult<DivanCatCorrectionViewModel> res =
                await _divanService.GetNextUnreviewedCatCorrectionAsync(skip, onlyUserCorrections);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);

            var resCount = await _divanService.GetUnreviewedCatCorrectionCountAsync(onlyUserCorrections);
            if (!string.IsNullOrEmpty(resCount.ExceptionString))
                return BadRequest(resCount.ExceptionString);

            // Paging Header
            HttpContext.Response.Headers.Append("paging-headers",
                JsonConvert.SerializeObject(
                    new PaginationMetadata()
                    {
                        totalCount = resCount.Result,
                        pageSize = -1,
                        currentPage = -1,
                        hasNextPage = false,
                        hasPreviousPage = false,
                        totalPages = -1
                    })
                );

            return Ok(res.Result);//might be null
        }

        /// <summary>
        /// moderate cat correction
        /// </summary>
        /// <param name="correction"></param>
        /// <returns></returns>
        [HttpPost]
        [Route("cat/correction/moderate")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + SecurableItem.ModifyOperationShortName)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanCatCorrectionViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> ModerateCatCorrectionAsync([FromBody] DivanCatCorrectionViewModel correction)
        {
            if (ReadOnlyMode)
                return BadRequest("سایت به دلایل فنی مثل انتقال سرور موقتاً در حالت فقط خواندنی قرار دارد. لطفاً ساعاتی دیگر مجدداً تلاش کنید.");

            Guid userId =
               new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);

            RServiceResult<DivanCatCorrectionViewModel> res =
                await _divanService.ModerateCatCorrectionAsync(userId, correction);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            if (res.Result == null)
                return NotFound();
            return Ok(res.Result);
        }




        /// <summary>
        /// readonly mode
        /// </summary>
        public bool ReadOnlyMode
        {
            get
            {
                try
                {
                    return bool.Parse(Configuration["ReadOnlyMode"]);
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Divan Service
        /// </summary>

        protected readonly IDivanService _divanService;

        /// <summary>
        /// IAppUserService instance
        /// </summary>
        protected IAppUserService _appUserService;

        /// <summary>
        /// for client IP resolution
        /// </summary>
        protected IHttpContextAccessor _httpContextAccessor;

        /// <summary>
        /// Image Service
        /// </summary>
        protected readonly IImageFileService _imageFileService;

        /// <summary>
        /// IMemoryCache
        /// </summary>
        protected readonly IMemoryCache _memoryCache;

        /// <summary>
        /// aggressive cache
        /// </summary>
        private bool AggressiveCacheEnabled
        {
            get
            {
                try
                {
                    return bool.Parse(Configuration["AggressiveCacheEnabled"]);
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Configuration
        /// </summary>
        protected IConfiguration Configuration { get; }

        /// <summary>
        /// related people (family tree / person tagging) service - used here only for the
        /// category-scoped "cat/{id}/persongraph" endpoint; everything else about people lives in
        /// DivanRelatedPersonController
        /// </summary>
        protected readonly IDivanRelatedPersonService _personService;

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="divanService"></param>
        /// <param name="appUserService"></param>
        /// <param name="httpContextAccessor"></param>
        /// <param name="imageFileService"></param>
        /// <param name="memoryCache"></param>
        /// <param name="configuration"></param>
        /// <param name="personService"></param>
        public DivanController(
            IDivanService divanService,
            IAppUserService appUserService,
            IHttpContextAccessor httpContextAccessor,
            IImageFileService imageFileService,
            IMemoryCache memoryCache,
            IConfiguration configuration,
            IDivanRelatedPersonService personService
            )
        {
            _divanService = divanService;
            _appUserService = appUserService;
            _httpContextAccessor = httpContextAccessor;
            _imageFileService = imageFileService;
            _memoryCache = memoryCache;
            Configuration = configuration;
            _personService = personService;
        }
    }
}
