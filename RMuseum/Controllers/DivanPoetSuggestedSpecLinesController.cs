using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using RMuseum.Models.Auth.Memory;
using RMuseum.Models.Divan.ViewModels;
using RMuseum.Services;
using RSecurityBackend.Models.Generic;
using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace RMuseum.Controllers
{
    [Produces("application/json")]
    [Route("api/poetspecs")]
    public class DivanPoetSuggestedSpecLinesController : Controller
    {
        /// <summary>
        /// returns list of suggested spec lines for a poet
        /// </summary>
        /// <param name="id">poet id</param>
        /// <returns></returns>
        [HttpGet("poet/{id}")]
        [AllowAnonymous]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPoetSuggestedSpecLineViewModel[]))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetPoetSuggestedSpecLinesAsync(int id)
        {
            var res = await _divanService.GetPoetSuggestedSpecLinesAsync(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// returns specific suggested line for poets
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>

        [HttpGet("{id}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ModeratePoetPhotos)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPoetSuggestedSpecLineViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        public async Task<IActionResult> GetPoetSuggestedSpecLineAsync(int id)
        {
            var res = await _divanService.GetPoetSuggestedSpecLineAsync(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// return the next unpublished suggested line for poets
        /// </summary>
        /// <param name="skip"></param>
        /// <returns></returns>

        [HttpGet("unpublished/next")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ModeratePoetPhotos)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPoetSuggestedSpecLineViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<IActionResult> GetNextUnmoderatedPoetSuggestedSpecLineAsync(int skip)
        {
            var res = await _divanService.GetNextUnmoderatedPoetSuggestedSpecLineAsync(skip);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            if (res.Result == null)
                return NotFound();

            var resCount = await _divanService.GetNextUnmoderatedPoetSuggestedSpecLinesCountAsync();
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


            return Ok(res.Result);
        }

        /// <summary>
        /// add a suggestion for poets spec lines
        /// </summary>
        /// <param name="spec"></param>
        /// <returns></returns>
        [HttpPost]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(DivanPoetSuggestedSpecLineViewModel))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> AddPoetSuggestedSpecLinesAsync([FromBody] DivanPoetSuggestedSpecLineViewModel spec)
        {
            spec.SuggestedById = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            var res = await _divanService.AddPoetSuggestedSpecLinesAsync(spec);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// modify a suggestion for poets spec lines
        /// </summary>
        /// <param name="spec"></param>
        /// <returns></returns>
        [HttpPut]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ModeratePoetPhotos)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(bool))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> ModifyPoetSuggestedSpecLinesAsync([FromBody] DivanPoetSuggestedSpecLineViewModel spec)
        {
            var res = await _divanService.ModifyPoetSuggestedSpecLinesAsync(spec);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// reject  a suggestion for poets spec lines
        /// </summary>
        /// <param name="id"></param>
        /// <param name="rejectionCause"></param>
        /// <returns></returns>
        [HttpPut("reject/{id}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ModeratePoetPhotos)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(bool))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> RejectPoetSuggestedSpecLinesAsync(int id, [FromBody] string rejectionCause)
        {
            Guid userId = new Guid(User.Claims.FirstOrDefault(c => c.Type == "UserId").Value);
            var res = await _divanService.RejectPoetSuggestedSpecLinesAsync(id, userId, rejectionCause);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }

        /// <summary>
        /// delete published suggested spec line
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpDelete("{id}")]
        [Authorize(Policy = RMuseumSecurableItem.DivanEntityShortName + ":" + RMuseumSecurableItem.ModeratePoetPhotos)]
        [ProducesResponseType((int)HttpStatusCode.OK, Type = typeof(bool))]
        [ProducesResponseType((int)HttpStatusCode.BadRequest, Type = typeof(string))]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        public async Task<IActionResult> DeletePoetSuggestedSpecLinesAsync(int id)
        {

            var res = await _divanService.DeletePoetSuggestedSpecLinesAsync(id);
            if (!string.IsNullOrEmpty(res.ExceptionString))
                return BadRequest(res.ExceptionString);
            return Ok(res.Result);
        }



        /// <summary>
        /// Divan Service
        /// </summary>
        protected readonly IDivanService _divanService;

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="divanService"></param>
        public DivanPoetSuggestedSpecLinesController(IDivanService divanService)
        {
            _divanService = divanService;
        }
    }
}
