using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RMuseum.DbContext;
using RMuseum.Models.Divan;
using RMuseum.Models.Divan.ViewModels;
using RMuseum.Models.DivanAudio;
using RMuseum.Models.DivanAudio.ViewModels;
using RSecurityBackend.Models.Generic;
using RSecurityBackend.Services;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using RMuseum.Models.Artifact;
using RSecurityBackend.Services.Implementation;
using DNTPersianUtils.Core;
using Microsoft.Extensions.Caching.Memory;
using System.Net.Http;
using System.Web;
using System.Text.RegularExpressions;
using RMuseum.Models.Auth.Memory;
using System.IO;
using RSecurityBackend.Models.Image;
using FluentFTP;
using System.Drawing;
using RMuseum.Models.DivanIntegration;
using RSecurityBackend.Models.Notification;

namespace RMuseum.Services.Implementation
{
    /// <summary>
    /// IDivanService implementation
    /// </summary>
    public partial class DivanService : IDivanService
    {

        /// <summary>
        /// Get List of poets
        /// </summary>
        /// <param name="published"></param>
        /// <param name="includeBio"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanPoetViewModel[]>> GetPoets(bool published, bool includeBio = true)
        {
            var cacheKey = $"/api/divan/poets?published={published}&includeBio={includeBio}";
            if (!_memoryCache.TryGetValue(cacheKey, out DivanPoetViewModel[] poets))
            {
                var res =
                 await
                 (from poet in _context.DivanPoets.Include(p => p.BirthLocation).Include(p => p.DeathLocation)
                  join cat in _context.DivanCategories.Where(c => c.ParentId == null)
                  on poet.Id equals cat.PoetId
                  where !published || poet.Published
                  select new DivanPoetViewModel()
                  {
                      Id = poet.Id,
                      Name = poet.Name,
                      Description = includeBio ? poet.Description : null,
                      FullUrl = cat.FullUrl,
                      RootCatId = cat.Id,
                      Nickname = poet.Nickname,
                      Published = poet.Published,
                      ImageUrl = $"/api/divan/poet/image{cat.FullUrl}.gif",
                      BirthYearInLHijri = poet.BirthYearInLHijri,
                      DeathYearInLHijri = poet.DeathYearInLHijri,
                      ValidBirthDate = poet.ValidBirthDate,
                      ValidDeathDate = poet.ValidDeathDate,
                      BirthPlace = poet.BirthLocation == null ? "" : poet.BirthLocation.Name,
                      BirthPlaceLatitude = poet.BirthLocation == null ? 0 : poet.BirthLocation.Latitude,
                      BirthPlaceLongitude = poet.BirthLocation == null ? 0 : poet.BirthLocation.Longitude,
                      DeathPlace = poet.DeathLocation == null ? "" : poet.DeathLocation.Name,
                      DeathPlaceLatitude = poet.DeathLocation == null ? 0 : poet.DeathLocation.Latitude,
                      DeathPlaceLongitude = poet.DeathLocation == null ? 0 : poet.DeathLocation.Longitude,
                      PinOrder = poet.PinOrder,
                  }
                  )
                  .AsNoTracking()
                 .ToListAsync();

                StringComparer fa = StringComparer.Create(new CultureInfo("ur-PK"), true);
                res.Sort((a, b) => fa.Compare(a.Nickname, b.Nickname));
                poets = res.ToArray();
                if (AggressiveCacheEnabled)
                    _memoryCache.Set(cacheKey, poets, TimeSpan.FromHours(1));
            }

            return new RServiceResult<DivanPoetViewModel[]>
                (
                    poets
                );
        }

        /// <summary>
        /// get poet by id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="catPoems"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanPoetCompleteViewModel>> GetPoetById(int id, bool catPoems = false)
        {
            var cacheKey = $"/api/divan/poet/{id}";

            if (!_memoryCache.TryGetValue(cacheKey, out DivanPoetCompleteViewModel poetCat))
            {
                var poet = await _context.DivanPoets.Where(p => p.Id == id).AsNoTracking().FirstOrDefaultAsync();
                if (poet == null)
                    return new RServiceResult<DivanPoetCompleteViewModel>(null);
                var cat = await _context.DivanCategories.Where(c => c.ParentId == null && c.PoetId == id).AsNoTracking().FirstOrDefaultAsync();
                poetCat = (await GetCatById(cat.Id, catPoems, false, true)).Result;
                if (poetCat != null && AggressiveCacheEnabled)
                {
                    _memoryCache.Set(cacheKey, poetCat, TimeSpan.FromHours(1));
                }
            }
            return new RServiceResult<DivanPoetCompleteViewModel>(poetCat);
        }

        /// <summary>
        /// get poet by url
        /// </summary>
        /// <param name="url"></param>
        /// <param name="catPoems"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanPoetCompleteViewModel>> GetPoetByUrl(string url, bool catPoems = false)
        {
            // /hafez/ => /hafez :
            if (url.LastIndexOf('/') == url.Length - 1)
            {
                url = url.Substring(0, url.Length - 1);
            }
            var cat = await _context.DivanCategories.Where(c => c.FullUrl == url && c.ParentId == null).AsNoTracking().SingleOrDefaultAsync();
            if (cat == null)
                return new RServiceResult<DivanPoetCompleteViewModel>(null);
            return await GetCatById(cat.Id, catPoems);
        }

        /// <summary>
        /// poet image id by url
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public async Task<RServiceResult<Guid>> GetPoetImageIdByUrl(string url)
        {
            // /hafez/ => /hafez :
            if (url.LastIndexOf('/') == url.Length - 1)
            {
                url = url.Substring(0, url.Length - 1);
            }
            var cat = await _context.DivanCategories.Where(c => c.FullUrl == url && c.ParentId == null).AsNoTracking().SingleOrDefaultAsync();
            if (cat == null)
                return new RServiceResult<Guid>(Guid.Empty);
            var poet = await _context.DivanPoets.Where(p => p.Id == cat.PoetId).AsNoTracking().SingleOrDefaultAsync();
            return new RServiceResult<Guid>(poet?.RImageId ?? Guid.Empty); // divan: no portrait -> placeholder (was a null cast -> 500)
        }

        /// <summary>
        /// get cat by url
        /// </summary>
        /// <param name="url"></param>
        /// <param name="poems"></param>
        /// <param name="mainSections"></param>
        /// <param name="paperSources"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanPoetCompleteViewModel>> GetCatByUrl(string url, bool poems = false, bool mainSections = false, bool paperSources = false)
        {
            // /hafez/ => /hafez :
            if (url.LastIndexOf('/') == url.Length - 1)
            {
                url = url.Substring(0, url.Length - 1);
            }
            var cat = await _context.DivanCategories.Where(c => c.FullUrl == url).AsNoTracking().SingleOrDefaultAsync();
            if (cat == null)
                return new RServiceResult<DivanPoetCompleteViewModel>(null);
            return await GetCatById(cat.Id, poems, mainSections, paperSources);
        }



        /// <summary>
        /// get list of books
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<DivanCatViewModel[]>> GetBooksAsync()
        {
            try
            {
                return new RServiceResult<DivanCatViewModel[]>
                    (
                    await _context.DivanCategories.AsNoTracking().Where(c => !string.IsNullOrEmpty(c.BookName)).OrderBy(c => c.BookName)
                    .Select(c => new DivanCatViewModel()
                    {
                        BookName = c.BookName,
                        FullUrl = c.FullUrl,
                        RImageId = c.RImageId,
                    }
                    )
                    .ToArrayAsync()
                    );
            }
            catch (Exception exp)
            {
                return new RServiceResult<DivanCatViewModel[]>(null, exp.ToString());
            }
        }

        /// <summary>
        /// generate missing book covers
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> GenerateMissingBookCoversAsync()
        {
            try
            {
                var books = await _context.DivanCategories.Where(c => !string.IsNullOrEmpty(c.BookName) && c.RImageId == null).ToListAsync();
                foreach (var book in books)
                {
                    using (System.Drawing.Image coverImg = System.Drawing.Image.FromFile(Configuration.GetSection("Divan")["BooksCoverTemplate"]))
                    {
                        using (Graphics g = Graphics.FromImage(coverImg))
                        {
                            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;
                            using (Font font = new Font(Configuration.GetSection("Divan")["BooksCoverTitleFontName"], float.Parse(Configuration.GetSection("Divan")["BooksCoverTitleFontSize"])))
                            {
                                SizeF sz = g.MeasureString(book.BookName, font);
                                using (SolidBrush brsh = new SolidBrush(Color.FromArgb(51, 0, 0)))
                                    g.DrawString(book.BookName, font, brsh, new PointF(200.0f - sz.Width / 2, 230.0f - sz.Height / 2));
                            }

                        }
                        MemoryStream coverData = new MemoryStream();
                        coverImg.Save(coverData, System.Drawing.Imaging.ImageFormat.Jpeg);
                        coverData.Seek(0, SeekOrigin.Begin);
                        RServiceResult<RImage> imageRes = await _imageFileService.Add(null, coverData, $"Book-{book.Id}", "CategoryImages");
                        if (!string.IsNullOrEmpty(imageRes.ExceptionString))
                        {
                            return new RServiceResult<bool>(false, imageRes.ExceptionString);
                        }
                        imageRes = await _imageFileService.Store(imageRes.Result);
                        if (!string.IsNullOrEmpty(imageRes.ExceptionString))
                        {
                            return new RServiceResult<bool>(false, imageRes.ExceptionString);
                        }
                        var finalRes = await SetCategoryExtraInfo(book.Id, book.BookName, imageRes.Result.Id, book.SumUpSubsGeoLocations, book.MapName);
                        if (!string.IsNullOrEmpty(finalRes.ExceptionString))
                        {
                            return new RServiceResult<bool>(false, finalRes.ExceptionString);
                        }
                    }
                }
                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// get cat by id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="poems"></param>
        /// <param name="mainSections"></param>
        /// <param name="paperSources"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanPoetCompleteViewModel>> GetCatById(int id, bool poems = false, bool mainSections = false, bool paperSources = false)
        {
            return await _GetCatById(_context, id, poems, mainSections, paperSources);
        }

        private async Task<RServiceResult<DivanPoetCompleteViewModel>> _GetCatById(RMuseumDbContext context, int id, bool poems = false, bool mainSections = false, bool paperSources = false)
        {
            var cat = await context.DivanCategories.Include(c => c.Poet).Include(c => c.Parent).Where(c => c.Id == id).AsNoTracking().FirstOrDefaultAsync();
            if (cat == null)
                return new RServiceResult<DivanPoetCompleteViewModel>(null);

            List<DivanCatViewModel> ancetors = new List<DivanCatViewModel>();

            var parent = cat.Parent;
            while (parent != null)
            {
                ancetors.Insert(0, new DivanCatViewModel()
                {
                    Id = parent.Id,
                    Title = parent.Title,
                    UrlSlug = parent.UrlSlug,
                    FullUrl = parent.FullUrl,
                    TableOfContentsStyle = parent.TableOfContentsStyle,
                    CatType = parent.CatType,
                    Description = parent.Description,
                    DescriptionHtml = parent.DescriptionHtml,
                    MixedModeOrder = parent.MixedModeOrder,
                    Published = parent.Published,
                    BookName = parent.BookName,
                    RImageId = parent.RImageId,
                    SumUpSubsGeoLocations = parent.SumUpSubsGeoLocations,
                    MapName = parent.MapName,
                });

                parent = await context.DivanCategories.Where(c => c.Id == parent.ParentId).AsNoTracking().FirstOrDefaultAsync();
            }


            int nextCatId =
                await context.DivanCategories.Where(c => c.PoetId == cat.PoetId && c.ParentId == cat.ParentId && c.Id > id).AnyAsync() ?
                await context.DivanCategories.Where(c => c.PoetId == cat.PoetId && c.ParentId == cat.ParentId && c.Id > id).MinAsync(c => c.Id)
                :
                0;
            var nextCat = nextCatId == 0 ? null : await context
                                        .DivanCategories
                                        .Where(c => c.Id == nextCatId)
                                        .Select
                                        (
                                            c =>
                                                new DivanCatViewModel()
                                                {
                                                    Id = c.Id,
                                                    Title = c.Title,
                                                    UrlSlug = c.UrlSlug,
                                                    FullUrl = c.FullUrl,
                                                    TableOfContentsStyle = c.TableOfContentsStyle,
                                                    CatType = c.CatType,
                                                    Description = c.Description,
                                                    DescriptionHtml = c.DescriptionHtml,
                                                    MixedModeOrder = c.MixedModeOrder,
                                                    Published = c.Published,
                                                    BookName = c.BookName,
                                                    RImageId = c.RImageId,
                                                    SumUpSubsGeoLocations = c.SumUpSubsGeoLocations,
                                                    MapName = c.MapName,
                                                    //other fields null
                                                }
                                        ).AsNoTracking().SingleOrDefaultAsync();

            int preCatId =
                 await context.DivanCategories.Where(c => c.PoetId == cat.PoetId && c.ParentId == cat.ParentId && c.Id < id).AnyAsync() ?
                await context.DivanCategories.Where(c => c.PoetId == cat.PoetId && c.ParentId == cat.ParentId && c.Id < id).MaxAsync(c => c.Id)
                :
                0;
            var preCat = preCatId == 0 ? null : await context
                                        .DivanCategories
                                        .Where(c => c.Id == preCatId)
                                        .Select
                                        (
                                            c =>
                                                new DivanCatViewModel()
                                                {
                                                    Id = c.Id,
                                                    Title = c.Title,
                                                    UrlSlug = c.UrlSlug,
                                                    FullUrl = c.FullUrl,
                                                    TableOfContentsStyle = c.TableOfContentsStyle,
                                                    CatType = c.CatType,
                                                    Description = c.Description,
                                                    DescriptionHtml = c.DescriptionHtml,
                                                    MixedModeOrder = c.MixedModeOrder,
                                                    Published = c.Published,
                                                    BookName = c.BookName,
                                                    RImageId = c.RImageId,
                                                    SumUpSubsGeoLocations = c.SumUpSubsGeoLocations,
                                                    MapName = c.MapName,
                                                    //other fields null
                                                }
                                        ).AsNoTracking().SingleOrDefaultAsync();

            DivanCatViewModel catViewModel = new DivanCatViewModel()
            {
                Id = cat.Id,
                Title = cat.Title,
                UrlSlug = cat.UrlSlug,
                FullUrl = cat.FullUrl,
                TableOfContentsStyle = cat.TableOfContentsStyle,
                CatType = cat.CatType,
                Description = cat.Description,
                DescriptionHtml = cat.DescriptionHtml,
                MixedModeOrder = cat.MixedModeOrder,
                Published = cat.Published,
                BookName = cat.BookName,
                RImageId = cat.RImageId,
                SumUpSubsGeoLocations = cat.SumUpSubsGeoLocations,
                MapName = cat.MapName,
                Next = nextCat,
                Previous = preCat,
                Ancestors = ancetors,
                Children = await context.DivanCategories.Where(c => c.ParentId == cat.Id).OrderBy(cat => cat.Id).Select
                 (
                 c => new DivanCatViewModel()
                 {
                     Id = c.Id,
                     Title = c.Title,
                     UrlSlug = c.UrlSlug,
                     FullUrl = c.FullUrl,
                     MixedModeOrder = c.MixedModeOrder,
                     Published = c.Published,
                 }
                 ).AsNoTracking().ToListAsync(),
                Poems = poems ? await context.DivanPoems
                .Where(p => p.CatId == cat.Id).OrderBy(p => p.Id).Select
                 (
                     p => new DivanPoemSummaryViewModel()
                     {
                         Id = p.Id,
                         Title = p.Title,
                         UrlSlug = p.UrlSlug,
                         Excerpt = context.DivanVerses.Where(v => v.PoemId == p.Id && v.VOrder == 1).FirstOrDefault().Text,
                     }
                 ).AsNoTracking().ToListAsync()
                 :
                 null,
                PaperSources = paperSources ? 
                    cat.ParentId == null ? await context.DivanPaperSources.AsNoTracking().Where(p => p.DivanPoetId == cat.PoetId).OrderByDescending(p => p.IsTextOriginalSource).ThenBy(p => p.OrderIndicator).ToListAsync()
                    : await context.DivanPaperSources.AsNoTracking().Where(p => p.DivanCatId == cat.Id).OrderByDescending(p => p.IsTextOriginalSource).ThenBy(p => p.OrderIndicator).ToListAsync() : 
                    null,
            };

            if (poems && mainSections)
            {
                foreach (var poem in catViewModel.Poems)
                {
                    poem.MainSections = await context.DivanPoemSections.AsNoTracking().Include(s => s.DivanMetre).Where(s => s.PoemId == poem.Id && s.SectionType == PoemSectionType.WholePoem && s.VerseType == VersePoemSectionType.First).OrderBy(s => s.Index).ToArrayAsync();
                    foreach (var section in poem.MainSections)
                    {
                        var firstVerse = await context.DivanVerses.AsNoTracking().Where(v => v.PoemId == section.PoemId &&
                            (
                            (section.VerseType == VersePoemSectionType.First && v.SectionIndex1 == section.Index)
                            ||
                            (section.VerseType == VersePoemSectionType.Second && v.SectionIndex2 == section.Index)
                            ||
                            (section.VerseType == VersePoemSectionType.Third && v.SectionIndex3 == section.Index)
                            ||
                            (section.VerseType == VersePoemSectionType.Forth && v.SectionIndex4 == section.Index)
                            )
                        ).OrderBy(v => v.VOrder).FirstOrDefaultAsync();
                        section.Excerpt = firstVerse == null ? "" : firstVerse.Text;
                    }
                }
            }

            return new RServiceResult<DivanPoetCompleteViewModel>
               (
               new DivanPoetCompleteViewModel()
               {
                   Poet = await context.DivanPoets.Include(p => p.BirthLocation).Include(p => p.DeathLocation).Where(p => p.Id == cat.PoetId)
                                        .Select(poet => new DivanPoetViewModel()
                                        {
                                            Id = poet.Id,
                                            Name = poet.Name,
                                            Description = poet.Description,
                                            FullUrl = context.DivanCategories.Where(c => c.PoetId == poet.Id && c.ParentId == null).Single().FullUrl,
                                            RootCatId = context.DivanCategories.Where(c => c.PoetId == poet.Id && c.ParentId == null).Single().Id,
                                            Nickname = poet.Nickname,
                                            Published = poet.Published,
                                            ImageUrl = $"/api/divan/poet/image{context.DivanCategories.Where(c => c.PoetId == poet.Id && c.ParentId == null).Single().FullUrl}.gif",
                                            BirthYearInLHijri = poet.BirthYearInLHijri,
                                            DeathYearInLHijri = poet.DeathYearInLHijri,
                                            ValidBirthDate = poet.ValidBirthDate,
                                            ValidDeathDate = poet.ValidDeathDate,
                                            PinOrder = poet.PinOrder,
                                            BirthPlace = poet.BirthLocation == null ? "" : poet.BirthLocation.Name,
                                            BirthPlaceLatitude = poet.BirthLocation == null ? 0 : poet.BirthLocation.Latitude,
                                            BirthPlaceLongitude = poet.BirthLocation == null ? 0 : poet.BirthLocation.Longitude,
                                            DeathPlace = poet.DeathLocation == null ? "" : poet.DeathLocation.Name,
                                            DeathPlaceLatitude = poet.DeathLocation == null ? 0 : poet.DeathLocation.Latitude,
                                            DeathPlaceLongitude = poet.DeathLocation == null ? 0 : poet.DeathLocation.Longitude,
                                        }).AsNoTracking().FirstOrDefaultAsync(),
                   Cat = catViewModel
               }
               );
        }

        /// <summary>
        /// get page url by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<string>> GetPageUrlById(int id)
        {
            var dbPage = await _context.DivanPages.Where(p => p.Id == id).AsNoTracking().SingleOrDefaultAsync();
            if (dbPage == null)
                return new RServiceResult<string>(null); //not found
            return new RServiceResult<string>(dbPage.FullUrl);
        }

        /// <summary>
        /// clean cache for paeg by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task CacheCleanForPageById(int id)
        {
            var dbPage = await _context.DivanPages.Where(p => p.Id == id).AsNoTracking().SingleOrDefaultAsync();
            if (dbPage != null)
            {
                CacheCleanForPageByUrl(dbPage.FullUrl);
            }
        }

        /// <summary>
        /// clean cache for page by url
        /// </summary>
        /// <param name="url"></param>
        public void CacheCleanForPageByUrl(string url)
        {
            var cachKey = $"DivanService::GetPageByUrl::{url}";
            if (_memoryCache.TryGetValue(cachKey, out DivanPageCompleteViewModel page))
            {
                _memoryCache.Remove(cachKey);

                var poemCachKey = $"GetPoemById({page.Id}, {true}, {false}, {true}, {true}, {true}, {true}, {true}, {true}, {true})";
                if (_memoryCache.TryGetValue(poemCachKey, out DivanPoemCompleteViewModel p))
                {
                    _memoryCache.Remove(poemCachKey);
                }
            }
        }

        /// <summary>
        /// clean cache for page by comment
        /// </summary>
        /// <param name="commentId"></param>
        /// <returns></returns>
        public async Task CacheCleanForComment(int commentId)
        {
            var comment = await _context.DivanComments.Where(c => c.Id == commentId).SingleOrDefaultAsync();
            if (comment != null)
            {
                await CacheCleanForPageById(comment.PoemId);
            }
        }

        /// <summary>
        /// get redirect url for a url
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        public async Task<RServiceResult<string>> GetRedirectAddressForPageUrl(string url)
        {
            if (url.IndexOf('?') != -1)
            {
                url = url.Substring(0, url.IndexOf('?'));
            }

            // /hafez/ => /hafez :
            if (url.LastIndexOf('/') == url.Length - 1)
            {
                url = url.Substring(0, url.Length - 1);
            }

            url = url.Replace("//", "/"); //duplicated slashes would be merged

            var pages = await _context.DivanPages.Where(p => url.StartsWith(p.RedirectFromFullUrl)).AsNoTracking().ToListAsync();
            if (pages.Count == 0)
                return new RServiceResult<string>(null); //not found

            var dbPage = pages[0];
            for (int i = 1; i < pages.Count; i++)
            {
                if (pages[i].RedirectFromFullUrl.Length > dbPage.RedirectFromFullUrl.Length)
                {
                    dbPage = pages[i];
                }
            }

            var target = dbPage.FullUrl;
            if (url != dbPage.RedirectFromFullUrl)
            {
                target = dbPage.FullUrl + url.Substring(dbPage.RedirectFromFullUrl.Length);
            }

            var dbPageRedirected = await _context.DivanPages.Where(p => p.FullUrl == target).AsNoTracking().SingleOrDefaultAsync();
            if (dbPageRedirected == null)
                return new RServiceResult<string>(null); //not found

            return new RServiceResult<string>(target);
        }

        /// <summary>
        /// get page by url
        /// </summary>
        /// <param name="url"></param>
        /// <param name="catPoems"></param>
        /// <param name="commentSortOrder"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanPageCompleteViewModel>> GetPageByUrl(string url, bool catPoems = false, DivanCommentSortOrder commentSortOrder = DivanCommentSortOrder.TopRated)
        {
            if (url.IndexOf('?') != -1)
            {
                url = url.Substring(0, url.IndexOf('?'));
            }

            // /hafez/ => /hafez :
            if (url.LastIndexOf('/') == url.Length - 1)
            {
                url = url.Substring(0, url.Length - 1);
            }

            url = url.Replace("//", "/"); //duplicated slashes would be merged

            var cachKey = $"DivanService::GetPageByUrl::{url}";
            // only the default (TopRated) sort order is cached, so a caller asking for a
            // different comment order never reads or overwrites the shared cache entry
            // used by everyone else
            if (commentSortOrder != DivanCommentSortOrder.TopRated || !_memoryCache.TryGetValue(cachKey, out DivanPageCompleteViewModel page))
            {
                var dbPage = await _context.DivanPages.Where(p => p.FullUrl == url).AsNoTracking().SingleOrDefaultAsync();
                if (dbPage == null)
                    return new RServiceResult<DivanPageCompleteViewModel>(null); //not found
                var secondPoet = dbPage.SecondPoetId == null ? null :
                     await
                     (from poet in _context.DivanPoets
                      join cat in _context.DivanCategories.Where(c => c.ParentId == null)
                      on poet.Id equals cat.PoetId
                      where poet.Id == (int)dbPage.SecondPoetId
                      orderby poet.Nickname descending
                      select new DivanPoetViewModel()
                      {
                          Id = poet.Id,
                          Name = poet.Name,
                          FullUrl = cat.FullUrl,
                          RootCatId = cat.Id,
                          Nickname = poet.Nickname,
                          Published = poet.Published,
                          ImageUrl = $"/api/divan/poet/image{cat.FullUrl}.gif",
                          BirthYearInLHijri = poet.BirthYearInLHijri,
                          ValidBirthDate = poet.ValidBirthDate,
                          ValidDeathDate = poet.ValidDeathDate,
                          DeathYearInLHijri = poet.DeathYearInLHijri,
                          PinOrder = poet.PinOrder,
                      }
                      )
                     .AsNoTracking().SingleAsync();
                page = new DivanPageCompleteViewModel()
                {
                    Id = dbPage.Id,
                    DivanPageType = dbPage.DivanPageType,
                    Title = dbPage.Title,
                    FullTitle = dbPage.FullTitle,
                    UrlSlug = dbPage.UrlSlug,
                    FullUrl = dbPage.FullUrl,
                    HtmlText = dbPage.HtmlText,
                    SecondPoet = secondPoet,
                    NoIndex = dbPage.NoIndex,
                    RedirectFromFullUrl = dbPage.RedirectFromFullUrl,

                };
                switch (page.DivanPageType)
                {
                    case DivanPageType.PoemPage:
                        {
                            var poemRes = await GetPoemById((int)dbPage.PoemId, commentSortOrder: commentSortOrder);
                            if (!string.IsNullOrEmpty(poemRes.ExceptionString))
                            {
                                return new RServiceResult<DivanPageCompleteViewModel>(null, poemRes.ExceptionString);
                            }
                            page.Poem = poemRes.Result;
                        }
                        break;

                    case DivanPageType.CatPage:
                        {
                            var catRes = await GetCatById((int)dbPage.CatId, catPoems, false, true);
                            if (!string.IsNullOrEmpty(catRes.ExceptionString))
                            {
                                return new RServiceResult<DivanPageCompleteViewModel>(null, catRes.ExceptionString);
                            }
                            page.PoetOrCat = catRes.Result;
                        }
                        break;
                    default:
                        {
                            if (dbPage.PoetId != null)
                            {
                                var poetRes = await GetPoetById((int)dbPage.PoetId, catPoems);
                                if (!string.IsNullOrEmpty(poetRes.ExceptionString))
                                {
                                    return new RServiceResult<DivanPageCompleteViewModel>(null, poetRes.ExceptionString);
                                }
                                page.PoetOrCat = poetRes.Result;

                                var pre = await _context.DivanPages.Where(p => p.DivanPageType == page.DivanPageType && p.ParentId == dbPage.ParentId && p.PoetId == dbPage.PoetId &&
                                    ((p.PageOrder < dbPage.PageOrder) || (p.PageOrder == dbPage.PageOrder && p.Id < dbPage.Id)))
                                    .OrderByDescending(p => p.PageOrder)
                                    .ThenByDescending(p => p.Id)
                                    .AsNoTracking()
                                    .FirstOrDefaultAsync();
                                if (pre != null)
                                {
                                    page.Previous = new DivanPageSummaryViewModel()
                                    {
                                        Id = pre.Id,
                                        Title = pre.Title,
                                        FullUrl = pre.FullUrl
                                    };
                                }

                                var next = await _context.DivanPages.Where(p => p.DivanPageType == page.DivanPageType && p.ParentId == dbPage.ParentId && p.PoetId == dbPage.PoetId &&
                                    ((p.PageOrder > dbPage.PageOrder) || (p.PageOrder == dbPage.PageOrder && p.Id > dbPage.Id)))
                                    .OrderBy(p => p.PageOrder)
                                    .ThenBy(p => p.Id)
                                    .AsNoTracking()
                                    .FirstOrDefaultAsync();
                                if (next != null)
                                {
                                    page.Next = new DivanPageSummaryViewModel()
                                    {
                                        Id = next.Id,
                                        Title = next.Title,
                                        FullUrl = next.FullUrl
                                    };
                                }
                            }
                        }
                        break;
                }
                if (AggressiveCacheEnabled)
                {
                    _memoryCache.Set(cachKey, page, TimeSpan.FromHours(1));
                }
            }

            return new RServiceResult<DivanPageCompleteViewModel>(page);
        }



        /// <summary>
        /// get poem recitations  (PlainText/HtmlText are intentionally empty)
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<PublicRecitationViewModel[]>> GetPoemRecitations(int id)
        {
            bool loadFromExternalServer = bool.Parse(Configuration.GetSection("ExternalFTPServer")["LoadFromExternalServer"]);
            var source =
                 from audio in _context.Recitations
                 join poem in _context.DivanPoems
                 on audio.DivanPostId equals poem.Id
                 where
                 audio.ReviewStatus == AudioReviewStatus.Approved
                 &&
                 poem.Id == id
                 &&
                 audio.AudioSyncStatus == AudioSyncStatus.SynchronizedOrRejected
                 orderby audio.RecitationType, audio.AudioOrder
                 select new PublicRecitationViewModel()
                 {
                     Id = audio.Id,
                     PoemId = audio.DivanPostId,
                     PoemFullTitle = poem.FullTitle,
                     PoemFullUrl = poem.FullUrl,
                     AudioTitle = audio.AudioTitle,
                     AudioArtist = audio.AudioArtist,
                     AudioArtistUrl = audio.AudioArtistUrl,
                     AudioSrc = audio.AudioSrc,
                     AudioSrcUrl = audio.AudioSrcUrl,
                     LegacyAudioGuid = audio.LegacyAudioGuid,
                     Mp3FileCheckSum = audio.Mp3FileCheckSum,
                     Mp3SizeInBytes = audio.Mp3SizeInBytes,
                     PublishDate = audio.ReviewDate,
                     FileLastUpdated = audio.FileLastUpdated,
                     Mp3Url = loadFromExternalServer ? audio.Mp3Url : $"{WebServiceUrl.Url}/api/audio/file/{audio.Id}.mp3",
                     XmlText = $"{WebServiceUrl.Url}/api/audio/xml/{audio.Id}",
                     PlainText = "", //poem.PlainText 
                     HtmlText = "",//poem.HtmlText
                     AudioOrder = audio.AudioOrder,
                     RecitationType = audio.RecitationType,
                     InSyncWithText = audio.InSyncWithText,
                     UpVotedByUser = false,
                 };
            var recitations = await source.AsNoTracking().ToArrayAsync();
            foreach (var recitation in recitations)
            {
                recitation.Mistakes =
                    await _context.RecitationApprovedMistakes.AsNoTracking()
                          .Where(m => m.RecitationId == recitation.Id)
                          .Select(m => new RecitationMistakeViewModel()
                          {
                              Id = m.Id,
                              Mistake = m.Mistake,
                              NumberOfLinesAffected = m.NumberOfLinesAffected,
                              CoupletIndex = m.CoupletIndex
                          }).ToArrayAsync();
            }
            return new RServiceResult<PublicRecitationViewModel[]>(recitations);
        }


        /// <summary>
        /// get poem whole sections
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanPoemSection[]>> GetPoemWholeSections(int id)
        {
            var sections =
                await _context.DivanPoemSections.AsNoTracking().Include(s => s.DivanMetre).Where(s => s.PoemId == id && s.SectionType == PoemSectionType.WholePoem).OrderBy(s => s.Index).ToArrayAsync();

            foreach (var section in sections)
            {
                section.Top6RelatedSections = (await GetRelatedSections(section.PoemId, section.Index, 0, 6)).Result;
            }

            return new RServiceResult<DivanPoemSection[]>(sections);
        }


        /// <summary>
        /// get user up votes for the recitations of a poem
        /// </summary>
        /// <param name="id"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<int[]>> GetUserPoemRecitationsUpVotes(int id, Guid userId)
        {
            var source =
                 from audio in _context.Recitations
                 join poem in _context.DivanPoems
                 on audio.DivanPostId equals poem.Id
                 where
                 audio.ReviewStatus == AudioReviewStatus.Approved
                 &&
                 poem.Id == id
                 orderby audio.AudioOrder
                 select audio.Id;

            List<int> upVotedRecitations = new List<int>();
            var recitationIds = await source.ToArrayAsync();
            foreach (var recitationId in recitationIds)
            {
                if (await _context.RecitationUserUpVotes.Where(v => v.RecitationId == recitationId && v.UserId == userId).AnyAsync())
                {
                    upVotedRecitations.Add(recitationId);
                }
            }

            return new RServiceResult<int[]>(upVotedRecitations.ToArray());
        }

        private async Task _FillPoemCoupletIndices(RMuseumDbContext context, int poemId)
        {
            var verses = await context.DivanVerses.Where(v => v.PoemId == poemId).OrderBy(v => v.VOrder).ToListAsync();
            int cIndex = -1;
            foreach (var verse in verses)
            {
                if (verse.VersePosition != VersePosition.Left && verse.VersePosition != VersePosition.CenteredVerse2 && verse.VersePosition != VersePosition.Comment)
                    cIndex++;
                if (verse.VersePosition != VersePosition.Comment)
                {
                    verse.CoupletIndex = cIndex;
                }
                else
                {
                    verse.CoupletIndex = null;
                }
            }
            context.DivanVerses.UpdateRange(verses);
        }


        /// <summary>
        /// get poem comments
        /// </summary>
        /// <param name="poemId"></param>
        /// <param name="userId"></param>
        /// <param name="coupletIndex"></param>
        /// <param name="sortOrder"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanCommentSummaryViewModel[]>> GetPoemComments(int poemId, Guid userId, int? coupletIndex, DivanCommentSortOrder sortOrder)
        {
            var query = _context.DivanComments
     .Include(c => c.User)
     .Where(comment =>
         (comment.Status == PublishStatus.Published ||
         (userId != Guid.Empty &&
          comment.Status == PublishStatus.Awaiting &&
          comment.UserId == userId))
         &&
         comment.PoemId == poemId
         &&
         (coupletIndex == null || comment.CoupletIndex == coupletIndex));

            query = sortOrder switch
            {
                DivanCommentSortOrder.TopRated => query.OrderByDescending(c => c.SortKey).ThenBy(c => c.CommentDate),
                DivanCommentSortOrder.Newest => query.OrderByDescending(c => c.CommentDate),
                _ => query.OrderBy(c => c.CommentDate), // Oldest
            };

            var source =
                from comment in query
                select new DivanCommentSummaryViewModel
                {
                    Id = comment.Id,
                    AuthorName = comment.User == null ? comment.AuthorName : comment.User.NickName,
                    AuthorUrl = comment.AuthorUrl,
                    CommentDate = comment.CommentDate,
                    HtmlComment = comment.HtmlComment,
                    PublishStatus = comment.Status == PublishStatus.Awaiting ? "در انتظار تأیید" : "",
                    InReplyToId = comment.InReplyToId,
                    UserId = comment.UserId,
                    CoupletIndex = comment.CoupletIndex ?? -1,
                    LikeCount = comment.LikeCount,
                    DislikeCount = comment.DislikeCount,
                    CurrentUserRatingValue = 0
                };

            DivanCommentSummaryViewModel[] allComments = await source.AsNoTracking().ToArrayAsync();

            foreach (DivanCommentSummaryViewModel comment in allComments)
            {
                comment.AuthorName = comment.AuthorName.ToPersianNumbers().ApplyCorrectYeKe();
                var relatedVerses = comment.CoupletIndex == -1 ? new List<DivanVerse>() : await _context.DivanVerses.Where(v => v.PoemId == poemId && v.CoupletIndex == comment.CoupletIndex).OrderBy(v => v.VOrder).ToListAsync();
                string coupleText = relatedVerses.Count == 0 ? "" : relatedVerses[0].Text;
                for (int nVerseIndex = 1; nVerseIndex < relatedVerses.Count; nVerseIndex++)
                {
                    coupleText += $" {relatedVerses[nVerseIndex].Text}";
                }
                comment.CoupletSummary = _CutSummary(coupleText);
            }

            DivanCommentSummaryViewModel[] rootComments = allComments.Where(c => c.InReplyToId == null).ToArray();

            foreach (DivanCommentSummaryViewModel comment in rootComments)
            {
                _FindReplies(comment, allComments);
            }
            return new RServiceResult<DivanCommentSummaryViewModel[]>(rootComments);
        }

        private void _FindReplies(DivanCommentSummaryViewModel comment, DivanCommentSummaryViewModel[] allComments)
        {
            comment.Replies = allComments.Where(c => c.InReplyToId == comment.Id).ToArray();
            foreach (DivanCommentSummaryViewModel reply in comment.Replies)
            {
                _FindReplies(reply, allComments);
            }
        }

        /// <summary>
        /// get a single comment information (replies are not included)
        /// </summary>
        /// <param name="commentId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanCommentSummaryViewModel>> GetCommentByIdAsync(int commentId)
        {
            var source =
                  from c in _context.DivanComments.Include(c => c.User)
                  where
                  c.Status == PublishStatus.Published
                  &&
                  c.Id == commentId
                  select new DivanCommentSummaryViewModel()
                  {
                      Id = c.Id,
                      AuthorName = c.User == null ? c.AuthorName : $"{c.User.NickName}",
                      AuthorUrl = c.AuthorUrl,
                      CommentDate = c.CommentDate,
                      HtmlComment = c.HtmlComment,
                      PublishStatus = c.Status == PublishStatus.Awaiting ? "در انتظار تأیید" : "",
                      InReplyToId = c.InReplyToId,
                      UserId = c.UserId,
                      CoupletIndex = c.CoupletIndex == null ? -1 : (int)c.CoupletIndex,
                      LikeCount = c.LikeCount,
                      DislikeCount = c.DislikeCount,
                      CurrentUserRatingValue = 0,
                  };

            DivanCommentSummaryViewModel comment = await source.AsNoTracking().SingleOrDefaultAsync();
            if (comment == null)
            {
                return new RServiceResult<DivanCommentSummaryViewModel>(null);
            }

            comment.AuthorName = comment.AuthorName.ToPersianNumbers().ApplyCorrectYeKe();

            var dbComment = await _context.DivanComments.AsNoTracking().Where(c => c.Id == commentId).SingleAsync();

            var relatedVerses = comment.CoupletIndex == -1 ? new List<DivanVerse>() : await _context.DivanVerses.Where(v => v.PoemId == dbComment.PoemId && v.CoupletIndex == comment.CoupletIndex).OrderBy(v => v.VOrder).ToListAsync();
            string coupleText = relatedVerses.Count == 0 ? "" : relatedVerses[0].Text;
            for (int nVerseIndex = 1; nVerseIndex < relatedVerses.Count; nVerseIndex++)
            {
                coupleText += $" {relatedVerses[nVerseIndex].Text}";
            }
            comment.CoupletSummary = _CutSummary(coupleText);

            return new RServiceResult<DivanCommentSummaryViewModel>(comment);
        }

        /// <summary>
        /// new comment
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="ip"></param>
        /// <param name="poemId"></param>
        /// <param name="content"></param>
        /// <param name="inReplyTo"></param>
        /// <param name="coupletIndex"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanCommentSummaryViewModel>> NewComment(Guid userId, string ip, int poemId, string content, int? inReplyTo, int? coupletIndex)
        {
            if (string.IsNullOrWhiteSpace(content))
            {
                return new RServiceResult<DivanCommentSummaryViewModel>(null, "متن حاشیه خالی است.");
            }

            var userRes = await _appUserService.GetUserInformation(userId);

            if (string.IsNullOrEmpty(userRes.Result.NickName))
            {
                return new RServiceResult<DivanCommentSummaryViewModel>(null, "لطفاً با مراجعه به پیشخان کاربری (دکمهٔ گوشهٔ پایین سمت چپ) «نام مستعار» خود را مشخص کنید و سپس اقدام به ارسال حاشیه بفرمایید.");
            }

            string coupletSummary = "";
            if (inReplyTo != null)
            {
                DivanComment refComment = await _context.DivanComments.Where(c => c.Id == (int)inReplyTo).SingleAsync();
                coupletIndex = refComment.CoupletIndex;
                if (refComment.CoupletIndex != null)
                {
                    var relatedVerses = refComment.CoupletIndex == -1 ? new List<DivanVerse>() : await _context.DivanVerses.Where(v => v.PoemId == poemId && v.CoupletIndex == refComment.CoupletIndex).OrderBy(v => v.VOrder).ToListAsync();
                    coupletSummary = relatedVerses.Count > 0 ? relatedVerses[0].Text : "";
                    if (relatedVerses.Count > 1)
                    {
                        coupletSummary += $" {relatedVerses[1].Text}";
                    }
                }
            }
            else
            if (coupletIndex != null)
            {
                var relatedVerses = await _context.DivanVerses.Where(v => v.PoemId == poemId && v.CoupletIndex == coupletIndex).OrderBy(v => v.VOrder).ToListAsync();
                coupletSummary = relatedVerses.Count > 0 ? relatedVerses[0].Text : "";
                if (relatedVerses.Count > 1)
                {
                    coupletSummary += $" {relatedVerses[1].Text}";
                }
            }

            content = content.ApplyCorrectYeKe();

            var processedComment = await _ProcessCommentHtml(content, _context);
            if (processedComment.TextWasDropped)
            {
                return new RServiceResult<DivanCommentSummaryViewModel>(null, _BuildSanitizerDroppedTextError(processedComment.RemainingPlainText));
            }
            content = processedComment.Html;

            string commentText = System.Net.WebUtility.HtmlDecode(Regex.Replace(content, "<.*?>", string.Empty));

            if (string.IsNullOrWhiteSpace(commentText))
            {
                return new RServiceResult<DivanCommentSummaryViewModel>(null, "متن حاشیه خالی است.");
            }

            int maxWordLengthInComments = int.Parse(Configuration.GetSection("Divan")["MaxWordLengthInComments"]);

            if (commentText.Split(" ", StringSplitOptions.RemoveEmptyEntries).Max(s => s.Length) > maxWordLengthInComments)
            {
                return new RServiceResult<DivanCommentSummaryViewModel>(null, "متن حاشیه شامل کلمات به هم پیوستهٔ طولانی است.");
            }

            PublishStatus status = PublishStatus.Published;
            var keepFirstTimeUsersComments = await _optionsService.GetValueAsync("KeepFirstTimeUsersComments", null, null);
            if (keepFirstTimeUsersComments.Result == true.ToString())
            {
                if ((await _context.DivanComments.AsNoTracking().Where(c => c.UserId == userId && c.Status == PublishStatus.Published).AnyAsync()) == false)//First time commenter
                {
                    status = PublishStatus.Awaiting;
                }
            }

            DivanComment comment = new DivanComment()
            {
                UserId = userId,
                AuthorIpAddress = ip,
                CommentDate = DateTime.Now,
                HtmlComment = content,
                InReplyToId = inReplyTo,
                PoemId = poemId,
                Status = status,
                CoupletIndex = coupletIndex,
            };

            _context.DivanComments.Add(comment);
            await _context.SaveChangesAsync();

            if (inReplyTo != null)
            {
                DivanComment refComment = await _context.DivanComments.Where(c => c.Id == (int)inReplyTo).SingleAsync();
                if (refComment.UserId != null)
                {

                    var poem = await _context.DivanPoems.Where(p => p.Id == comment.PoemId).SingleAsync();

                    await _notificationService.PushNotification((Guid)refComment.UserId,
                                       "پاسخ به حاشیهٔ شما",
                                       $"{userRes.Result.NickName} برای حاشیهٔ شما روی <a href=\"https://ganjoor.net{poem.FullUrl}\">{poem.FullTitle}</a> این پاسخ را نوشته است: {Environment.NewLine}" +
                                       $"{content}" +
                                       $"این متن حاشیهٔ خود شماست: {Environment.NewLine}" +
                                       $"{refComment.HtmlComment}",
                                       NotificationType.ActionRequired
                                       );
                }
            }

            await CacheCleanForPageById(poemId);

            if(comment.Status == PublishStatus.Awaiting)
            {
                var moderators = await _appUserService.GetUsersHavingPermission(RMuseumSecurableItem.DivanEntityShortName, RMuseumSecurableItem.ModerateOperationShortName);
                if (string.IsNullOrEmpty(moderators.ExceptionString)) //if not, do nothing!
                {
                    foreach (var moderator in moderators.Result)
                    {
                        await _notificationService.PushNotification
                                        (
                                            (Guid)moderator.Id,
                                            "حاشیه در انتظار تأیید",
                                            $"حاشیه‌ای در انتظار تأیید است. لطفاً <a href=\"https://ganjoor.net/User/AwaitingComments\">حاشیه‌های در انتظار تأیید</a> را بررسی فرمایید.{Environment.NewLine}" +
                                            $"توجه فرمایید که اگر کاربر دیگری که دارای مجوز بررسی حاشیه‌هاست پیش از شما به آن رسیدگی کرده باشد آن را در صف نخواهید دید.",
                                            NotificationType.ActionRequired
                                        );
                    }
                }
            }


            return new RServiceResult<DivanCommentSummaryViewModel>
                (
                new DivanCommentSummaryViewModel()
                {
                    Id = comment.Id,
                    AuthorName = $"{userRes.Result.NickName}",
                    AuthorUrl = comment.AuthorUrl,
                    CommentDate = comment.CommentDate,
                    HtmlComment = comment.HtmlComment,
                    PublishStatus = comment.Status == PublishStatus.Awaiting ? "در انتظار تأیید" : "",
                    InReplyToId = comment.InReplyToId,
                    UserId = comment.UserId,
                    Replies = Array.Empty<DivanCommentSummaryViewModel>(),
                    CoupletIndex = coupletIndex == null ? -1 : (int)coupletIndex,
                    MyComment = true,
                    CoupletSummary = _CutSummary(coupletSummary),
                    LikeCount = comment.LikeCount,
                    DislikeCount = comment.DislikeCount,
                    CurrentUserRatingValue = 0,
                }
                );
        }

        private string _CutSummary(string summary)
        {
            if (summary.Length > 50)
            {
                summary = summary.Substring(0, 30);
                int n = summary.LastIndexOf(' ');
                if (n >= 0)
                {
                    summary = summary.Substring(0, n) + " ...";
                }
                else
                {
                    summary += "...";
                }
            }
            return summary;
        }

        /// <summary>
        /// update user's own comment
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="commentId"></param>
        /// <param name="htmlComment"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> EditMyComment(Guid userId, int commentId, string htmlComment)
        {
            DivanComment comment = await _context.DivanComments.Where(c => c.Id == commentId && c.UserId == userId).SingleOrDefaultAsync();//userId is not part of key but it helps making call secure
            if (comment == null)
            {
                return new RServiceResult<bool>(false); //not found
            }

            htmlComment = htmlComment.ApplyCorrectYeKe();

            var processedComment = await _ProcessCommentHtml(htmlComment, _context);
            if (processedComment.TextWasDropped)
            {
                return new RServiceResult<bool>(false, _BuildSanitizerDroppedTextError(processedComment.RemainingPlainText));
            }
            htmlComment = processedComment.Html;

            string commentText = System.Net.WebUtility.HtmlDecode(Regex.Replace(htmlComment, "<.*?>", string.Empty));

            if (string.IsNullOrWhiteSpace(commentText))
            {
                return new RServiceResult<bool>(false, "متن حاشیه خالی است.");
            }

            await CacheCleanForComment(commentId);

            comment.HtmlComment = htmlComment;

            _context.DivanComments.Update(comment);
            await _context.SaveChangesAsync();

            return new RServiceResult<bool>(true);
        }

        /// <summary>
        /// link or unlink user's own comment to a coupletIndex
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="commentId"></param>
        /// <param name="coupletIndex">if null then unlinks</param>
        /// <returns>couplet summary</returns>
        public async Task<RServiceResult<string>> LinkUnLinkMyComment(Guid userId, int commentId, int? coupletIndex)
        {
            var userRes = await _appUserService.GetUserInformation(userId);

            if (string.IsNullOrEmpty(userRes.Result.NickName))
            {
                return new RServiceResult<string>(null, "لطفاً با مراجعه به پیشخان کاربری (دکمهٔ گوشهٔ پایین سمت چپ) «نام مستعار» خود را مشخص کنید و سپس اقدام به ارسال حاشیه بفرمایید.");
            }

            DivanComment comment = await _context.DivanComments.Where(c => c.Id == commentId && c.UserId == userId).SingleOrDefaultAsync();//userId is not part of key but it helps making call secure
            if (comment == null)
            {
                return new RServiceResult<string>(null); //not found
            }

            await CacheCleanForComment(commentId);



            comment.CoupletIndex = coupletIndex;

            string coupletSummary = "";
            if (coupletIndex != null)
            {
                var relatedVerses = await _context.DivanVerses.Where(v => v.PoemId == comment.PoemId && v.CoupletIndex == coupletIndex).OrderBy(v => v.VOrder).ToListAsync();
                coupletSummary = relatedVerses.Count > 0 ? relatedVerses[0].Text : "";
                if (relatedVerses.Count > 1)
                {
                    coupletSummary += $" {relatedVerses[1].Text}";
                }
            }

            _context.DivanComments.Update(comment);
            await _context.SaveChangesAsync();

            return new RServiceResult<string>
                (
               coupletSummary
                );
        }

        /// <summary>
        /// delete a reported  comment
        /// </summary>
        /// <param name="repordId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> DeleteModerateComment(int repordId)
        {
            DivanCommentAbuseReport report = await _context.DivanReportedComments.Where(r => r.Id == repordId).SingleOrDefaultAsync();
            if (report == null)
            {
                return new RServiceResult<bool>(false);
            }

            var reportUserId = report.ReportedById;


            DivanComment comment = await _context.DivanComments.Where(c => c.Id == report.DivanCommentId).SingleOrDefaultAsync();
            if (comment == null)
            {
                return new RServiceResult<bool>(false); //not found
            }

            var commentId = report.DivanCommentId;
            string commentHtmltext = comment.HtmlComment;


            if (comment.UserId != null)
            {
                string reason = "";
                switch (report.ReasonCode)
                {
                    case "offensive":
                        reason = "توهین‌آمیز است.";
                        break;
                    case "religious":
                        reason = "بحث مذهبی کرده.";
                        break;
                    case "repeated":
                        reason = "تکراری است.";
                        break;
                    case "unrelated":
                        reason = "به این شعر ربطی ندارد.";
                        break;
                    case "brokenlink":
                        reason = "لینک شکسته است.";
                        break;
                    case "ad":
                        reason = "تبلیغاتی است.";
                        break;
                    case "bogus":
                        reason = "نامفهوم است.";
                        break;
                    case "latin":
                        reason = "فارسی ننوشته.";
                        break;
                    case "other":
                        reason = "دلیل دیگر";
                        break;
                }
                if (!string.IsNullOrEmpty(report.ReasonText))
                    reason += $" {report.ReasonText}";
                reason = reason.Trim();
                reason = string.IsNullOrEmpty(reason) ? "" : $"علت ارائه شده برای حذف یا متن گزارش کاربر شاکی: {Environment.NewLine}" +
                                       $"{reason} {Environment.NewLine}";
                await _notificationService.PushNotification((Guid)comment.UserId,
                                       "حذف حاشیهٔ شما",
                                       $"حاشیهٔ شما به دلیل ناسازگاری با قوانین حاشیه‌گذاری گنجور و طبق گزارشات دیگر کاربران حذف شده است.{Environment.NewLine}" +
                                       $"{reason}{Environment.NewLine}" +
                                       $"<a href=\"https://ganjoor.net?p={comment.PoemId}\">نشانی صفحهٔ متناظر در گنجور</a>{Environment.NewLine}" +
                                       $"این متن حاشیهٔ حذف شدهٔ شماست: {Environment.NewLine}" +
                                       $"{comment.HtmlComment}",
                                       NotificationType.Warning
                                       );
            }

            //if user has got replies, delete them and notify their owners of what happened
            var replies = await _FindReplies(comment);
            for (int i = replies.Count - 1; i >= 0; i--)
            {
                if (replies[i].UserId != null)
                {
                    await _notificationService.PushNotification((Guid)replies[i].UserId,
                                           "حذف پاسخ شما به حاشیه",
                                           $"پاسخ شما به یکی از حاشیه‌های گنجور به دلیل حذف زنجیرهٔ حاشیه توسط یکی از حاشیه‌گذاران حذف شده است.{Environment.NewLine}" +
                                           $"این متن حاشیهٔ حذف شدهٔ شماست: {Environment.NewLine}" +
                                           $"{replies[i].HtmlComment}",
                                           NotificationType.Warning
                                           );
                }
                _context.DivanComments.Remove(replies[i]);
            }

            _context.DivanComments.Remove(comment);
            await _context.SaveChangesAsync();

            await CacheCleanForComment(report.DivanCommentId);

            if (reportUserId != null)
            {
                await _notificationService.PushNotification((Guid)reportUserId, "حذف حاشیهٔ گزارش شده توسط شما",
                    $"گزارش شما برای حاشیه‌ای با متن ذیل پذیرفته و حاشیه حذف شد. متن حاشیهٔ گزارش شده توسط شما:{Environment.NewLine}" +
                    commentHtmltext
                    );
            }

            return new RServiceResult<bool>(true);
        }

        /// <summary>
        /// publish awaiting comment
        /// </summary>
        /// <param name="commentId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> PublishAwaitingComment(int commentId)
        {
            DivanComment comment = await _context.DivanComments.Where(c => c.Id == commentId).SingleOrDefaultAsync();//userId is not part of key but it helps making call secure
            if (comment == null)
            {
                return new RServiceResult<bool>(false); //not found
            }
            comment.Status = PublishStatus.Published;
            _context.Update(comment);
            await _context.SaveChangesAsync();
            return new RServiceResult<bool>(true);

        }

        /// <summary>
        /// delete anybody's comment
        /// </summary>
        /// <param name="commentId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> DeleteAnybodyComment(int commentId)
        {
            DivanComment comment = await _context.DivanComments.AsNoTracking().Where(c => c.Id == commentId).SingleOrDefaultAsync();//userId is not part of key but it helps making call secure
            if (comment == null)
            {
                return new RServiceResult<bool>(false); //not found
            }

            return await DeleteMyComment((Guid)comment.UserId, commentId);
        }


        /// <summary>
        /// delete user own comment
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="commentId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> DeleteMyComment(Guid userId, int commentId)
        {
            DivanComment comment = await _context.DivanComments.Where(c => c.Id == commentId && c.UserId == userId).SingleOrDefaultAsync();//userId is not part of key but it helps making call secure
            if (comment == null)
            {
                return new RServiceResult<bool>(false); //not found
            }

            await CacheCleanForComment(commentId);

            //if user has got replies, delete them and notify their owners of what happened
            var replies = await _FindReplies(comment);
            for (int i = replies.Count - 1; i >= 0; i--)
            {
                if (replies[i].UserId != null && replies[i].UserId != userId)
                {
                    await _notificationService.PushNotification((Guid)replies[i].UserId,
                                           "حذف پاسخ شما به حاشیه",
                                           $"پاسخ شما به یکی از حاشیه‌های گنجور به دلیل حذف زنجیرهٔ حاشیه توسط یکی از حاشیه‌گذاران حذف شده است.{Environment.NewLine}" +
                                           $"<a href=\"https://ganjoor.net?p={comment.PoemId}\">نشانی صفحهٔ متناظر در گنجور</a>{Environment.NewLine}" +
                                           $"این متن حاشیهٔ حذف شدهٔ شماست: {Environment.NewLine}" +
                                           $"{replies[i].HtmlComment}",
                                           NotificationType.Warning
                                           );
                }
                _context.DivanComments.Remove(replies[i]);
            }

            _context.DivanComments.Remove(comment);
            await _context.SaveChangesAsync();

            return new RServiceResult<bool>(true);
        }

        private async Task<List<DivanComment>> _FindReplies(DivanComment comment)
        {
            List<DivanComment> replies = await _context.DivanComments.Where(c => c.InReplyToId == comment.Id).AsNoTracking().ToListAsync();
            List<DivanComment> replyToReplies = new List<DivanComment>();
            foreach (DivanComment reply in replies)
            {
                replyToReplies.AddRange(await _FindReplies(reply));
            }
            if (replyToReplies.Count > 0)
            {
                replies.AddRange(replyToReplies);
            }
            return replies;
        }


        /// <summary>
        /// get recent comments
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="filterUserId"></param>
        /// <param name="onlyPublished"></param>
        /// <param name="onlyAwaiting"></param>
        /// <param name="term"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, DivanCommentFullViewModel[] Items)>> GetRecentComments(PagingParameterModel paging, Guid filterUserId, bool onlyPublished, bool onlyAwaiting = false, string term = null)
        {
            string[] searchPatterns = LanguageUtils.SearchLikePatterns(term); // divan: LIKE instead of full-text; empty term -> no filter

            var comments = _context.DivanComments.AsQueryable();
            foreach (var pattern in searchPatterns)
                comments = comments.Where(c => EF.Functions.Like(c.HtmlComment, pattern));
            var source =
                 from comment in comments.Include(c => c.Poem).Include(c => c.User).Include(c => c.InReplyTo).ThenInclude(r => r.User)
                 where
                  ((comment.Status == PublishStatus.Published) || !onlyPublished)
                  &&
                  ((comment.Status == PublishStatus.Awaiting) || !onlyAwaiting)
                 &&
                 ((filterUserId == Guid.Empty) || (filterUserId != Guid.Empty && comment.UserId == filterUserId))
                 orderby comment.CommentDate descending
                 select new DivanCommentFullViewModel()
                 {
                     Id = comment.Id,
                     AuthorName = comment.User == null ? comment.AuthorName : $"{comment.User.NickName}",
                     AuthorUrl = comment.AuthorUrl,
                     CommentDate = comment.CommentDate,
                     HtmlComment = comment.HtmlComment,
                     PublishStatus = "",//invalid!
                     UserId = comment.UserId,
                     CoupletIndex = comment.CoupletIndex == null ? -1 : (int)comment.CoupletIndex,
                     InReplyTo = comment.InReplyTo == null ? null :
                        new DivanCommentSummaryViewModel()
                        {
                            Id = comment.InReplyTo.Id,
                            AuthorName = comment.InReplyTo.User == null ? comment.InReplyTo.AuthorName : $"{comment.InReplyTo.User.NickName}",
                            AuthorUrl = comment.InReplyTo.AuthorUrl,
                            CommentDate = comment.InReplyTo.CommentDate,
                            HtmlComment = comment.InReplyTo.HtmlComment,
                            PublishStatus = "",
                            UserId = comment.InReplyTo.UserId,
                            CoupletIndex = comment.InReplyTo.CoupletIndex == null ? -1 : (int)comment.InReplyTo.CoupletIndex,
                            LikeCount = comment.InReplyTo.LikeCount,
                            DislikeCount = comment.InReplyTo.DislikeCount,
                            CurrentUserRatingValue = 0,
                        },
                     Poem = new DivanPoemSummaryViewModel()
                     {
                         Id = comment.Poem.Id,
                         Title = comment.Poem.FullTitle,
                         UrlSlug = comment.Poem.FullUrl,
                         Excerpt = ""
                     },
                     LikeCount = comment.LikeCount,
                     DislikeCount = comment.DislikeCount,
                     CurrentUserRatingValue = 0,
                 };

            (PaginationMetadata PagingMeta, DivanCommentFullViewModel[] Items) paginatedResult =
                await QueryablePaginator<DivanCommentFullViewModel>.Paginate(source, paging);


            foreach (DivanCommentFullViewModel comment in paginatedResult.Items)
            {
                comment.AuthorName = comment.AuthorName.ToPersianNumbers().ApplyCorrectYeKe();
                var relatedVerses = comment.CoupletIndex == -1 ? new List<DivanVerse>() : await _context.DivanVerses.Where(v => v.PoemId == comment.Poem.Id && v.CoupletIndex == comment.CoupletIndex).OrderBy(v => v.VOrder).ToListAsync();
                string coupleText = relatedVerses.Count == 0 ? "" : relatedVerses[0].Text;
                for (int nVerseIndex = 1; nVerseIndex < relatedVerses.Count; nVerseIndex++)
                {
                    coupleText += $" {relatedVerses[nVerseIndex].Text}";
                }


                comment.CoupletSummary = _CutSummary(coupleText);
                if (comment.InReplyTo != null)
                {

                    var replyRelatedVerses = comment.InReplyTo.CoupletIndex == -1 ? new List<DivanVerse>() : await _context.DivanVerses.Where(v => v.PoemId == comment.Poem.Id && v.CoupletIndex == comment.InReplyTo.CoupletIndex).OrderBy(v => v.VOrder).ToListAsync();
                    string replyCoupleText = relatedVerses.Count == 0 ? "" : relatedVerses[0].Text;
                    for (int nVerseIndex = 1; nVerseIndex < replyRelatedVerses.Count; nVerseIndex++)
                    {
                        replyCoupleText += $" {replyRelatedVerses[nVerseIndex].Text}";
                    }
                    comment.InReplyTo.CoupletSummary = _CutSummary(replyCoupleText);
                }
            }

            return new RServiceResult<(PaginationMetadata PagingMeta, DivanCommentFullViewModel[] Items)>(paginatedResult);
        }

        /// <summary>
        /// report a comment
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="report"></param>
        /// <returns>id of report record</returns>
        public async Task<RServiceResult<int>> ReportComment(Guid userId, DivanPostReportCommentViewModel report)
        {
            DivanCommentAbuseReport r = new DivanCommentAbuseReport()
            {
                DivanCommentId = report.CommentId,
                ReportedById = userId,
                ReasonCode = report.ReasonCode,
                ReasonText = report.ReasonText,
            };
            _context.DivanReportedComments.Add(r);
            await _context.SaveChangesAsync();
            var moderators = await _appUserService.GetUsersHavingPermission(RMuseumSecurableItem.DivanEntityShortName, RMuseumSecurableItem.ModerateOperationShortName);
            if (string.IsNullOrEmpty(moderators.ExceptionString)) //if not, do nothing!
            {
                foreach (var moderator in moderators.Result)
                {
                    await _notificationService.PushNotification
                                    (
                                        (Guid)moderator.Id,
                                        "گزارش حاشیه",
                                        $"گزارشی برای یک حاشیه ثبت شده است. لطفاً بخش <a href=\"https://ganjoor.net/User/ReportedComments\">حاشیه‌های گزارش شده</a> را بررسی فرمایید.{Environment.NewLine}" +
                                        $"توجه فرمایید که اگر کاربر دیگری که دارای مجوز بررسی حاشیه‌هاست پیش از شما به آن رسیدگی کرده باشد آن را در صف نخواهید دید.",
                                        NotificationType.ActionRequired
                                    );
                }
            }
            return new RServiceResult<int>(r.DivanCommentId);
        }

        /// <summary>
        /// delete a report
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> DeleteReport(int id)
        {
            DivanCommentAbuseReport report = await _context.DivanReportedComments.Where(r => r.Id == id).SingleOrDefaultAsync();
            if (report == null)
            {
                return new RServiceResult<bool>(false);
            }
            _context.DivanReportedComments.Remove(report);
            await _context.SaveChangesAsync();
            return new RServiceResult<bool>(true);
        }

        /// <summary>
        /// Get list of reported comments
        /// </summary>
        /// <param name="paging"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, DivanCommentAbuseReportViewModel[] Items)>> GetReportedComments(PagingParameterModel paging)
        {
            var source =
                 from report in _context.DivanReportedComments
                 join comment in _context.DivanComments.Include(c => c.Poem).Include(c => c.User).Include(c => c.InReplyTo).ThenInclude(r => r.User)
                 on report.DivanCommentId equals comment.Id
                 orderby report.Id descending
                 select
                 new DivanCommentAbuseReportViewModel()
                 {
                     Id = report.Id,
                     ReasonCode = report.ReasonCode,
                     ReasonText = report.ReasonText,
                     Comment = new DivanCommentFullViewModel()
                     {
                         Id = comment.Id,
                         AuthorName = comment.User == null ? comment.AuthorName : $"{comment.User.NickName}",
                         AuthorUrl = comment.AuthorUrl,
                         CommentDate = comment.CommentDate,
                         HtmlComment = comment.HtmlComment,
                         PublishStatus = "",//invalid!
                         UserId = comment.UserId,
                         InReplyTo = comment.InReplyTo == null ? null :
                        new DivanCommentSummaryViewModel()
                        {
                            Id = comment.InReplyTo.Id,
                            AuthorName = comment.InReplyTo.User == null ? comment.InReplyTo.AuthorName : $"{comment.InReplyTo.User.NickName}",
                            AuthorUrl = comment.InReplyTo.AuthorUrl,
                            CommentDate = comment.InReplyTo.CommentDate,
                            HtmlComment = comment.InReplyTo.HtmlComment,
                            PublishStatus = "",
                            UserId = comment.InReplyTo.UserId,
                            LikeCount = comment.LikeCount,
                            DislikeCount = comment.DislikeCount,
                            CurrentUserRatingValue = 0,
                        },
                         Poem = new DivanPoemSummaryViewModel()
                         {
                             Id = comment.Poem.Id,
                             Title = comment.Poem.FullTitle,
                             UrlSlug = comment.Poem.FullUrl,
                             Excerpt = ""
                         }
                     }
                 };

            (PaginationMetadata PagingMeta, DivanCommentAbuseReportViewModel[] Items) paginatedResult =
                await QueryablePaginator<DivanCommentAbuseReportViewModel>.Paginate(source, paging);


            foreach (DivanCommentAbuseReportViewModel report in paginatedResult.Items)
            {
                report.Comment.AuthorName = report.Comment.AuthorName.ToPersianNumbers().ApplyCorrectYeKe();
            }

            return new RServiceResult<(PaginationMetadata PagingMeta, DivanCommentAbuseReportViewModel[] Items)>(paginatedResult);
        }


        /// <summary>
        /// get poem images by id (some fields are intentionally field with blank or null),
        /// EntityImageId : the most important data field, image url is {WebServiceUrl.Url}/api/images/thumb/{EntityImageId}.jpg or {WebServiceUrl.Url}/api/images/norm/{EntityImageId}.jpg
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<PoemRelatedImage[]>> GetPoemImages(int id)
        {
            var museumSrc =
                 from link in _context.DivanLinks.Include(l => l.Artifact).Include(l => l.Item).ThenInclude(i => i.Images)
                 join poem in _context.DivanPoems
                 on link.DivanPostId equals poem.Id
                 where
                 link.DisplayOnPage == true
                 &&
                 link.ReviewResult == ReviewResult.Approved
                 &&
                 poem.Id == id
                 orderby link.IsTextOriginalSource descending, link.ReviewDate
                 select new PoemRelatedImage()
                 {
                     Id = link.Id,
                     PoemRelatedImageType = PoemRelatedImageType.MuseumLink,
                     ThumbnailImageUrl = link.Item.Images.First().ExternalNormalSizeImageUrl.Replace("/norm/", "/thumb/").Replace("/orig/", "/thumb/"),
                     TargetPageUrl = link.LinkToOriginalSource ? link.OriginalSourceUrl : $"https://museum.ganjoor.net/items/{link.Artifact.FriendlyUrl}/{link.Item.FriendlyUrl}",
                     AltText = $"{link.Artifact.Name} » {link.Item.Name}",
                     IsTextOriginalSource = link.IsTextOriginalSource
                 };
            List<PoemRelatedImage> museumImages = await museumSrc.ToListAsync();

            var externalSrc =
                 from link in _context.PinterestLinks
                 join poem in _context.DivanPoems
                 on link.DivanPostId equals poem.Id
                 where
                 link.ReviewResult == ReviewResult.Approved
                 &&
                 poem.Id == id
                 orderby link.ReviewDate
                 select new PoemRelatedImage()
                 {
                     Id = link.Id,
                     PoemRelatedImageType = PoemRelatedImageType.ExternalLink,
                     ThumbnailImageUrl = link.LinkType == LinkType.Naskban ? link.PinterestImageUrl : link.Item.Images.First().ExternalNormalSizeImageUrl.Replace("/norm/", "/thumb/").Replace("/orig/", "/thumb/"),
                     TargetPageUrl = link.PinterestUrl,
                     AltText = link.AltText,
                     IsTextOriginalSource = link.IsTextOriginalSource
                 };

            museumImages.AddRange(await externalSrc.AsNoTracking().ToListAsync());

            for (int i = 0; i < museumImages.Count; i++)
            {
                museumImages[i].ImageOrder = 0;
            }
            return new RServiceResult<PoemRelatedImage[]>(museumImages.ToArray());
        }

        /// <summary>
        /// Get Poem By Url
        /// </summary>
        /// <param name="url"></param>
        /// <param name="catInfo"></param>
        /// <param name="catPoems"></param>
        /// <param name="rhymes"></param>
        /// <param name="recitations"></param>
        /// <param name="images"></param>
        /// <param name="songs"></param>
        /// <param name="comments"></param>
        /// <param name="verseDetails"></param>
        /// <param name="navigation"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanPoemCompleteViewModel>> GetPoemByUrl(string url, bool catInfo = true, bool catPoems = false, bool rhymes = true, bool recitations = true, bool images = true, bool songs = true, bool comments = true, bool verseDetails = true, bool navigation = true)
        {
            // /hafez/ => /hafez :
            if (url.LastIndexOf('/') == url.Length - 1)
            {
                url = url.Substring(0, url.Length - 1);
            }
            var poem = await _context.DivanPoems.Where(p => p.FullUrl == url).SingleOrDefaultAsync();
            if (poem == null)
            {
                return new RServiceResult<DivanPoemCompleteViewModel>(null); //not found
            }
            return await GetPoemById(poem.Id, catInfo, catPoems, rhymes, recitations, images, songs, comments, verseDetails, navigation);
        }

        /// <summary>
        /// get poem verses
        /// </summary>
        /// <param name="id"></param>
        /// <param name="coupletIndex"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanVerseViewModel[]>> GetPoemVersesAsync(int id, int coupletIndex)
        {
            try
            {
                return new RServiceResult<DivanVerseViewModel[]>(await _context.DivanVerses
                                                    .Where(v => v.PoemId == id && (coupletIndex == -1 || v.CoupletIndex == coupletIndex))
                                                    .OrderBy(v => v.VOrder)
                                                    .Select
                                                    (
                                                        v => new DivanVerseViewModel()
                                                        {
                                                            Id = v.Id,
                                                            VOrder = v.VOrder,
                                                            CoupletIndex = v.CoupletIndex,
                                                            VersePosition = v.VersePosition,
                                                            Text = v.Text,
                                                            SectionIndex1 = v.SectionIndex1,
                                                            SectionIndex2 = v.SectionIndex2,
                                                            SectionIndex3 = v.SectionIndex3,
                                                            SectionIndex4 = v.SectionIndex4,
                                                            LanguageId = v.LanguageId,
                                                            CoupletSummary = v.CoupletSummary,
                                                        }
                                                    ).AsNoTracking().ToArrayAsync());
            }
            catch (Exception exp)
            {
                return new RServiceResult<DivanVerseViewModel[]>(null, exp.ToString());
            }
        }

        /// <summary>
        /// Get Poem By Id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="catInfo"></param>
        /// <param name="catPoems"></param>
        /// <param name="rhymes"></param>
        /// <param name="recitations"></param>
        /// <param name="images"></param>
        /// <param name="songs"></param>
        /// <param name="comments"></param>
        /// <param name="verseDetails"></param>
        /// <param name="navigation"></param>
        /// <param name="relatedpoems"></param>
        /// <param name="sections">sections</param>
        /// <param name="commentSortOrder"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanPoemCompleteViewModel>> GetPoemById(int id, bool catInfo = true, bool catPoems = false, bool rhymes = true, bool recitations = true, bool images = true, bool songs = true, bool comments = true, bool verseDetails = true, bool navigation = true, bool relatedpoems = true, bool sections = true, DivanCommentSortOrder commentSortOrder = DivanCommentSortOrder.TopRated)
        {
            var cachKey = $"GetPoemById({id}, {catInfo}, {catPoems}, {rhymes}, {recitations}, {images}, {songs}, {comments}, {verseDetails}, {navigation}, {relatedpoems}, {sections}, {commentSortOrder})";
            if (!_memoryCache.TryGetValue(cachKey, out DivanPoemCompleteViewModel poemViewModel))
            {
                var poem = await _context.DivanPoems.Where(p => p.Id == id).AsNoTracking().SingleOrDefaultAsync();
                if (poem == null)
                {
                    return new RServiceResult<DivanPoemCompleteViewModel>(null); //not found
                }
                DivanPoetCompleteViewModel cat = null;
                if (catInfo)
                {
                    var catRes = await GetCatById(poem.CatId, catPoems);
                    if (!string.IsNullOrEmpty(catRes.ExceptionString))
                    {
                        return new RServiceResult<DivanPoemCompleteViewModel>(null, catRes.ExceptionString);
                    }
                    cat = catRes.Result;
                }

                DivanPoemSummaryViewModel next = null;
                if (navigation)
                {
                    int nextId =
                        await _context.DivanPoems
                                                       .Where(p => p.CatId == poem.CatId && p.Id > poem.Id)
                                                       .AnyAsync()
                                                       ?
                        await _context.DivanPoems
                                                       .Where(p => p.CatId == poem.CatId && p.Id > poem.Id)
                                                       .MinAsync(p => p.Id)
                                                       :
                                                       0;
                    if (nextId != 0)
                    {
                        next = await _context.DivanPoems.Where(p => p.Id == nextId).Select
                            (
                            p =>
                            new DivanPoemSummaryViewModel()
                            {
                                Id = p.Id,
                                Title = p.Title,
                                UrlSlug = p.UrlSlug,
                                Excerpt = _context.DivanVerses.Where(v => v.PoemId == p.Id && v.VOrder == 1).FirstOrDefault().Text
                            }
                            ).AsNoTracking().SingleAsync();
                    }

                }

                DivanPoemSummaryViewModel previous = null;
                if (navigation)
                {
                    int preId =
                        await _context.DivanPoems
                                                       .Where(p => p.CatId == poem.CatId && p.Id < poem.Id)
                                                       .AnyAsync()
                                                       ?
                        await _context.DivanPoems
                                                       .Where(p => p.CatId == poem.CatId && p.Id < poem.Id)
                                                       .MaxAsync(p => p.Id)
                                                       :
                                                       0;
                    if (preId != 0)
                    {
                        previous = await _context.DivanPoems.Where(p => p.Id == preId).Select
                            (
                            p =>
                            new DivanPoemSummaryViewModel()
                            {
                                Id = p.Id,
                                Title = p.Title,
                                UrlSlug = p.UrlSlug,
                                Excerpt = _context.DivanVerses.Where(v => v.PoemId == p.Id && v.VOrder == 1).FirstOrDefault().Text
                            }
                            ).AsNoTracking().SingleAsync();
                    }

                }

                PublicRecitationViewModel[] rc = null;
                if (recitations)
                {
                    var rcRes = await GetPoemRecitations(id);
                    if (!string.IsNullOrEmpty(rcRes.ExceptionString))
                        return new RServiceResult<DivanPoemCompleteViewModel>(null, rcRes.ExceptionString);
                    rc = rcRes.Result;
                }

                PoemRelatedImage[] imgs = null;
                if (images)
                {
                    var imgsRes = await GetPoemImages(id);
                    if (!string.IsNullOrEmpty(imgsRes.ExceptionString))
                        return new RServiceResult<DivanPoemCompleteViewModel>(null, imgsRes.ExceptionString);
                    imgs = imgsRes.Result;
                }

                DivanVerseViewModel[] verses = null;
                if (verseDetails)
                {
                    verses = await _context.DivanVerses
                                                    .Where(v => v.PoemId == id)
                                                    .OrderBy(v => v.VOrder)
                                                    .Select
                                                    (
                                                        v => new DivanVerseViewModel()
                                                        {
                                                            Id = v.Id,
                                                            VOrder = v.VOrder,
                                                            CoupletIndex = v.CoupletIndex,
                                                            VersePosition = v.VersePosition,
                                                            Text = v.Text,
                                                            SectionIndex1 = v.SectionIndex1,
                                                            SectionIndex2 = v.SectionIndex2,
                                                            SectionIndex3 = v.SectionIndex3,
                                                            SectionIndex4 = v.SectionIndex4,
                                                            LanguageId = v.LanguageId,
                                                            CoupletSummary = v.CoupletSummary,
                                                        }
                                                    ).AsNoTracking().ToArrayAsync();
                };


                PoemMusicTrackViewModel[] tracks = null; // divan: music features removed (models kept so they can be rebuilt later)

                DivanCommentSummaryViewModel[] poemComments = null;

                if (comments)
                {
                    var commentsRes = await GetPoemComments(id, Guid.Empty, null, commentSortOrder);
                    if (!string.IsNullOrEmpty(commentsRes.ExceptionString))
                        return new RServiceResult<DivanPoemCompleteViewModel>(null, commentsRes.ExceptionString);
                    poemComments = commentsRes.Result;
                }

                DivanPoemSection[] poemSections = null;
                if (sections)
                {
                    var poemSectionsRes = await GetPoemWholeSections(id);
                    if (!string.IsNullOrEmpty(poemSectionsRes.ExceptionString))
                        return new RServiceResult<DivanPoemCompleteViewModel>(null, poemSectionsRes.ExceptionString);
                    poemSections = poemSectionsRes.Result;
                }

                var tagsRes = await GetPoemGeoDateTagsAsync(id);
                if (!string.IsNullOrEmpty(tagsRes.ExceptionString))
                    return new RServiceResult<DivanPoemCompleteViewModel>(null, tagsRes.ExceptionString);
                PoemGeoDateTag[] geoDateTags = tagsRes.Result;

                var quotedRes = await GetDivanQuotedPoemsForPoemAsync(id, 0, 6, null, true, true);
                if (!string.IsNullOrEmpty(quotedRes.ExceptionString))
                    return new RServiceResult<DivanPoemCompleteViewModel>(null, quotedRes.ExceptionString);
                DivanQuotedPoemViewModel[] quoteds = quotedRes.Result;



                poemViewModel = new DivanPoemCompleteViewModel()
                {
                    Id = poem.Id,
                    Title = poem.Title,
                    FullTitle = poem.FullTitle,
                    FullUrl = poem.FullUrl,
                    UrlSlug = poem.UrlSlug,
                    HtmlText = poem.HtmlText,
                    PlainText = poem.PlainText,
                    SourceName = poem.SourceName,
                    SourceUrlSlug = poem.SourceUrlSlug,
                    OldTag = poem.OldTag,
                    OldTagPageUrl = poem.OldTagPageUrl,
                    MixedModeOrder = poem.MixedModeOrder,
                    Published = poem.Published,
                    Language = poem.Language,
                    PoemSummary = poem.PoemSummary,
                    Category = cat,
                    Next = next,
                    Previous = previous,
                    Recitations = rc,
                    Images = imgs,
                    Verses = verses,
                    Songs = tracks,
                    Comments = poemComments,
                    Sections = poemSections,
                    GeoDateTags = geoDateTags,
                    Top6QuotedPoems = quoteds,
                    ClaimedByMultiplePoets = poem.ClaimedByMultiplePoets,
                };

                if (AggressiveCacheEnabled)
                {
                    _memoryCache.Set(cachKey, poemViewModel, TimeSpan.FromHours(1));
                }
            }
            return new RServiceResult<DivanPoemCompleteViewModel>
                (
                poemViewModel
                );
        }

        /// <summary>
        /// delete unreviewed user corrections for a poem
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="poemId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> DeletePoemCorrections(Guid userId, int poemId)
        {
            var preCorrections = await _context.DivanPoemCorrections.Include(c => c.VerseOrderText).Include(c => c.GeoDateTags)
                .Where(c => c.UserId == userId && c.PoemId == poemId && c.Reviewed == false)
                .ToListAsync();
            if (preCorrections.Count > 0)
            {
                foreach (var preCorrection in preCorrections)
                {
                    preCorrection.VerseOrderText.Clear();
                    preCorrection.GeoDateTags?.Clear();
                }
                _context.DivanPoemCorrections.RemoveRange(preCorrections);
                await _context.SaveChangesAsync();
            }
            return new RServiceResult<bool>(true);
        }

        /// <summary>
        /// send poem correction
        /// </summary>
        /// <param name="correction"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanPoemCorrectionViewModel>> SuggestPoemCorrection(DivanPoemCorrectionViewModel correction)
        {
            // wrapped in try/catch (matching ModeratePoemCorrection's own pattern) so an unexpected
            // exception - e.g. the EF Core "same key value is already being tracked" conflict this
            // method used to throw on a second save - comes back as a normal RServiceResult.ExceptionString
            // instead of an unhandled exception. Left uncaught, it would bypass DivanController's own
            // "if (!string.IsNullOrEmpty(res.ExceptionString)) return BadRequest(...)" check entirely
            // (that line is never reached because this method never returns), fall through to the
            // framework's generic HTML error page, and show up to the user as unreadable HTML source
            // instead of a readable message - with the real exception visible only in the Windows Event
            // Log.
            try
            {
            if (!string.IsNullOrEmpty(correction.Rhythm3) || !string.IsNullOrEmpty(correction.Rhythm4))
                return new RServiceResult<DivanPoemCorrectionViewModel>(null, "انتساب وزن سوم و چهارم هنوز پیاده‌سازی نشده است.");

            var sections = await _context.DivanPoemSections.AsNoTracking().Include(s => s.DivanMetre)
                .Where(s => s.PoemId == correction.PoemId).OrderBy(s => s.SectionType).ThenBy(s => s.Index).ToListAsync();
            //beware: items consisting only of paragraphs have no main setion (mainSection in the following line can legitimately become null)
            var mainSection = sections.FirstOrDefault(s => s.SectionType == PoemSectionType.WholePoem && s.VerseType == VersePoemSectionType.First);
            var secondSection = sections.FirstOrDefault(s => s.SectionType == PoemSectionType.WholePoem && s.VerseType == VersePoemSectionType.Second);

            if (correction.Rhythm != null || correction.Rhythm2 != null)
            {
                if (correction.Rhythm == "")
                    return new RServiceResult<DivanPoemCorrectionViewModel>(null, "امکان حذف وزن اول با وجود وزن دوم وجود ندارد.");

                if (mainSection == null)
                    return new RServiceResult<DivanPoemCorrectionViewModel>(null, "امکان تعیین وزن برای این مورد وجود ندارد.");
                var poemVerses = await _context.DivanVerses.AsNoTracking().
                    Where(p => p.PoemId == correction.PoemId).OrderBy(v => v.VOrder).ToListAsync();
                if (poemVerses.Where(v => v.VersePosition == VersePosition.Paragraph).Any())
                {
                    return new RServiceResult<DivanPoemCorrectionViewModel>(null, "امکان انتساب وزن به متون مخلوط از طریق ویرایشگر کاربر وجود ندارد.");
                }
                if (sections.Where(s => s.SectionType == PoemSectionType.WholePoem && s.VerseType == VersePoemSectionType.First).Count() > 1)
                {
                    return new RServiceResult<DivanPoemCorrectionViewModel>(null, "امکان انتساب وزن به متون حاوی بیش از یک شعر از طریق ویرایشگر کاربر وجود ندارد.");
                }
            }

            var preCorrections = await _context.DivanPoemCorrections.Include(c => c.VerseOrderText).Include(c => c.GeoDateTags)
                .Where(c => c.UserId == correction.UserId && c.PoemId == correction.PoemId && c.Reviewed == false)
                .ToListAsync();

            var poem = (await GetPoemById(correction.PoemId, false, false, true, false, false, false, false, true, false)).Result;

            if (correction.GeoDateTags != null)
            {
                // already-approved tags for this poem, used below to reject suggestions that just
                // repeat what's already tagged on the same couplet - no extra service call needed,
                // GetPoemById above always loads this
                var approvedGeoDateTags = poem.GeoDateTags ?? Array.Empty<PoemGeoDateTag>();

                // lazily loaded only if a tag actually suggests a brand new location - most
                // submissions either pick an existing location or only carry a date, so this
                // extra query is skipped entirely in the common case
                DivanGeoLocation[] allLocations = null;

                foreach (var geoDateTag in correction.GeoDateTags)
                {
                    if (!geoDateTag.MarkForDelete)
                    {
                        bool hasLocation = geoDateTag.LocationId != null ||
                            (!string.IsNullOrWhiteSpace(geoDateTag.SuggestedLocationName) && geoDateTag.SuggestedLatitude != null && geoDateTag.SuggestedLongitude != null);
                        bool hasDate = geoDateTag.LunarYear != null;
                        bool hasPerson = geoDateTag.PersonId != null || !string.IsNullOrWhiteSpace(geoDateTag.SuggestedPersonGraphJson);
                        if (!hasLocation && !hasDate && !hasPerson)
                        {
                            return new RServiceResult<DivanPoemCorrectionViewModel>(null, "برچسب جغرافیایی/تاریخی/شخصیتی باید حداقل شامل مکان، تاریخ یا شخصیت باشد.");
                        }

                        if (geoDateTag.LocationId == null && !string.IsNullOrWhiteSpace(geoDateTag.SuggestedLocationName)
                            && geoDateTag.SuggestedLatitude != null && geoDateTag.SuggestedLongitude != null)
                        {
                            // catches "this is already a catalogued place" regardless of which couplet/poem
                            // it was previously tagged on - a hard reject, since coordinates a few kilometers
                            // apart are effectively certainly the same real place, not a judgment call
                            const double nearDuplicateKm = 5.0;
                            if (allLocations == null)
                            {
                                allLocations = await _context.DivanGeoLocations.AsNoTracking().ToArrayAsync();
                            }
                            var nearExistingLocation = allLocations.FirstOrDefault(l =>
                                _GeoDistanceKm((double)geoDateTag.SuggestedLatitude, (double)geoDateTag.SuggestedLongitude, l.Latitude, l.Longitude) <= nearDuplicateKm);
                            if (nearExistingLocation != null)
                            {
                                return new RServiceResult<DivanPoemCorrectionViewModel>(null,
                                    $"مکانی با نام «{nearExistingLocation.Name}» با مختصات بسیار نزدیک از قبل در فهرست مکان‌ها ثبت شده است. لطفاً به‌جای پیشنهاد مکان جدید، همان را از فهرست «مکان» انتخاب کنید.");
                            }
                            // a same-name-but-far-away match is deliberately NOT rejected here - it can
                            // legitimately be a different real place sharing a name, and the editor's
                            // client-side check already asks the user to confirm that before it gets here
                        }

                        // PoemGeoDateTag.CoupletIndex uses 0 for "whole poem", matching a null CoupletIndex here
                        int effectiveCoupletIndex = geoDateTag.CoupletIndex ?? 0;
                        var approvedForCouplet = approvedGeoDateTags.Where(t => t.CoupletIndex == effectiveCoupletIndex);

                        if (hasLocation)
                        {
                            bool locationAlreadyTagged = geoDateTag.LocationId != null
                                ? approvedForCouplet.Any(t => t.LocationId == geoDateTag.LocationId)
                                : approvedForCouplet.Any(t => t.Location != null &&
                                    string.Equals(t.Location.Name?.Trim(), geoDateTag.SuggestedLocationName.Trim(), StringComparison.OrdinalIgnoreCase));
                            if (locationAlreadyTagged)
                            {
                                return new RServiceResult<DivanPoemCorrectionViewModel>(null, "این مکان از قبل برای همین بیت ثبت شده است.");
                            }
                        }

                        if (hasDate)
                        {
                            bool dateAlreadyTagged = approvedForCouplet.Any(t =>
                                t.LunarYear == geoDateTag.LunarYear &&
                                t.LunarMonth == geoDateTag.LunarMonth &&
                                t.LunarDay == geoDateTag.LunarDay);
                            if (dateAlreadyTagged)
                            {
                                return new RServiceResult<DivanPoemCorrectionViewModel>(null, "این تاریخ از قبل برای همین بیت ثبت شده است.");
                            }
                        }
                    }
                }
            }

            foreach (var verse in correction.VerseOrderText)
            {
                if (!verse.NewVerse)
                {
                    var v = poem.Verses.Where(poemVerse => poemVerse.VOrder == verse.VORder).First();
                    verse.OriginalText = v.Text;
                    verse.OriginalVersePosition = v.VersePosition;
                    verse.OriginalLanguageId = v.LanguageId;
                    verse.OriginalCoupletSummary = v.CoupletSummary;
                }
            }
            if (correction.GeoDateTags != null)
            {
                // every submission here creates a brand-new DivanPoemCorrection row (and any older
                // unreviewed correction for the same user/poem - preCorrections above - is removed
                // together with its own GeoDateTags rows a few lines down). The client pre-fills the
                // form from the user's previous, still-unreviewed draft (MyLastEditGeoDateTagsJson in
                // Editor.cshtml.cs/.cshtml), so when the user only adds one more tag and resubmits, the
                // posted GeoDateTags array still carries the OLD rows' real database Ids alongside the
                // new one. Those old rows are already tracked by EF from the preCorrections query above,
                // so attaching this freshly-deserialized correction.GeoDateTags (Added, because its new
                // parent dbCorrection is Added) under those same Ids makes EF try to track two different
                // instances for the same key, throwing "cannot be tracked because another instance with
                // the same key value ... is already being tracked" - reproducible simply by saving a
                // correction, then adding another person/tag and saving again. Every tag belongs to a
                // brand-new correction row here, so none of them should ever reuse an old row's Id.
                foreach (var geoDateTag in correction.GeoDateTags)
                {
                    geoDateTag.Id = 0;
                }
            }

            DivanPoemCorrection dbCorrection = new DivanPoemCorrection()
            {
                PoemId = correction.PoemId,
                UserId = correction.UserId,
                VerseOrderText = correction.VerseOrderText,
                Title = correction.Title,
                OriginalTitle = poem.Title,
                Rhythm = correction.Rhythm,
                OriginalRhythm = (mainSection == null || mainSection.DivanMetre == null) ? null : mainSection.DivanMetre.Rhythm,
                Rhythm2 = correction.Rhythm2,
                OriginalRhythm2 = (secondSection == null || secondSection.DivanMetre == null) ? null : secondSection.DivanMetre.Rhythm,
                RhymeLetters = correction.RhymeLetters,
                OriginalRhymeLetters = mainSection == null ? null : mainSection.RhymeLetters,
                PoemFormat = correction.PoemFormat,
                OriginalPoemFormat = correction.PoemFormat == null ? null : mainSection.PoemFormat,
                Note = correction.Note,
                Date = DateTime.Now,
                Result = CorrectionReviewResult.NotReviewed,
                Reviewed = false,
                AffectedThePoem = false,
                //Language = correction.Language, not used
                PoemSummary = correction.PoemSummary,
                HideMyName = correction.HideMyName,
                GeoDateTags = correction.GeoDateTags,

            };
            _context.DivanPoemCorrections.Add(dbCorrection);
            await _context.SaveChangesAsync();
            correction.Id = dbCorrection.Id;

            if (preCorrections.Count > 0)
            {
                foreach (var preCorrection in preCorrections)
                {
                    preCorrection.VerseOrderText.Clear();
                    preCorrection.GeoDateTags?.Clear();
                }
                _context.DivanPoemCorrections.RemoveRange(preCorrections);
                await _context.SaveChangesAsync();
            }

            return new RServiceResult<DivanPoemCorrectionViewModel>(correction);
            }
            catch (Exception exp)
            {
                return new RServiceResult<DivanPoemCorrectionViewModel>(null, exp.ToString());
            }
        }

        /// <summary>
        /// last unreviewed user correction for a poem
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="poemId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanPoemCorrectionViewModel>> GetLastUnreviewedUserCorrectionForPoem(Guid userId, int poemId)
        {
            var dbCorrection = await _context.DivanPoemCorrections.AsNoTracking().Include(c => c.VerseOrderText).Include(c => c.GeoDateTags).ThenInclude(g => g.Location).Include(c => c.User)
                .Where(c => c.UserId == userId && c.PoemId == poemId && c.Reviewed == false)
                .OrderByDescending(c => c.Id)
                .FirstOrDefaultAsync();

            if (dbCorrection == null)
                return new RServiceResult<DivanPoemCorrectionViewModel>(null);

            return new RServiceResult<DivanPoemCorrectionViewModel>
                (
                new DivanPoemCorrectionViewModel()
                {
                    Id = dbCorrection.Id,
                    PoemId = dbCorrection.PoemId,
                    UserId = dbCorrection.UserId,
                    VerseOrderText = dbCorrection.VerseOrderText == null ? null : dbCorrection.VerseOrderText.ToArray(),
                    Title = dbCorrection.Title,
                    OriginalTitle = dbCorrection.OriginalTitle,
                    Rhythm = dbCorrection.Rhythm,
                    OriginalRhythm = dbCorrection.OriginalRhythm,
                    RhythmResult = dbCorrection.RhythmResult,
                    Rhythm2 = dbCorrection.Rhythm2,
                    OriginalRhythm2 = dbCorrection.OriginalRhythm2,
                    RhymeLetters = dbCorrection.RhymeLetters,
                    OriginalRhymeLetters = dbCorrection.OriginalRhymeLetters,
                    RhymeLettersReviewResult = dbCorrection.RhymeLettersReviewResult,
                    PoemSummary = dbCorrection.PoemSummary,
                    OriginalPoemSummary = dbCorrection.OriginalPoemSummary,
                    SummaryReviewResult = dbCorrection.SummaryReviewResult,
                    Note = dbCorrection.Note,
                    Date = dbCorrection.Date,
                    Reviewed = dbCorrection.Reviewed,
                    Result = dbCorrection.Result,
                    ReviewNote = dbCorrection.ReviewNote,
                    ReviewDate = dbCorrection.ReviewDate,
                    UserNickname = dbCorrection.HideMyName && dbCorrection.Reviewed ? "" : string.IsNullOrEmpty(dbCorrection.User.NickName) ? dbCorrection.User.Id.ToString() : dbCorrection.User.NickName,
                    PoemFormat = dbCorrection.PoemFormat,
                    OriginalPoemFormat = dbCorrection.OriginalPoemFormat,
                    PoemFormatReviewResult = dbCorrection.PoemFormatReviewResult,
                    HideMyName = dbCorrection.HideMyName,
                    GeoDateTags = dbCorrection.GeoDateTags == null ? null : dbCorrection.GeoDateTags.ToArray(),
                }
                );
        }

        /// <summary>
        /// get user or all corrections
        /// </summary>
        /// <param name="userId">if sent empty returns all corrections</param>
        /// <param name="paging"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, DivanPoemCorrectionViewModel[] Items)>> GetUserCorrections(Guid userId, PagingParameterModel paging)
        {
            var source = from dbCorrection in
                             _context.DivanPoemCorrections.AsNoTracking().Include(c => c.VerseOrderText).Include(c => c.GeoDateTags).ThenInclude(g => g.Location).Include(c => c.User)
                         where userId == Guid.Empty || dbCorrection.UserId == userId
                         orderby dbCorrection.Id descending
                         select
                          dbCorrection;

            (PaginationMetadata PagingMeta, DivanPoemCorrection[] Items) dbPaginatedResult =
                await QueryablePaginator<DivanPoemCorrection>.Paginate(source, paging);

            List<DivanPoemCorrectionViewModel> list = new List<DivanPoemCorrectionViewModel>();
            foreach (var dbCorrection in dbPaginatedResult.Items)
            {
                list.Add
                    (
                new DivanPoemCorrectionViewModel()
                {
                    Id = dbCorrection.Id,
                    PoemId = dbCorrection.PoemId,
                    UserId = dbCorrection.UserId,
                    VerseOrderText = dbCorrection.VerseOrderText == null ? null : dbCorrection.VerseOrderText.ToArray(),
                    Title = dbCorrection.Title,
                    OriginalTitle = dbCorrection.OriginalTitle,
                    Rhythm = dbCorrection.Rhythm,
                    OriginalRhythm = dbCorrection.OriginalRhythm,
                    RhythmResult = dbCorrection.RhythmResult,
                    Rhythm2 = dbCorrection.Rhythm2,
                    OriginalRhythm2 = dbCorrection.OriginalRhythm2,
                    Rhythm2Result = dbCorrection.Rhythm2Result,
                    RhymeLetters = dbCorrection.RhymeLetters,
                    OriginalRhymeLetters = dbCorrection.OriginalRhymeLetters,
                    RhymeLettersReviewResult = dbCorrection.RhymeLettersReviewResult,
                    PoemSummary = dbCorrection.PoemSummary,
                    OriginalPoemSummary = dbCorrection.OriginalPoemSummary,
                    SummaryReviewResult = dbCorrection.SummaryReviewResult,
                    Note = dbCorrection.Note,
                    Date = dbCorrection.Date,
                    Reviewed = dbCorrection.Reviewed,
                    Result = dbCorrection.Result,
                    ReviewNote = dbCorrection.ReviewNote,
                    ReviewDate = dbCorrection.ReviewDate,
                    UserNickname = dbCorrection.HideMyName && dbCorrection.Reviewed ? "" : string.IsNullOrEmpty(dbCorrection.User.NickName) ? dbCorrection.User.Id.ToString() : dbCorrection.User.NickName,
                    PoemFormat = dbCorrection.PoemFormat,
                    OriginalPoemFormat = dbCorrection.OriginalPoemFormat,
                    PoemFormatReviewResult = dbCorrection.PoemFormatReviewResult,
                    HideMyName = dbCorrection.HideMyName,
                    GeoDateTags = dbCorrection.GeoDateTags == null ? null : dbCorrection.GeoDateTags.ToArray(),
                }
                );
            }

            return new RServiceResult<(PaginationMetadata, DivanPoemCorrectionViewModel[])>
                ((dbPaginatedResult.PagingMeta, list.ToArray()));
        }

        /// <summary>
        /// effective corrections for poem
        /// </summary>
        /// <param name="poemId"></param>
        /// <param name="paging"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, DivanPoemCorrectionViewModel[] Items)>> GetPoemEffectiveCorrections(int poemId, PagingParameterModel paging)
        {
            var source = from dbCorrection in
                             _context.DivanPoemCorrections.AsNoTracking().Include(c => c.VerseOrderText).Include(c => c.GeoDateTags).ThenInclude(g => g.Location)
                         where
                         dbCorrection.PoemId == poemId
                         &&
                         dbCorrection.Reviewed == true
                         &&
                         (
                         dbCorrection.Result == CorrectionReviewResult.Approved || dbCorrection.RhythmResult == CorrectionReviewResult.Approved
                         || dbCorrection.Rhythm2Result == CorrectionReviewResult.Approved || dbCorrection.RhymeLettersReviewResult == CorrectionReviewResult.Approved
                         || dbCorrection.PoemFormatReviewResult == CorrectionReviewResult.Approved
                         ||
                         dbCorrection.SummaryReviewResult == CorrectionReviewResult.Approved
                         ||
                         dbCorrection.VerseOrderText
                            .Any(v =>
                                v.Result == CorrectionReviewResult.Approved
                                ||
                                v.VersePositionResult == CorrectionReviewResult.Approved
                                ||
                                v.MarkForDeleteResult == CorrectionReviewResult.Approved
                                ||
                                v.NewVerseResult == CorrectionReviewResult.Approved
                                ||
                                v.SummaryReviewResult == CorrectionReviewResult.Approved
                                ||
                                v.LanguageReviewResult == CorrectionReviewResult.Approved
                                )
                         ||
                         dbCorrection.GeoDateTags.Any(g => g.Result == CorrectionReviewResult.Approved)
                         )
                         orderby dbCorrection.Id descending
                         select
                         dbCorrection;

            (PaginationMetadata PagingMeta, DivanPoemCorrection[] Items) dbPaginatedResult =
                await QueryablePaginator<DivanPoemCorrection>.Paginate(source, paging);

            List<DivanPoemCorrectionViewModel> list = new List<DivanPoemCorrectionViewModel>();
            foreach (var dbCorrection in dbPaginatedResult.Items)
            {
                list.Add
                    (
                new DivanPoemCorrectionViewModel()
                {
                    Id = dbCorrection.Id,
                    PoemId = dbCorrection.PoemId,
                    UserId = dbCorrection.UserId,
                    VerseOrderText = dbCorrection.VerseOrderText == null ? null : dbCorrection.VerseOrderText.ToArray(),
                    Title = dbCorrection.Title,
                    OriginalTitle = dbCorrection.OriginalTitle,
                    Rhythm = dbCorrection.Rhythm,
                    OriginalRhythm = dbCorrection.OriginalRhythm,
                    RhythmResult = dbCorrection.RhythmResult,
                    Rhythm2 = dbCorrection.Rhythm2,
                    OriginalRhythm2 = dbCorrection.OriginalRhythm2,
                    Rhythm2Result = dbCorrection.Rhythm2Result,
                    RhymeLetters = dbCorrection.RhymeLetters,
                    OriginalRhymeLetters = dbCorrection.OriginalRhymeLetters,
                    RhymeLettersReviewResult = dbCorrection.RhymeLettersReviewResult,
                    PoemSummary = dbCorrection.PoemSummary,
                    OriginalPoemSummary = dbCorrection.OriginalPoemSummary,
                    SummaryReviewResult = dbCorrection.SummaryReviewResult,
                    Note = dbCorrection.Note,
                    Date = dbCorrection.Date,
                    Reviewed = dbCorrection.Reviewed,
                    Result = dbCorrection.Result,
                    ReviewNote = dbCorrection.ReviewNote,
                    ReviewDate = dbCorrection.ReviewDate,
                    PoemFormat = dbCorrection.PoemFormat,
                    OriginalPoemFormat = dbCorrection.OriginalPoemFormat,
                    PoemFormatReviewResult = dbCorrection.PoemFormatReviewResult,
                    GeoDateTags = dbCorrection.GeoDateTags == null ? null : dbCorrection.GeoDateTags.ToArray(),
                }
                );
            }

            return new RServiceResult<(PaginationMetadata, DivanPoemCorrectionViewModel[])>
                ((dbPaginatedResult.PagingMeta, list.ToArray()));
        }

        /// <summary>
        /// get correction by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanPoemCorrectionViewModel>> GetCorrectionById(int id)
        {
            var dbCorrection = await _context.DivanPoemCorrections.AsNoTracking().Include(c => c.VerseOrderText).Include(c => c.GeoDateTags).ThenInclude(g => g.Location).Include(c => c.User)
                .Where(c => c.Id == id)
                .FirstOrDefaultAsync();

            if (dbCorrection == null)
                return new RServiceResult<DivanPoemCorrectionViewModel>(null);

            return new RServiceResult<DivanPoemCorrectionViewModel>
                (
                new DivanPoemCorrectionViewModel()
                {
                    Id = dbCorrection.Id,
                    PoemId = dbCorrection.PoemId,
                    UserId = dbCorrection.UserId,
                    VerseOrderText = dbCorrection.VerseOrderText == null ? null : dbCorrection.VerseOrderText.OrderBy(v => v.VORder).ToArray(),
                    Title = dbCorrection.Title,
                    OriginalTitle = dbCorrection.OriginalTitle,
                    Rhythm = dbCorrection.Rhythm,
                    OriginalRhythm = dbCorrection.OriginalRhythm,
                    RhythmResult = dbCorrection.RhythmResult,
                    Rhythm2 = dbCorrection.Rhythm2,
                    OriginalRhythm2 = dbCorrection.OriginalRhythm2,
                    Rhythm2Result = dbCorrection.Rhythm2Result,
                    RhymeLetters = dbCorrection.RhymeLetters,
                    OriginalRhymeLetters = dbCorrection.OriginalRhymeLetters,
                    RhymeLettersReviewResult = dbCorrection.RhymeLettersReviewResult,
                    PoemSummary = dbCorrection.PoemSummary,
                    OriginalPoemSummary = dbCorrection.OriginalPoemSummary,
                    SummaryReviewResult = dbCorrection.SummaryReviewResult,
                    Note = dbCorrection.Note,
                    Date = dbCorrection.Date,
                    Reviewed = dbCorrection.Reviewed,
                    Result = dbCorrection.Result,
                    ReviewNote = dbCorrection.ReviewNote,
                    ReviewDate = dbCorrection.ReviewDate,
                    UserNickname = dbCorrection.HideMyName && dbCorrection.Reviewed ? "" : string.IsNullOrEmpty(dbCorrection.User.NickName) ? dbCorrection.User.Id.ToString() : dbCorrection.User.NickName,
                    PoemFormat = dbCorrection.PoemFormat,
                    OriginalPoemFormat = dbCorrection.OriginalPoemFormat,
                    PoemFormatReviewResult = dbCorrection.PoemFormatReviewResult,
                    HideMyName = dbCorrection.HideMyName,
                    GeoDateTags = dbCorrection.GeoDateTags == null ? null : dbCorrection.GeoDateTags.ToArray(),
                }
                );
        }

        /// <summary>
        /// get next unreviewed correction
        /// </summary>
        /// <param name="skip"></param>
        /// <param name="onlyUserCorrections"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanPoemCorrectionViewModel>> GetNextUnreviewedCorrection(int skip, bool onlyUserCorrections)
        {
            string systemEmail = $"{Configuration.GetSection("Divan")["SystemEmail"]}";
            var systemUser = await _appUserService.FindUserByEmail(systemEmail);
            var systemUserId = systemUser.Result == null ? Guid.Empty : (Guid)systemUser.Result.Id;

            var dbCorrection = await _context.DivanPoemCorrections.AsNoTracking().Include(c => c.VerseOrderText).Include(c => c.GeoDateTags).ThenInclude(g => g.Location).Include(c => c.User)
                .Where(c => c.Reviewed == false && (onlyUserCorrections == false || c.UserId != systemUserId))
                .OrderBy(c => c.Id)
                .Skip(skip)
                .FirstOrDefaultAsync();

            if (dbCorrection == null)
                return new RServiceResult<DivanPoemCorrectionViewModel>(null);

            return new RServiceResult<DivanPoemCorrectionViewModel>
                (
                new DivanPoemCorrectionViewModel()
                {
                    Id = dbCorrection.Id,
                    PoemId = dbCorrection.PoemId,
                    UserId = dbCorrection.UserId,
                    VerseOrderText = dbCorrection.VerseOrderText == null ? null : dbCorrection.VerseOrderText.ToArray(),
                    Title = dbCorrection.Title,
                    OriginalTitle = dbCorrection.OriginalTitle,
                    Rhythm = dbCorrection.Rhythm,
                    OriginalRhythm = dbCorrection.OriginalRhythm,
                    RhythmResult = dbCorrection.RhythmResult,
                    RhymeLetters = dbCorrection.RhymeLetters,
                    OriginalRhymeLetters = dbCorrection.OriginalRhymeLetters,
                    RhymeLettersReviewResult = dbCorrection.RhymeLettersReviewResult,
                    PoemSummary = dbCorrection.PoemSummary,
                    OriginalPoemSummary = dbCorrection.OriginalPoemSummary,
                    SummaryReviewResult = dbCorrection.SummaryReviewResult,
                    Note = dbCorrection.Note,
                    Date = dbCorrection.Date,
                    Reviewed = dbCorrection.Reviewed,
                    Result = dbCorrection.Result,
                    Rhythm2 = dbCorrection.Rhythm2,
                    OriginalRhythm2 = dbCorrection.OriginalRhythm2,
                    Rhythm2Result = dbCorrection.Rhythm2Result,
                    ReviewNote = dbCorrection.ReviewNote,
                    ReviewDate = dbCorrection.ReviewDate,
                    UserNickname = dbCorrection.HideMyName && dbCorrection.Reviewed ? "" : string.IsNullOrEmpty(dbCorrection.User.NickName) ? dbCorrection.User.Id.ToString() : dbCorrection.User.NickName,
                    PoemFormat = dbCorrection.PoemFormat,
                    OriginalPoemFormat = dbCorrection.OriginalPoemFormat,
                    PoemFormatReviewResult = dbCorrection.PoemFormatReviewResult,
                    HideMyName = dbCorrection.HideMyName,
                    GeoDateTags = dbCorrection.GeoDateTags == null ? null : dbCorrection.GeoDateTags.ToArray(),
                }
                );
        }

        /// <summary>
        /// unreviewed corrections count
        /// </summary>
        /// <param name="onlyUserCorrections"></param>
        /// <returns></returns>
        public async Task<RServiceResult<int>> GetUnreviewedCorrectionCount(bool onlyUserCorrections)
        {
            string systemEmail = $"{Configuration.GetSection("Divan")["SystemEmail"]}";
            var systemUser = await _appUserService.FindUserByEmail(systemEmail);
            var systemUserId = systemUser.Result == null ? Guid.Empty : (Guid)systemUser.Result.Id;
            return new RServiceResult<int>(await _context.DivanPoemCorrections.AsNoTracking().Include(c => c.VerseOrderText)
                .Where(c => c.Reviewed == false && (onlyUserCorrections == false || c.UserId != systemUserId))
                .CountAsync());
        }

        /// <summary>
        /// random poem id from hafez sonnets and old c.ganjoor.net service
        /// </summary>
        /// <returns></returns>
        private int _GetRandomPoemId(int poetId, int loopBreaker = 0)
        {
            // divan: data-driven (upstream used hard-coded id ranges of Persian poets); 0 = any poet
            var poems = _context.DivanPoems.AsNoTracking().Where(p => p.Published);
            if (poetId != 0)
                poems = poems.Where(p => p.Cat.PoetId == poetId);
            return poems.OrderBy(p => Guid.NewGuid()).Select(p => p.Id).FirstOrDefault();
        }



        /// <summary>
        /// get a random poem from hafez
        /// </summary>
        /// <param name="poetId"></param>
        /// <param name="recitation"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanPoemCompleteViewModel>> Faal(int poetId = 0, bool recitation = true)
        {
            int poemId = _GetRandomPoemId(poetId);
            var poem = await _context.DivanPoems.Where(p => p.Id == poemId).AsNoTracking().SingleOrDefaultAsync();
            PublicRecitationViewModel[] recitations = poem == null || !recitation ? new PublicRecitationViewModel[] { } : (await GetPoemRecitations(poemId)).Result;
            int loopPreventer = 0;
            while (poem == null || (recitation && recitations.Length == 0))
            {
                poem = await _context.DivanPoems.Where(p => p.Id == poemId).AsNoTracking().SingleOrDefaultAsync();
                recitations = poem == null ? new PublicRecitationViewModel[] { } : (await GetPoemRecitations(poemId)).Result;
                loopPreventer++;
                if (loopPreventer > 5)
                {
                    return new RServiceResult<DivanPoemCompleteViewModel>(null);
                }
            }

            return await GetPoemById(poemId, false, false, false, recitation, false, false, false, true /*verse details*/, false);
        }

        /// <summary>
        /// Get Similar Poems accroding to prosody and rhyme informations
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="metre"></param>
        /// <param name="rhyme"></param>
        /// <param name="poetId"></param>
        /// <param name="language"></param>
        /// <param name="format"></param>
        /// <param name="catId"></param>
        /// <param name="term"></param>
        /// <param name="coupletCountsFrom"></param>
        /// <param name="coupletCountsTo"></param>
        /// <param name="exceptPoetId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, DivanPoemCompleteViewModel[] Items)>> GetSimilarPoemsAsync(PagingParameterModel paging, string metre, string rhyme, int? poetId, int? catId, string language, DivanPoemFormat format , string term , int coupletCountsFrom, int coupletCountsTo, int[] exceptPoetId)
        {
            if (poetId == null)
            {
                catId = null;
            }
            List<int> catIdList = new List<int>();
            if (catId != null)
            {
                catIdList.Add((int)catId);
                await _populateCategoryChildren(_context, (int)catId, catIdList);
            }
            string[] searchPatterns = LanguageUtils.SearchLikePatterns(term); // divan: LIKE instead of full-text; empty term -> no filter
            var sections = _context.DivanPoemSections.AsQueryable();
            foreach (var pattern in searchPatterns)
                sections = sections.Where(s => EF.Functions.Like(s.Poem.PlainText, pattern));
            var source =
                sections.Include(s => s.Poem).Include(s => s.Poet).Include(s => s.DivanMetre)
                .Where(s =>
                        (poetId == null || s.PoetId == poetId)
                        &&
                        (exceptPoetId.Length == 0 || !exceptPoetId.Contains(s.PoetId ?? 0))
                        &&
                        ((language == "ur-PK" && string.IsNullOrEmpty(s.Language)) || s.Language == language)
                        &&
                        (string.IsNullOrEmpty(metre) || (metre == "null" && s.DivanMetreId == null) || (!string.IsNullOrEmpty(metre) && s.DivanMetre.Rhythm == metre))
                        &&
                        ((string.IsNullOrEmpty(rhyme) && s.SectionType == PoemSectionType.WholePoem) || (!string.IsNullOrEmpty(rhyme) && s.RhymeLetters == rhyme))
                        &&
                        (format == DivanPoemFormat.Unknown || s.PoemFormat == format)
                        &&
                        (catId == null || catIdList.Contains(s.Poem.CatId))
                        &&
                        (s.CoupletsCount >= coupletCountsFrom)
                        &&
                        (coupletCountsTo == 0 || s.CoupletsCount <= coupletCountsTo)
                        )
                .OrderBy(p => p.Poet.BirthYearInLHijri).ThenBy(p => p.Poet.Nickname).ThenBy(p => p.SectionType).ThenBy(p => p.Poem.Id)
                .Select
                (
                    section =>
                    new DivanPoemCompleteViewModel()
                    {
                        Id = section.Poem.Id,
                        Title = section.Poem.Title,
                        FullTitle = section.Poem.FullTitle,
                        FullUrl = section.CachedFirstCoupletIndex == 0 ? section.Poem.FullUrl : section.Poem.FullUrl + "#bn" + (section.CachedFirstCoupletIndex + 1).ToString(),
                        UrlSlug = section.Poem.UrlSlug,
                        HtmlText = section.HtmlText,
                        PlainText = section.PlainText,
                        MixedModeOrder = section.Poem.MixedModeOrder,
                        Published = section.Poem.Published,
                        Language = section.Poem.Language,
                        PoemSummary = section.Poem.PoemSummary,
                        Category = new DivanPoetCompleteViewModel()
                        {
                            Poet = new DivanPoetViewModel()
                            {
                                Id = section.Poet.Id,
                            }
                        },
                        SectionIndex = section.Index,
                        ClaimedByMultiplePoets = section.Poem.ClaimedByMultiplePoets,
                        CoupletsCount = section.CoupletsCount,

                    }
                ).AsNoTracking();


            (PaginationMetadata PagingMeta, DivanPoemCompleteViewModel[] Items) paginatedResult =
               await QueryablePaginator<DivanPoemCompleteViewModel>.Paginate(source, paging);


            Dictionary<int, DivanPoetCompleteViewModel> cachedPoets = new Dictionary<int, DivanPoetCompleteViewModel>();

            foreach (var item in paginatedResult.Items)
            {
                if (cachedPoets.TryGetValue(item.Category.Poet.Id, out DivanPoetCompleteViewModel poet))
                {
                    item.Category = poet;
                }
                else
                {
                    poet = (await GetPoetById(item.Category.Poet.Id)).Result;

                    cachedPoets.Add(item.Category.Poet.Id, poet);

                    item.Category = poet;
                }
            }

            return new RServiceResult<(PaginationMetadata PagingMeta, DivanPoemCompleteViewModel[] Items)>(paginatedResult);
        }

        private async Task _populateCategoryChildren(RMuseumDbContext context, int catId, List<int> catListId)
        {
            var catRes = await _GetCatById(context, catId, false);
            foreach (var c in catRes.Result.Cat.Children)
            {
                catListId.Add(c.Id);
                await _populateCategoryChildren(context, c.Id, catListId);
            }
        }

        /// <summary>
        /// language tagged poem sections
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="language"></param>
        /// <param name="poetId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, DivanPoemCompleteViewModel[] Items)>> GetLanguageTaggedPoemSections(PagingParameterModel paging, string language, int? poetId)
        {
            if (string.IsNullOrEmpty(language))
            {
                language = "ur-PK";
            }
            var source =
                _context.DivanPoemSections.Include(s => s.Poem).Include(s => s.Poet).Include(s => s.DivanMetre)
                .Where(s =>
                        (poetId == null || s.PoetId == poetId)
                        &&
                        ((language == "ur-PK" && string.IsNullOrEmpty(s.Language)) || s.Language == language)
                        &&
                        s.SectionType == PoemSectionType.WholePoem
                        )
                .OrderBy(p => p.Poet.BirthYearInLHijri).ThenBy(p => p.Poet.Nickname).ThenBy(p => p.Poem.Id)
                .Select
                (
                    section =>
                    new DivanPoemCompleteViewModel()
                    {
                        Id = section.Poem.Id,
                        Title = section.Poem.Title,
                        FullTitle = section.Poem.FullTitle,
                        FullUrl = section.CachedFirstCoupletIndex == 0 ? section.Poem.FullUrl : section.Poem.FullUrl + "#bn" + (section.CachedFirstCoupletIndex + 1).ToString(),
                        UrlSlug = section.Poem.UrlSlug,
                        HtmlText = section.HtmlText,
                        PlainText = section.PlainText,
                        MixedModeOrder = section.Poem.MixedModeOrder,
                        Published = section.Poem.Published,
                        Language = section.Poem.Language,
                        PoemSummary = section.Poem.PoemSummary,
                        Category = new DivanPoetCompleteViewModel()
                        {
                            Poet = new DivanPoetViewModel()
                            {
                                Id = section.Poet.Id,
                            }
                        },
                        SectionIndex = section.Index,
                        ClaimedByMultiplePoets = section.Poem.ClaimedByMultiplePoets,

                    }
                ).AsNoTracking();


            (PaginationMetadata PagingMeta, DivanPoemCompleteViewModel[] Items) paginatedResult =
               await QueryablePaginator<DivanPoemCompleteViewModel>.Paginate(source, paging);


            Dictionary<int, DivanPoetCompleteViewModel> cachedPoets = new Dictionary<int, DivanPoetCompleteViewModel>();

            foreach (var item in paginatedResult.Items)
            {
                if (cachedPoets.TryGetValue(item.Category.Poet.Id, out DivanPoetCompleteViewModel poet))
                {
                    item.Category = poet;
                }
                else
                {
                    poet = (await GetPoetById(item.Category.Poet.Id)).Result;

                    cachedPoets.Add(item.Category.Poet.Id, poet);

                    item.Category = poet;
                }
            }

            return new RServiceResult<(PaginationMetadata PagingMeta, DivanPoemCompleteViewModel[] Items)>(paginatedResult);
        }



        /// <summary>
        /// Search
        /// You need to run this scripts manually on the database before using this method:
        /// 
        /// CREATE FULLTEXT CATALOG [DivanPoemPlainTextCatalog] WITH ACCENT_SENSITIVITY = OFF AS DEFAULT
        /// 
        /// CREATE FULLTEXT INDEX ON [dbo].[DivanPoems](
        /// [PlainText] LANGUAGE 'English')
        /// KEY INDEX [PK_DivanPoems]ON ([DivanPoemPlainTextCatalog], FILEGROUP [PRIMARY])
        /// WITH (CHANGE_TRACKING = AUTO, STOPLIST = SYSTEM)
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="term"></param>
        /// <param name="poetId"></param>
        /// <param name="catId"></param>
        /// <param name="exceptPoetId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, DivanPoemCompleteViewModel[] Items)>> Search(PagingParameterModel paging, string term, int? poetId, int? catId, int[] exceptPoetId)
        {
            term = term.Trim().ApplyCorrectYeKe();

            if (string.IsNullOrEmpty(term))
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, DivanPoemCompleteViewModel[] Items)>((null, null), "خطای جستجوی عبارت خالی");
            }

            term = term.Replace("‌", " ");//replace zwnj with space


            string[] searchPatterns = LanguageUtils.SearchLikePatterns(term); // divan: LIKE instead of full-text
            if (poetId == null)
            {
                catId = null;
            }
            if (poetId != null && catId != null)
            {
                var cat = await _context.DivanCategories.AsNoTracking().Where(c => c.Id == catId).SingleAsync();
                if(cat.PoetId != poetId)
                {
                    catId = null;
                }
            }
            if (poetId != null && catId == null)
            {
                var poetRes = await GetPoetById((int)poetId);
                if (!string.IsNullOrEmpty(poetRes.ExceptionString))
                    return new RServiceResult<(PaginationMetadata PagingMeta, DivanPoemCompleteViewModel[] Items)>((null, null), poetRes.ExceptionString);
                catId = poetRes.Result.Cat.Id;
            }
            List<int> catIdList = new List<int>();
            if (catId != null)
            {
                catIdList.Add((int)catId);
                await _populateCategoryChildren(_context, (int)catId, catIdList);
            }

            var poems = _context.DivanPoems.AsQueryable();
            foreach (var pattern in searchPatterns)
                poems = poems.Where(p => EF.Functions.Like(p.PlainText, pattern));
            var source =
                poems
                .Where(p =>
                        (catId == null || catIdList.Contains(p.CatId))
                        &&
                        (exceptPoetId.Length == 0 || !exceptPoetId.Contains(p.Cat.PoetId))
                        )
                .Include(p => p.Cat).ThenInclude(c => c.Poet)
                .OrderBy(p => p.Cat.Poet.BirthYearInLHijri).ThenBy(p => p.Cat.Poet.Nickname).ThenBy(p => p.Id)
                .Select
                (
                    poem =>
                    new DivanPoemCompleteViewModel()
                    {
                        Id = poem.Id,
                        Title = poem.Title,
                        FullTitle = poem.FullTitle,
                        FullUrl = poem.FullUrl,
                        UrlSlug = poem.UrlSlug,
                        HtmlText = poem.HtmlText,
                        PlainText = poem.PlainText,
                        MixedModeOrder = poem.MixedModeOrder,
                        Published = poem.Published,
                        Language = poem.Language,
                        PoemSummary = poem.PoemSummary,
                        Category = new DivanPoetCompleteViewModel()
                        {
                            Poet = new DivanPoetViewModel()
                            {
                                Id = poem.Cat.Poet.Id,
                            }
                        },
                        ClaimedByMultiplePoets = poem.ClaimedByMultiplePoets,
                    }
                ).AsNoTracking();



            (PaginationMetadata PagingMeta, DivanPoemCompleteViewModel[] Items) paginatedResult =
               await QueryablePaginator<DivanPoemCompleteViewModel>.Paginate(source, paging);


            Dictionary<int, DivanPoetCompleteViewModel> cachedPoets = new Dictionary<int, DivanPoetCompleteViewModel>();

            foreach (var item in paginatedResult.Items)
            {
                if (cachedPoets.TryGetValue(item.Category.Poet.Id, out DivanPoetCompleteViewModel poet))
                {
                    item.Category = poet;
                }
                else
                {
                    poet = (await GetPoetById(item.Category.Poet.Id)).Result;

                    cachedPoets.Add(item.Category.Poet.Id, poet);

                    item.Category = poet;
                }

            }
            return new RServiceResult<(PaginationMetadata PagingMeta, DivanPoemCompleteViewModel[] Items)>(paginatedResult);
        }

        private async Task _UpdatePageChildrenTitleAndUrl(RMuseumDbContext context, DivanPage dbPage, bool messWithTitles, bool messWithUrls)
        {
            var children = await context.DivanPages.Where(p => p.ParentId == dbPage.Id).ToListAsync();
            foreach (var child in children)
            {
                child.FullUrl = dbPage.FullUrl + "/" + child.UrlSlug;
                child.FullTitle = dbPage.FullTitle + " » " + child.Title;

                switch (child.DivanPageType)
                {
                    case DivanPageType.PoemPage:
                        {
                            DivanPoem poem = await context.DivanPoems.Where(p => p.Id == child.Id).SingleAsync();
                            if (messWithTitles)
                                poem.FullTitle = child.FullTitle;
                            if (messWithUrls)
                                poem.FullUrl = child.FullUrl;

                            context.DivanPoems.Update(poem);
                        }
                        break;
                    case DivanPageType.CatPage:
                        {
                            if (messWithUrls)
                            {
                                DivanCat cat = await context.DivanCategories.Where(c => c.Id == child.CatId).SingleAsync();
                                cat.FullUrl = child.FullUrl;
                                context.DivanCategories.Update(cat);
                            }

                        }
                        break;
                }

                await _UpdatePageChildrenTitleAndUrl(context, child, messWithTitles, messWithUrls);

                CacheCleanForPageByUrl(child.FullUrl);
            }
            context.DivanPages.UpdateRange(children);
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// modify page
        /// </summary>
        /// <param name="id"></param>
        /// <param name="editingUserId"></param>
        /// <param name="pageData"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanPageCompleteViewModel>> UpdatePageAsync(int id, Guid editingUserId, DivanModifyPageViewModel pageData)
        {
            return await _UpdatePageAsync(_context, id, editingUserId, pageData, true);
        }

        /// <summary>
        /// modify poem => only these fields: NoIndex, RedirectFromFullUrl, MixedModeOrder
        /// </summary>
        /// <param name="id"></param>
        /// <param name="editingUserId"></param>
        /// <param name="pageData"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanPageCompleteViewModel>> UpdatePoemAsync(int id, Guid editingUserId, DivanModifyPageViewModel pageData)
        {
            return await _UpdatePoemAsync(_context, id, editingUserId, pageData, true);
        }

        /// <summary>
        /// break a poem from a verse forward
        /// </summary>
        /// <param name="poemId"></param>
        /// <param name="vOrder"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<int>> BreakPoemAsync(int poemId, int vOrder, Guid userId)
        {
            var poem = (await GetPoemById(poemId, true, false, true, false, false, false, false, true, true)).Result;
            var parentPage = await _context.DivanPages.AsNoTracking().Where(p => p.DivanPageType == DivanPageType.CatPage && p.CatId == poem.Category.Cat.Id).SingleOrDefaultAsync();
            if(parentPage == null)
            {
                parentPage = await _context.DivanPages.AsNoTracking().Where(p => p.DivanPageType == DivanPageType.PoetPage && p.CatId == poem.Category.Cat.Id).SingleAsync();
            }
            var poemTitleStaticPart = "شمارهٔ";
            if(!poem.Title.Contains(poemTitleStaticPart))
            {
                poemTitleStaticPart = "بخش";
            }
            if (poem.Next == null)
            {
                return await _BreakLastPoemInItsCategoryAsync(_context, poemId, vOrder, userId, poem, parentPage, poemTitleStaticPart);
            }

            _backgroundTaskQueue.QueueBackgroundWorkItem
                        (
                        async token =>
                        {
                            using (RMuseumDbContext context = new RMuseumDbContext(new DbContextOptions<RMuseumDbContext>())) //this is long running job, so _context might be already been freed/collected by GC
                            {
                                LongRunningJobProgressServiceEF jobProgressServiceEF = new LongRunningJobProgressServiceEF(context);
                                var job = (await jobProgressServiceEF.NewJob($"Breaking poem {poem.FullTitle}", "Query data")).Result;
                                try
                                {
                                    var res = await _BreakPoemAsync(context, poemId, vOrder, userId, poem, parentPage, poemTitleStaticPart);
                                    if (!string.IsNullOrEmpty(res.ExceptionString))
                                    {
                                        await jobProgressServiceEF.UpdateJob(job.Id, 100, "", false, res.ExceptionString);
                                        return;
                                    }

                                    await jobProgressServiceEF.UpdateJob(job.Id, 100, "", true);
                                }
                                catch (Exception exp)
                                {
                                    await jobProgressServiceEF.UpdateJob(job.Id, 100, "", false, exp.ToString());
                                }

                            }
                        });

            return new RServiceResult<int>(-1);
        }

        /// <summary>
        /// update related sections
        /// </summary>
        /// <param name="metreId"></param>
        /// <param name="rhyme"></param>
        public void UpdateRelatedSections(int metreId, string rhyme)
        {
            if (string.IsNullOrEmpty(rhyme))
                return;
            if (metreId <= 0)
                return;
            _backgroundTaskQueue.QueueBackgroundWorkItem
                                    (
                                    async token =>
                                    {
                                        using (RMuseumDbContext inlineContext = new RMuseumDbContext(new DbContextOptions<RMuseumDbContext>())) //this is long running job, so context might be already been freed/collected by GC
                                        {
                                            LongRunningJobProgressServiceEF jobProgressServiceEF = new LongRunningJobProgressServiceEF(inlineContext);
                                            var job = (await jobProgressServiceEF.NewJob($"بازسازی فهرست بخش‌های مرتبط", $"M: {metreId}, G: {rhyme}")).Result;

                                            try
                                            {
                                                await _UpdateRelatedSections(inlineContext, metreId, rhyme);
                                                await inlineContext.SaveChangesAsync();

                                                await jobProgressServiceEF.UpdateJob(job.Id, 100, "", true);
                                            }
                                            catch (Exception exp)
                                            {
                                                await jobProgressServiceEF.UpdateJob(job.Id, 100, "", false, exp.ToString());
                                            }
                                        }
                                    });
        }

        /// <summary>
        /// return page modifications history
        /// </summary>
        /// <param name="pageId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanPageSnapshotSummaryViewModel[]>> GetOlderVersionsOfPage(int pageId)
        {
            return
                new RServiceResult<DivanPageSnapshotSummaryViewModel[]>
                (
                    await _context.DivanPageSnapshots.AsNoTracking()
                                    .Where(s => s.DivanPageId == pageId)
                                    .OrderByDescending(s => s.RecordDate)
                                    .Select
                                    (
                                        s =>
                                            new DivanPageSnapshotSummaryViewModel()
                                            {
                                                Id = s.Id,
                                                RecordDate = s.RecordDate,
                                                Note = s.Note
                                            }
                                    )
                                    .ToArrayAsync()
                );
        }

        /// <summary>
        /// get old version
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanModifyPageViewModel>> GetOldVersionOfPage(int id)
        {
            return new RServiceResult<DivanModifyPageViewModel>
                (
                await _context.DivanPageSnapshots.AsNoTracking()
                              .Where(s => s.Id == id)
                              .Select
                              (
                                s =>
                                    new DivanModifyPageViewModel()
                                    {
                                        HtmlText = s.HtmlText,
                                        Note = s.Note,
                                        OldTag = s.OldTag,
                                        OldTagPageUrl = s.OldTagPageUrl,
                                        RhymeLetters = s.RhymeLetters,
                                        Rhythm = s.Rhythm,
                                        SourceName = s.SourceName,
                                        SourceUrlSlug = s.SourceUrlSlug,
                                        Title = s.Title,
                                        UrlSlug = s.UrlSlug
                                    }
                              )
                              .SingleOrDefaultAsync()
                );
        }

        /// <summary>
        /// returns metre list (ordered by Rhythm)
        /// </summary>
        /// <param name="sortOnVerseCount"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanMetre[]>> GetDivanMetres(bool sortOnVerseCount = false)
        {
            return new RServiceResult<DivanMetre[]>(
                sortOnVerseCount ?
                await _context.DivanMetres.OrderByDescending(m => m.VerseCount).AsNoTracking().ToArrayAsync()
                :
                await _context.DivanMetres.OrderBy(m => m.Rhythm).AsNoTracking().ToArrayAsync()
                );
        }

        private void CleanPoetCache(int poetId)
        {
            //cache clean:
            _memoryCache.Remove($"/api/divan/poets?published={true}&includeBio={false}");
            _memoryCache.Remove($"/api/divan/poets?published={false}&includeBio={true}");
            _memoryCache.Remove("divan/poets");
            _memoryCache.Remove($"/api/divan/poet/{poetId}");
            _memoryCache.Remove($"poet/byid/{poetId}");
        }

        /// <summary>
        /// modify poet
        /// </summary>
        /// <param name="poet"></param>
        /// <param name="editingUserId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> UpdatePoetAsync(DivanPoetViewModel poet, Guid editingUserId)
        {
            var dbPoet = await _context.DivanPoets.Where(p => p.Id == poet.Id).SingleAsync();
            var dbPoetPage = await _context.DivanPages.Where(page => page.PoetId == poet.Id && page.DivanPageType == DivanPageType.PoetPage).SingleAsync();
            if (string.IsNullOrEmpty(poet.Nickname))
            {
                poet.Nickname = dbPoet.Nickname;
                poet.Published = dbPoet.Published;
            }
            if (string.IsNullOrEmpty(poet.FullUrl))
                poet.FullUrl = dbPoetPage.FullUrl;
            if (string.IsNullOrEmpty(poet.Name))
                poet.Name = dbPoet.Name;
            if (string.IsNullOrEmpty(poet.Description))
                poet.Description = dbPoet.Description;

            if (dbPoet.Nickname != poet.Nickname || dbPoetPage.FullUrl != poet.FullUrl)
            {
                var resPageEdit =
                    await UpdatePageAsync
                    (
                    dbPoetPage.Id,
                    editingUserId,
                    new DivanModifyPageViewModel()
                    {
                        Title = poet.Nickname,
                        HtmlText = dbPoetPage.HtmlText,
                        Note = "ویرایش مستقیم مشخصات سخنور",
                        UrlSlug = poet.FullUrl.Substring(1),
                    }
                    );
                if (!string.IsNullOrEmpty(resPageEdit.ExceptionString))
                    new RServiceResult<bool>(false, resPageEdit.ExceptionString);

                dbPoet.Nickname = poet.Nickname;
            }
            dbPoet.Name = poet.Name;
            dbPoet.Description = poet.Description;
            bool publishedChange = dbPoet.Published != poet.Published;
            dbPoet.Published = poet.Published;
            dbPoet.BirthYearInLHijri = poet.BirthYearInLHijri;
            dbPoet.ValidBirthDate = poet.ValidBirthDate;
            dbPoet.DeathYearInLHijri = poet.DeathYearInLHijri;
            dbPoet.ValidDeathDate = poet.ValidDeathDate;
            dbPoet.PinOrder = poet.PinOrder;
            dbPoet.BirthLocationId = string.IsNullOrEmpty(poet.BirthPlace) ? null
                : (await _context.DivanGeoLocations.Where(l => l.Name == poet.BirthPlace).SingleAsync()).Id;
            dbPoet.DeathLocationId = string.IsNullOrEmpty(poet.DeathPlace) ? null
               : (await _context.DivanGeoLocations.Where(l => l.Name == poet.DeathPlace).SingleAsync()).Id;
            _context.DivanPoets.Update(dbPoet);
            await _context.SaveChangesAsync();

            if (publishedChange)
            {
                var pages = await _context.DivanPages.Where(p => p.PoemId == poet.Id).ToListAsync();
                foreach (var page in pages)
                {
                    page.Published = poet.Published;
                }

                _context.DivanPages.UpdateRange(pages);

                await _context.SaveChangesAsync();
            }

            CleanPoetCache(poet.Id);

            return new RServiceResult<bool>(true);
        }

        /// <summary>
        /// create new poet
        /// </summary>
        /// <param name="poet"></param>
        /// <param name="editingUserId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanPoetCompleteViewModel>> AddPoetAsync(DivanPoetViewModel poet, Guid editingUserId)
        {
            if (await _context.DivanPoets.Where(p => p.Nickname == poet.Nickname || p.Name == poet.Name).AnyAsync())
            {
                return new RServiceResult<DivanPoetCompleteViewModel>(null, "conflicting poet");
            }

            if (await _context.DivanCategories.Where(c => c.FullUrl == poet.FullUrl).AnyAsync())
            {
                return new RServiceResult<DivanPoetCompleteViewModel>(null, "conflicting cat");
            }

            if (await _context.DivanPages.Where(p => p.FullUrl == poet.FullUrl).AnyAsync())
            {
                return new RServiceResult<DivanPoetCompleteViewModel>(null, "conflicting page");
            }

            if (poet.FullUrl.IndexOf('/') != 0)
            {
                return new RServiceResult<DivanPoetCompleteViewModel>(null, "Invalid FullUrl, it must start with /");
            }

            if (poet.FullUrl.Substring(1).IndexOf('/') >= 0)
            {
                return new RServiceResult<DivanPoetCompleteViewModel>(null, "Invalid FullUrl, it must contain only one /");
            }

            var id = 1 + await _context.DivanPoets.MaxAsync(p => p.Id);

            for (int i = 2; i < id; i++)
            {
                if (!(await _context.DivanPoets.Where(p => p.Id == i).AnyAsync()))
                {
                    id = i;
                    break;
                }
            }

            if (string.IsNullOrEmpty(poet.Description))
                poet.Description = "";

            DivanPoet dbPoet = new DivanPoet()
            {
                Id = id,
                Name = poet.Name,
                Nickname = poet.Nickname,
                Description = poet.Description,
                Published = poet.Published,
                BirthYearInLHijri = poet.BirthYearInLHijri,
                ValidBirthDate = poet.ValidBirthDate,
                DeathYearInLHijri = poet.DeathYearInLHijri,
                ValidDeathDate = poet.ValidDeathDate,
                PinOrder = poet.PinOrder,
                BirthLocationId = string.IsNullOrEmpty(poet.BirthPlace) ? null
                : (await _context.DivanGeoLocations.Where(l => l.Name == poet.BirthPlace).SingleAsync()).Id,
                DeathLocationId = string.IsNullOrEmpty(poet.DeathPlace) ? null
               : (await _context.DivanGeoLocations.Where(l => l.Name == poet.DeathPlace).SingleAsync()).Id

            };

            _context.DivanPoets.Add(dbPoet);

            var poetCatId = 1 + await _context.DivanCategories.MaxAsync(c => c.Id);

            DivanCat dbCat = new DivanCat()
            {
                Id = poetCatId,
                PoetId = id,
                Title = poet.Nickname,
                UrlSlug = poet.FullUrl.Substring(1),
                FullUrl = poet.FullUrl,
                TableOfContentsStyle = DivanTOC.Analyse,
                Published = true,
            };
            _context.DivanCategories.Add(dbCat);

            var poetPageId = 1 + await _context.DivanPages.MaxAsync(p => p.Id);
            while (await _context.DivanPoems.Where(p => p.Id == poetPageId).AnyAsync())
                poetPageId++;

            var pageText = "";
            foreach (var line in poet.Description.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries))
            {
                pageText += $"<p>{line}</p>{Environment.NewLine}";
            }

            DivanPage dbPage = new DivanPage()
            {
                Id = poetPageId,
                DivanPageType = DivanPageType.PoetPage,
                Published = poet.Published,
                PageOrder = -1,
                Title = poet.Nickname,
                FullTitle = poet.Nickname,
                UrlSlug = poet.FullUrl.Substring(1),
                FullUrl = poet.FullUrl,
                HtmlText = pageText,
                PoetId = id,
                CatId = poetCatId,
                PostDate = DateTime.Now
            };

            _context.DivanPages.Add(dbPage);

            DivanPageSnapshot snapshot = new DivanPageSnapshot()
            {
                DivanPageId = poetPageId,
                MadeObsoleteByUserId = editingUserId,
                RecordDate = DateTime.Now,
                Note = "ایجاد سخنور",
                Title = dbPage.Title,
                UrlSlug = dbPage.UrlSlug,
                HtmlText = dbPage.HtmlText,
            };

            _context.DivanPageSnapshots.Add(snapshot);

            await _context.SaveChangesAsync();

            CleanPoetCache(0);

            return await GetPoetById(id);
        }

        /// <summary>
        /// delete poet
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public RServiceResult<bool> StartDeletePoet(int id)
        {
            _backgroundTaskQueue.QueueBackgroundWorkItem
                        (
                        async token =>
                        {
                            using (RMuseumDbContext context = new RMuseumDbContext(new DbContextOptions<RMuseumDbContext>())) //this is long running job, so _context might be already been freed/collected by GC
                            {
                                LongRunningJobProgressServiceEF jobProgressServiceEF = new LongRunningJobProgressServiceEF(context);
                                var job = (await jobProgressServiceEF.NewJob($"Deleting Poet {id}", "Query data")).Result;
                                try
                                {
                                    var pages = await context.DivanPages.Where(p => p.PoetId == id).ToListAsync();
                                    context.DivanPages.RemoveRange(pages);
                                    await jobProgressServiceEF.UpdateJob(job.Id, 50, "Deleting page and Querying the poet - if no progress unplublish and regen group by centuries");
                                    var poet = await context.DivanPoets.Where(p => p.Id == id).SingleAsync();
                                    context.DivanPoets.Remove(poet);
                                    await jobProgressServiceEF.UpdateJob(job.Id, 99, "Deleting poet");
                                    await context.SaveChangesAsync();
                                    await jobProgressServiceEF.UpdateJob(job.Id, 100, "", true);
                                }
                                catch (Exception exp)
                                {
                                    await jobProgressServiceEF.UpdateJob(job.Id, 100, "", false, exp.ToString());
                                }

                            }
                        });

            return new RServiceResult<bool>(true);
        }

        /// <summary>
        /// delete a page
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> DeletePageAsync(int id)
        {
            var firstChild = await _context.DivanPages.Where(p => p.ParentId == id).FirstOrDefaultAsync();
            if (firstChild != null)
            {
                return new RServiceResult<bool>(false, "Please delete children of the page first.");
            }
            var page = await _context.DivanPages.Where(p => p.Id == id).SingleAsync();
            if (page.PoemId != null)
            {
                return new RServiceResult<bool>(false, "Poem related pages can not be deleted.");
            }
            var cat = await _context.DivanCategories.Where(c => c.FullUrl == page.FullUrl).FirstOrDefaultAsync();
            if (cat != null)
            {
                return new RServiceResult<bool>(false, "Category related pages can not be deleted.");
            }
            _context.DivanPages.Remove(page);
            await _context.SaveChangesAsync();
            return new RServiceResult<bool>(true);
        }


        /// <summary>
        /// delete a poem
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> DeletePoemAsync(int id)
        {
            try
            {
                var poem = await _context.DivanPoems.Where(p => p.Id == id).SingleAsync();


                var comments = await _context.DivanComments.Where(c => c.PoemId == id).ToListAsync();
                _context.RemoveRange(comments);

                var music = await _context.DivanPoemMusicTracks.Where(m => m.PoemId == id).ToListAsync();
                _context.RemoveRange(music);

                //these lines cause timeout, so I commented them:
                /*
                var similars = await _context.DivanCachedRelatedSections.Where(s => s.FullUrl.Contains(poem.FullUrl)).ToListAsync();
                _context.RemoveRange(similars);
                */
                var corrections = await _context.DivanPoemCorrections.Include(c => c.VerseOrderText).Where(c => c.PoemId == id).ToListAsync();
                _context.RemoveRange(corrections);

                var page = await _context.DivanPages.Where(p => p.Id == id && p.DivanPageType == DivanPageType.PoemPage).SingleOrDefaultAsync();
                if(page != null)
                {
                    _context.Remove(page);
                }

                _context.Remove(poem);

                await _context.SaveChangesAsync();

                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// delete a category
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> DeleteCategoryAsync(int id)
        {
            try
            {
                var poems = await _context.DivanPoems.Where(p => p.CatId == id).ToListAsync();
                foreach (var poem in poems)
                {
                    var res = await DeletePoemAsync(poem.Id);
                    if (!string.IsNullOrEmpty(res.ExceptionString))
                        return res;
                }
                var subCats = await _context.DivanCategories.Where(c => c.ParentId == id).ToListAsync();
                foreach (var subCat in subCats)
                {
                    var res = await DeleteCategoryAsync(subCat.Id);
                    if (!string.IsNullOrEmpty(res.ExceptionString))
                        return res;
                }

                var page = await _context.DivanPages.Where(p => p.DivanPageType == DivanPageType.CatPage && p.CatId == id).SingleAsync();
                _context.Remove(page);
                await _context.SaveChangesAsync();

                var cat = await _context.DivanCategories.Where(c => c.Id == id).SingleAsync();
                _context.Remove(cat);
                await _context.SaveChangesAsync();

                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// chaneg poet image
        /// </summary>
        /// <param name="poetId"></param>
        /// <param name="imageId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> ChangePoetImageAsync(int poetId, Guid imageId)
        {
            try
            {
                RServiceResult<RImage> img =
                   await _imageFileService.GetImage(imageId);
                if (!string.IsNullOrEmpty(img.ExceptionString))
                {
                    return new RServiceResult<bool>(false, img.ExceptionString);
                }
                if (bool.Parse(Configuration.GetSection("ExternalFTPServer")["UploadEnabled"]))
                {
                    var ftpClient = new AsyncFtpClient
                                        (
                                            Configuration.GetSection("ExternalFTPServer")["Host"],
                                            Configuration.GetSection("ExternalFTPServer")["Username"],
                                            Configuration.GetSection("ExternalFTPServer")["Password"]
                                        );
                    ftpClient.ValidateCertificate += FtpClient_ValidateCertificate;
                    await ftpClient.AutoConnect();
                    ftpClient.Config.RetryAttempts = 3;
                    RServiceResult<string> imgPath = _imageFileService.GetImagePath(img.Result);
                    if (!string.IsNullOrEmpty(imgPath.ExceptionString))
                        return new RServiceResult<bool>(false, imgPath.ExceptionString);

                    var localFilePath = imgPath.Result;
                    var remoteFilePath = $"{Configuration.GetSection("ExternalFTPServer")["RootPath"]}/images/PoetImages/{Path.GetFileName(localFilePath)}";
                    await ftpClient.UploadFile(localFilePath, remoteFilePath);
                    await ftpClient.Disconnect();
                }
                if (bool.Parse(Configuration.GetSection("BackupFTPServer")["UploadEnabled"]))
                {
                    var ftpClient = new AsyncFtpClient
                                        (
                                            Configuration.GetSection("BackupFTPServer")["Host"],
                                            Configuration.GetSection("BackupFTPServer")["Username"],
                                            Configuration.GetSection("BackupFTPServer")["Password"]
                                        );
                    ftpClient.ValidateCertificate += FtpClient_ValidateCertificate;
                    await ftpClient.AutoConnect();
                    ftpClient.Config.RetryAttempts = 3;
                    RServiceResult<string> imgPath = _imageFileService.GetImagePath(img.Result);
                    if (!string.IsNullOrEmpty(imgPath.ExceptionString))
                        return new RServiceResult<bool>(false, imgPath.ExceptionString);

                    var localFilePath = imgPath.Result;
                    var remoteFilePath = $"{Configuration.GetSection("BackupFTPServer")["RootPath"]}/images/PoetImages/{Path.GetFileName(localFilePath)}";
                    await ftpClient.UploadFile(localFilePath, remoteFilePath);
                    await ftpClient.Disconnect();
                }
                var dbPoet = await _context.DivanPoets.Where(p => p.Id == poetId).SingleAsync();
                dbPoet.RImageId = imageId;
                _context.DivanPoets.Update(dbPoet);
                await _context.SaveChangesAsync();
                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        private void FtpClient_ValidateCertificate(FluentFTP.Client.BaseClient.BaseFtpClient control, FtpSslValidationEventArgs e)
        {
            e.Accept = true;
        }






        /// <summary>
        /// find poem rhyme
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanRhymeAnalysisResult>> FindPoemMainSectionRhyme(int id)
        {
            var section = await _context.DivanPoemSections.AsNoTracking().Where(s => s.PoemId == id && s.SectionType == PoemSectionType.WholePoem && s.VerseType == VersePoemSectionType.First).OrderBy(s => s.Index).FirstOrDefaultAsync();
            if (section == null)
                return new RServiceResult<DivanRhymeAnalysisResult>(null, "no sections");
            return await FindSectionRhyme(section.Id);
        }

        /// <summary>
        /// find poem section rhyme
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanRhymeAnalysisResult>> FindSectionRhyme(int id)
        {
            return await _FindSectionRhyme(_context, id);
        }


        private async Task<RServiceResult<DivanRhymeAnalysisResult>> _FindSectionRhyme(RMuseumDbContext context, int id)
        {
            var section = await context.DivanPoemSections.Include(s => s.DivanMetre).AsNoTracking().Where(s => s.Id == id).FirstOrDefaultAsync();
            if (section == null)
                return new RServiceResult<DivanRhymeAnalysisResult>(null, "no sections");
            var verses = await context.DivanVerses.AsNoTracking().Where(v => v.PoemId == section.PoemId).OrderBy(v => v.VOrder).ToListAsync();
            var rhymeAnalysisResult = LanguageUtils.FindRhyme(FilterSectionVerses(section, verses));
            if (rhymeAnalysisResult.Rhyme.Length > 30 && verses.Count == 2 && section.DivanMetre != null)//single verse
            {
                var rhymingSection = await context.DivanPoemSections.AsNoTracking()
                                        .Where(s => s.DivanMetreId == section.DivanMetreId && section.RhymeLetters != null && s.RhymeLetters.Length < 15 && rhymeAnalysisResult.Rhyme.Contains(s.RhymeLetters))
                                        .OrderByDescending(s => s.RhymeLetters.Length)
                                        .FirstOrDefaultAsync();
                if (rhymingSection != null)
                {
                    rhymeAnalysisResult.Rhyme = rhymingSection.RhymeLetters;
                }
            }
            return new RServiceResult<DivanRhymeAnalysisResult>(rhymeAnalysisResult);
        }



        /// <summary>
        /// find poem rhythm
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<string>> FindPoemMainSectionRhythm(int id)
        {
            var metres = (await GetDivanMetres()).Result.Select(m => m.Rhythm).ToArray();
            return await _FindPoemMainSectionRhythm(id, _context, _httpClient, metres);
        }


        private async Task<RServiceResult<string>> _FindPoemMainSectionRhythm(int id, RMuseumDbContext context, HttpClient httpClient, string[] metres, bool alwaysReturnaAResult = false)
        {
            var section = await context.DivanPoemSections.AsNoTracking().Where(s => s.PoemId == id && s.SectionType == PoemSectionType.WholePoem && s.VerseType == VersePoemSectionType.First).OrderBy(s => s.Index).FirstOrDefaultAsync();
            if (section == null)
                return new RServiceResult<string>(null, "no main sections");
            return await _FindSectionRhythm(section, context, httpClient, metres, alwaysReturnaAResult);
        }
        private async Task<RServiceResult<string>> _FindSectionRhythm(DivanPoemSection section, RMuseumDbContext context, HttpClient httpClient, string[] metres, bool alwaysReturnaAResult = false)
        {
            try
            {
                var poemVerses = await context.DivanVerses.AsNoTracking().Where(v => v.PoemId == section.PoemId).OrderBy(v => v.VOrder).ToListAsync();
                var verses = FilterSectionVerses(section, poemVerses);
                if (verses.Any(v => v.VersePosition == VersePosition.Paragraph))
                {
                    return new RServiceResult<string>("paragraph");
                }

                Dictionary<string, int> rhytmCounter = new Dictionary<string, int>();

                for (int i = 0; i < verses.Count; i++)
                {
                    var verse = verses[i];

                    try
                    {
                        var response = await httpClient.GetAsync($"http://sorud.info/?Text={HttpUtility.UrlEncode(LanguageUtils.MakeTextSearchable(verse.Text))}");
                        response.EnsureSuccessStatusCode();
                        string result = await response.Content.ReadAsStringAsync();
                        if (result.IndexOf("آهنگِ همه‌ی بندها شناسایی نشد.") != -1)
                        {
                            continue;
                        }
                        int nVaznIndex = result.IndexOf("ctl00_MainContent_lblMetricBottom");
                        if (nVaznIndex == -1)
                        {
                            continue;
                        }
                        nVaznIndex += 2;
                        int nQuote1Index = result.IndexOf('\'', nVaznIndex);
                        if (nQuote1Index == -1)
                        {
                            continue;
                        }

                        int nQuote2Index = result.IndexOf('\'', nQuote1Index + 1);
                        if (nQuote2Index == -1)
                        {
                            continue;
                        }

                        string strRokn = result.Substring(nQuote1Index + 1, nQuote2Index - nQuote1Index - 1);

                        int nSpanClose = result.IndexOf("</span>", nQuote2Index + 1);

                        if (nSpanClose == -1)
                        {
                            continue;
                        }

                        int nFQ1 = result.IndexOf('«', nQuote2Index + 1);

                        if (nFQ1 == -1)
                        {
                            continue;
                        }


                        string rhythm = metres.Where(m => m.IndexOf(strRokn) == 0).SingleOrDefault();

                        if (string.IsNullOrEmpty(rhythm))
                            continue;

                        if (rhythm == "فاعلاتن فاعلن فاعلاتن فاعلن")
                            rhythm = "فاعلاتن فاعلاتن فاعلاتن فاعلن (رمل مثمن محذوف)";

                        if (rhythm == "مفاعلتن مفاعلتن مفاعلتن مفاعلتن")
                            rhythm = "مفاعیلن مفاعیلن مفاعیلن مفاعیلن (هزج مثمن سالم)";

                        if (rhythm == "فاعلات مفعولن فاعلات مفعولن")
                            rhythm = "فاعلن مفاعیلن فاعلن مفاعیلن (مقتضب مثمن مطوی مقطوع)";

                        if (rhytmCounter.TryGetValue(rhythm, out int count))
                        {
                            count++;
                            if (count > 10 || (count * 100.0 / verses.Count > 60))
                            {
                                return new RServiceResult<string>(rhythm);
                            }

                        }
                        else
                        {
                            count = 1;
                        }
                        rhytmCounter[rhythm] = count;
                    }
                    catch
                    {
                        //continue
                    }
                }

                if (alwaysReturnaAResult)
                {
                    int maxCount = -1;
                    string rhytm = "";
                    foreach (var r in rhytmCounter)
                    {
                        if (r.Value > maxCount)
                        {
                            maxCount = r.Value;
                            rhytm = r.Key;
                        }
                    }

                    if (!string.IsNullOrEmpty(rhytm))
                        return new RServiceResult<string>(rhytm);

                    if (verses.Count < 9)
                        return new RServiceResult<string>("paragraph");
                }

                return new RServiceResult<string>("");
            }
            catch (Exception exp)
            {
                return new RServiceResult<string>(null, exp.ToString());
            }
        }



        /// <summary>
        /// manually add a duplicate for a poems
        /// </summary>
        /// <param name="srcCatId"></param>
        /// <param name="srcPoemId"></param>
        /// <param name="destPoemId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> AdDuplicateAsync(int srcCatId, int srcPoemId, int destPoemId)
        {
            try
            {
                var alreadyDup = await _context.DivanDuplicates.AsNoTracking().Where(p => p.SrcPoemId == srcPoemId).FirstOrDefaultAsync();
                if (alreadyDup != null)
                {
                    return new RServiceResult<bool>(false, $"already dupped : {alreadyDup.DestPoemId}");
                }
                var dup = new DivanDuplicate()
                {
                    SrcCatId = srcCatId,
                    SrcPoemId = srcPoemId,
                    DestPoemId = destPoemId
                };
                _context.DivanDuplicates.Add(dup);
                await _context.SaveChangesAsync();

                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// delete duplicate
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> DeleteDuplicateAsync(int id)
        {
            try
            {
                var dup = await _context.DivanDuplicates.Where(d => d.Id == id).SingleAsync();
                _context.Remove(dup);
                await _context.SaveChangesAsync();
                return new RServiceResult<bool>(false);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// get category poem related images
        /// </summary>
        /// <param name="catId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<PoemRelatedImageEx[]>> GetCatPoemImagesAsync(int catId)
        {
            try
            {
                var museumSrc =
                 from link in _context.DivanLinks.Include(l => l.Artifact).Include(l => l.Item).ThenInclude(i => i.Images)
                 join poem in _context.DivanPoems
                 on link.DivanPostId equals poem.Id
                 where
                 link.DisplayOnPage == true
                 &&
                 link.ReviewResult == ReviewResult.Approved
                 &&
                 poem.CatId == catId
                 orderby poem.Id
                 select new PoemRelatedImageEx()
                 {
                     PoemRelatedImageType = PoemRelatedImageType.MuseumLink,
                     ThumbnailImageUrl = link.Item.Images.First().ExternalNormalSizeImageUrl.Replace("/norm/", "/thumb/").Replace("/orig/", "/thumb/"),
                     TargetPageUrl = link.LinkToOriginalSource ? link.OriginalSourceUrl : $"https://museum.ganjoor.net/items/{link.Artifact.FriendlyUrl}/{link.Item.FriendlyUrl}",
                     AltText = $"{link.Artifact.Name} » {link.Item.Name}",
                     IsTextOriginalSource = link.IsTextOriginalSource,
                     PoemId = poem.Id,
                     PoemFullUrl = poem.FullUrl,
                     PoemFullTitle = poem.FullTitle,
                 };
                List<PoemRelatedImageEx> museumImages = await museumSrc.ToListAsync();

                var externalSrc =
                     from link in _context.PinterestLinks
                     join poem in _context.DivanPoems
                     on link.DivanPostId equals poem.Id
                     where
                     link.ReviewResult == ReviewResult.Approved
                     &&
                     poem.CatId == catId
                     orderby poem.Id
                     select new PoemRelatedImageEx()
                     {
                         PoemRelatedImageType = PoemRelatedImageType.ExternalLink,
                         ThumbnailImageUrl = link.LinkType == LinkType.Naskban ? link.PinterestImageUrl : link.Item.Images.First().ExternalNormalSizeImageUrl.Replace("/norm/", "/thumb/").Replace("/orig/", "/thumb/"),
                         TargetPageUrl = link.PinterestUrl,
                         AltText = link.AltText,
                         IsTextOriginalSource = link.IsTextOriginalSource,
                         PoemId = poem.Id,
                         PoemFullUrl = poem.FullUrl,
                         PoemFullTitle = poem.FullTitle,
                     };

                museumImages.AddRange(await externalSrc.AsNoTracking().ToListAsync());

                return new RServiceResult<PoemRelatedImageEx[]>(museumImages.ToArray());
            }
            catch (Exception exp)
            {
                return new RServiceResult<PoemRelatedImageEx[]>(null, exp.ToString());
            }

        }

        /// <summary>
        /// add page
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanPage>> AddPageAsync(DivanPage page)
        {
            try
            {
                if (page.UrlSlug.Contains("/"))
                    return new RServiceResult<DivanPage>(null, "slug could not contain /");
                page.UrlSlug = page.UrlSlug.Trim();
                page.Title = page.Title.Trim();
                if (string.IsNullOrEmpty(page.UrlSlug))
                    return new RServiceResult<DivanPage>(null, "empty url");
                if (string.IsNullOrEmpty(page.Title))
                    return new RServiceResult<DivanPage>(null, "empty title");
                var newPageId = 1 + await _context.DivanPages.MaxAsync(p => p.Id);
                while (await _context.DivanPoems.Where(p => p.Id == newPageId).AnyAsync())
                    newPageId++;
                page.Id = newPageId;
                if(page.ParentId != null)
                {
                    var parentPage = await _context.DivanPages.AsNoTracking().Where(p => p.Id == page.ParentId).SingleAsync();
                    page.FullUrl = parentPage.FullUrl + "/" + page.UrlSlug;
                    page.FullTitle = parentPage.FullTitle + " » " + page.Title;
                }
                if (true == await _context.DivanPages.Where(p => p.FullUrl == page.FullUrl).AnyAsync())
                    return new RServiceResult<DivanPage>(null, "duplicated full url.");
                if (true == await _context.DivanPages.Where(p => p.FullTitle == page.FullTitle).AnyAsync())
                    return new RServiceResult<DivanPage>(null, "duplicated full title");
                _context.Add(page);
                await _context.SaveChangesAsync();
                return new RServiceResult<DivanPage>(page);
            }
            catch (Exception exp)
            {
                return new RServiceResult<DivanPage>(null, exp.ToString());
            }
        }


        /// <summary>
        /// send cat correction
        /// </summary>
        /// <param name="correction"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanCatCorrectionViewModel>> SuggestCatCorrectionAsync(DivanCatCorrectionViewModel correction)
        {

            var preCorrections = await _context.DivanCatCorrections
                .Where(c => c.UserId == correction.UserId && c.CatId == correction.CatId && c.Reviewed == false)
                .ToListAsync();

            var cat = await _context.DivanCategories.AsNoTracking().Where(c => c.Id == correction.CatId).SingleAsync();

            var page = await _context.DivanPages.AsNoTracking().Where(p => p.FullUrl == cat.FullUrl).SingleAsync();

            DivanCatCorrection dbCorrection = new DivanCatCorrection()
            {
                CatId = correction.CatId,
                UserId = correction.UserId,
                DescriptionHtml = correction.DescriptionHtml,
                Description = correction.Description,
                OriginalDescription = cat.Description,
                OriginalDescriptionHtml = cat.DescriptionHtml,
                Note = correction.Note,
                Date = DateTime.Now,
                Result = CorrectionReviewResult.NotReviewed,
                Reviewed = false,
                AffectedTheCat = false,
                HideMyName = correction.HideMyName,
                PageId = page.Id,
            };
            _context.DivanCatCorrections.Add(dbCorrection);
            await _context.SaveChangesAsync();
            correction.Id = dbCorrection.Id;

            if (preCorrections.Count > 0)
            {
                _context.DivanCatCorrections.RemoveRange(preCorrections);
                await _context.SaveChangesAsync();
            }

            var moderators = await _appUserService.GetUsersHavingPermission(RMuseumSecurableItem.DivanEntityShortName, RMuseumSecurableItem.ModerateOperationShortName);
            if (string.IsNullOrEmpty(moderators.ExceptionString)) //if not, do nothing!
            {
                foreach (var moderator in moderators.Result)
                {
                    await _notificationService.PushNotification
                                    (
                                        (Guid)moderator.Id,
                                        "پیشنهاد متن بخش یا زندگینامه",
                                        $"کاربری ویرایشی را برای یک بخش یا زندگینامهٔ یک شاعر پیشنهاد داده است. لطفاً بخش <a href=\"https://ganjoor.net/Admin/ReviewCatEdits\">ویرایش‌های بخش‌ها</a> را بررسی فرمایید.{Environment.NewLine}" +
                                        $"توجه فرمایید که اگر کاربر دیگری که دارای مجوز بررسی ویرایش‌های بخش‌های پیشنهادی است پیش از شما به آن رسیدگی کرده باشد آن را در صف نخواهید دید.",
                                        NotificationType.ActionRequired
                                    );
                }
            }

            return new RServiceResult<DivanCatCorrectionViewModel>(correction);
        }

        /// <summary>
        /// delete unreviewed user corrections for a cat
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="catId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> DeleteCatCorrectionsAsync(Guid userId, int catId)
        {
            var preCorrections = await _context.DivanCatCorrections
                .Where(c => c.UserId == userId && c.CatId == catId && c.Reviewed == false)
                .ToListAsync();
            if (preCorrections.Count > 0)
            {
                _context.DivanCatCorrections.RemoveRange(preCorrections);
                await _context.SaveChangesAsync();
            }
            return new RServiceResult<bool>(true);
        }

        /// <summary>
        /// last unreviewed user correction for a cat
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="catId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanCatCorrectionViewModel>> GetLastUnreviewedUserCorrectionForCatAsync(Guid userId, int catId)
        {
            var dbCorrection = await _context.DivanCatCorrections.AsNoTracking().Include(c => c.User)
                .Where(c => c.UserId == userId && c.CatId == catId && c.Reviewed == false)
                .OrderByDescending(c => c.Id)
                .FirstOrDefaultAsync();

            if (dbCorrection == null)
                return new RServiceResult<DivanCatCorrectionViewModel>(null);

            return new RServiceResult<DivanCatCorrectionViewModel>
                (
                new DivanCatCorrectionViewModel()
                {
                    Id = dbCorrection.Id,
                    CatId = dbCorrection.CatId,
                    UserId = dbCorrection.UserId,
                    Description = dbCorrection.Description,
                    DescriptionHtml = dbCorrection.DescriptionHtml,
                    OriginalDescription = dbCorrection.OriginalDescription,
                    OriginalDescriptionHtml = dbCorrection.OriginalDescriptionHtml,
                    Note = dbCorrection.Note,
                    Date = dbCorrection.Date,
                    Reviewed = dbCorrection.Reviewed,
                    Result = dbCorrection.Result,
                    ReviewNote = dbCorrection.ReviewNote,
                    ReviewDate = dbCorrection.ReviewDate,
                    UserNickname = dbCorrection.HideMyName && dbCorrection.Reviewed ? "" : string.IsNullOrEmpty(dbCorrection.User.NickName) ? dbCorrection.User.Id.ToString() : dbCorrection.User.NickName,
                    HideMyName = dbCorrection.HideMyName,
                    PageId = dbCorrection.PageId,
                }
                );
        }

        /// <summary>
        /// get user or all corrections for categories
        /// </summary>
        /// <param name="userId">if sent empty returns all corrections</param>
        /// <param name="paging"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, DivanCatCorrectionViewModel[] Items)>> GetUserCatCorrectionsAsync(Guid userId, PagingParameterModel paging)
        {
            var source = from dbCorrection in
                             _context.DivanCatCorrections.AsNoTracking().Include(c => c.User)
                         where userId == Guid.Empty || dbCorrection.UserId == userId
                         orderby dbCorrection.Id descending
                         select
                          dbCorrection;

            (PaginationMetadata PagingMeta, DivanCatCorrection[] Items) dbPaginatedResult =
                await QueryablePaginator<DivanCatCorrection>.Paginate(source, paging);

            List<DivanCatCorrectionViewModel> list = new List<DivanCatCorrectionViewModel>();
            foreach (var dbCorrection in dbPaginatedResult.Items)
            {
                list.Add
                    (
                new DivanCatCorrectionViewModel()
                {
                    Id = dbCorrection.Id,
                    CatId = dbCorrection.CatId,
                    UserId = dbCorrection.UserId,
                    Description = dbCorrection.Description,
                    DescriptionHtml = dbCorrection.DescriptionHtml,
                    OriginalDescription = dbCorrection.OriginalDescription,
                    OriginalDescriptionHtml = dbCorrection.OriginalDescriptionHtml,
                    Note = dbCorrection.Note,
                    Date = dbCorrection.Date,
                    Reviewed = dbCorrection.Reviewed,
                    Result = dbCorrection.Result,
                    ReviewNote = dbCorrection.ReviewNote,
                    ReviewDate = dbCorrection.ReviewDate,
                    UserNickname = dbCorrection.HideMyName && dbCorrection.Reviewed ? "" : string.IsNullOrEmpty(dbCorrection.User.NickName) ? dbCorrection.User.Id.ToString() : dbCorrection.User.NickName,
                    HideMyName = dbCorrection.HideMyName,
                    PageId = dbCorrection.PageId,
                }
                );
            }

            return new RServiceResult<(PaginationMetadata, DivanCatCorrectionViewModel[])>
                ((dbPaginatedResult.PagingMeta, list.ToArray()));
        }


        /// <summary>
        /// cat effectinve corrections
        /// </summary>
        /// <param name="catId"></param>
        /// <param name="paging"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, DivanCatCorrectionViewModel[] Items)>> GetCatEffectiveCorrectionsAsync(int catId, PagingParameterModel paging)
        {
            var source = from dbCorrection in
                             _context.DivanCatCorrections.AsNoTracking()
                         where
                         dbCorrection.CatId == catId
                         &&
                         dbCorrection.Reviewed == true
                         &&
                         dbCorrection.Result == CorrectionReviewResult.Approved
                         orderby dbCorrection.Id descending
                         select
                         dbCorrection;

            (PaginationMetadata PagingMeta, DivanCatCorrection[] Items) dbPaginatedResult =
                await QueryablePaginator<DivanCatCorrection>.Paginate(source, paging);

            List<DivanCatCorrectionViewModel> list = new List<DivanCatCorrectionViewModel>();
            foreach (var dbCorrection in dbPaginatedResult.Items)
            {
                list.Add
                    (
                new DivanCatCorrectionViewModel()
                {
                    Id = dbCorrection.Id,
                    CatId = dbCorrection.CatId,
                    UserId = dbCorrection.UserId,
                    Description = dbCorrection.Description,
                    DescriptionHtml = dbCorrection.DescriptionHtml,
                    OriginalDescription = dbCorrection.OriginalDescription,
                    OriginalDescriptionHtml = dbCorrection.OriginalDescriptionHtml,
                    Note = dbCorrection.Note,
                    Date = dbCorrection.Date,
                    Reviewed = dbCorrection.Reviewed,
                    Result = dbCorrection.Result,
                    ReviewNote = dbCorrection.ReviewNote,
                    ReviewDate = dbCorrection.ReviewDate,
                    UserNickname = dbCorrection.HideMyName && dbCorrection.Reviewed ? "" : string.IsNullOrEmpty(dbCorrection.User.NickName) ? dbCorrection.User.Id.ToString() : dbCorrection.User.NickName,
                    HideMyName = dbCorrection.HideMyName,
                    PageId = dbCorrection.PageId,
                }
                );
            }

            return new RServiceResult<(PaginationMetadata, DivanCatCorrectionViewModel[])>
                ((dbPaginatedResult.PagingMeta, list.ToArray()));
        }

        /// <summary>
        /// get cat correction by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanCatCorrectionViewModel>> GetCatCorrectionByIdAsync(int id)
        {
            var dbCorrection = await _context.DivanCatCorrections.AsNoTracking().Include(c => c.User)
                .Where(c => c.Id == id)
                .FirstOrDefaultAsync();

            if (dbCorrection == null)
                return new RServiceResult<DivanCatCorrectionViewModel>(null);

            return new RServiceResult<DivanCatCorrectionViewModel>
                (
                new DivanCatCorrectionViewModel()
                {
                    Id = dbCorrection.Id,
                    CatId = dbCorrection.CatId,
                    UserId = dbCorrection.UserId,
                    Description = dbCorrection.Description,
                    DescriptionHtml = dbCorrection.DescriptionHtml,
                    OriginalDescription = dbCorrection.OriginalDescription,
                    OriginalDescriptionHtml = dbCorrection.OriginalDescriptionHtml,
                    Note = dbCorrection.Note,
                    Date = dbCorrection.Date,
                    Reviewed = dbCorrection.Reviewed,
                    Result = dbCorrection.Result,
                    ReviewNote = dbCorrection.ReviewNote,
                    ReviewDate = dbCorrection.ReviewDate,
                    UserNickname = dbCorrection.HideMyName && dbCorrection.Reviewed ? "" : string.IsNullOrEmpty(dbCorrection.User.NickName) ? dbCorrection.User.Id.ToString() : dbCorrection.User.NickName,
                    HideMyName = dbCorrection.HideMyName,
                    PageId = dbCorrection.PageId,
                }
                );
        }


        /// <summary>
        /// get next unreviewed cat correction
        /// </summary>
        /// <param name="skip"></param>
        /// <param name="onlyUserCorrections"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanCatCorrectionViewModel>> GetNextUnreviewedCatCorrectionAsync(int skip, bool onlyUserCorrections)
        {
            string systemEmail = $"{Configuration.GetSection("Divan")["SystemEmail"]}";
            var systemUser = await _appUserService.FindUserByEmail(systemEmail);
            var systemUserId = systemUser.Result == null ? Guid.Empty : (Guid)systemUser.Result.Id;

            var dbCorrection = await _context.DivanCatCorrections.AsNoTracking().Include(c => c.User)
                .Where(c => c.Reviewed == false && (onlyUserCorrections == false || c.UserId != systemUserId))
                .OrderBy(c => c.Id)
                .Skip(skip)
                .FirstOrDefaultAsync();

            if (dbCorrection == null)
                return new RServiceResult<DivanCatCorrectionViewModel>(null);

            return new RServiceResult<DivanCatCorrectionViewModel>
                (
                new DivanCatCorrectionViewModel()
                {
                    Id = dbCorrection.Id,
                    CatId = dbCorrection.CatId,
                    UserId = dbCorrection.UserId,
                    Description = dbCorrection.Description,
                    DescriptionHtml = dbCorrection.DescriptionHtml,
                    OriginalDescription = dbCorrection.OriginalDescription,
                    OriginalDescriptionHtml = dbCorrection.OriginalDescriptionHtml,
                    Note = dbCorrection.Note,
                    Date = dbCorrection.Date,
                    Reviewed = dbCorrection.Reviewed,
                    Result = dbCorrection.Result,
                    ReviewNote = dbCorrection.ReviewNote,
                    ReviewDate = dbCorrection.ReviewDate,
                    UserNickname = dbCorrection.HideMyName && dbCorrection.Reviewed ? "" : string.IsNullOrEmpty(dbCorrection.User.NickName) ? dbCorrection.User.Id.ToString() : dbCorrection.User.NickName,
                    HideMyName = dbCorrection.HideMyName,
                    PageId = dbCorrection.PageId,
                }
                );
        }

        /// <summary>
        /// unreviewed cat corrections count
        /// </summary>
        /// <param name="onlyUserCorrections"></param>
        /// <returns></returns>
        public async Task<RServiceResult<int>> GetUnreviewedCatCorrectionCountAsync(bool onlyUserCorrections)
        {
            string systemEmail = $"{Configuration.GetSection("Divan")["SystemEmail"]}";
            var systemUser = await _appUserService.FindUserByEmail(systemEmail);
            var systemUserId = systemUser.Result == null ? Guid.Empty : (Guid)systemUser.Result.Id;
            return new RServiceResult<int>(await _context.DivanCatCorrections.AsNoTracking()
                .Where(c => c.Reviewed == false && (onlyUserCorrections == false || c.UserId != systemUserId))
                .CountAsync());
        }

        /// <summary>
        /// aggressive cache
        /// </summary>
        public bool AggressiveCacheEnabled
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
        /// Database Context
        /// </summary>
        protected readonly RMuseumDbContext _context;

        /// <summary>
        /// Configuration
        /// </summary>
        protected IConfiguration Configuration { get; }

        /// <summary>
        /// Background Task Queue Instance
        /// </summary>
        protected readonly IBackgroundTaskQueue _backgroundTaskQueue;

        /// <summary>
        /// IAppUserService instance
        /// </summary>
        protected IAppUserService _appUserService;


        /// <summary>
        /// Messaging service
        /// </summary>
        protected readonly IRNotificationService _notificationService;

        /// <summary>
        /// Image File Service
        /// </summary>
        protected readonly IImageFileService _imageFileService;

        /// <summary>
        /// IMemoryCache
        /// </summary>
        protected readonly IMemoryCache _memoryCache;

        /// <summary>
        /// http client
        /// </summary>
        protected readonly HttpClient _httpClient;

        /// <summary>
        /// options service
        /// </summary>

        protected readonly IRGenericOptionsService _optionsService;

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="context"></param>
        /// <param name="configuration"></param>
        /// <param name="backgroundTaskQueue"></param>
        /// <param name="appUserService"></param>
        /// <param name="notificationService"></param>
        /// <param name="imageFileService"></param>
        /// <param name="memoryCache"></param>
        /// <param name="httpClient"></param>
        /// <param name="optionsService"></param>
        public DivanService(RMuseumDbContext context, IConfiguration configuration, IBackgroundTaskQueue backgroundTaskQueue, IAppUserService appUserService, IRNotificationService notificationService, IImageFileService imageFileService, IMemoryCache memoryCache, HttpClient httpClient, IRGenericOptionsService optionsService)
        {
            _context = context;
            _backgroundTaskQueue = backgroundTaskQueue;
            _appUserService = appUserService;
            _notificationService = notificationService;
            _imageFileService = imageFileService;
            _memoryCache = memoryCache;
            Configuration = configuration;
            _httpClient = httpClient;
            _optionsService = optionsService;
        }
    }
}
