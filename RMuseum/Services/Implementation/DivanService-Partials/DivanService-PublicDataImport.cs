using Microsoft.EntityFrameworkCore;
using RMuseum.DbContext;
using RMuseum.Models.Divan;
using RMuseum.Models.Divan.PublicExport;
using RMuseum.Utils.PublicDataImport;
using RSecurityBackend.Models.Generic;
using RSecurityBackend.Services.Implementation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace RMuseum.Services.Implementation
{
    /// <summary>
    /// IDivanService implementation
    /// </summary>
    public partial class DivanService : IDivanService
    {
        /// <summary>
        /// Category (and root-poet) pages don't carry a real production page id in the public
        /// export — DivanPage isn't part of that data set — so this importer mints one
        /// deterministically from the category id, kept well clear of any real id range so it can
        /// never collide with an actual production DivanPage/DivanPoem id. Poem pages don't
        /// need this: they reuse the poem's own id, matching the convention already used by
        /// _ImportSQLiteCatChildren (see DivanService-SQLiteImport.cs).
        /// </summary>
        private static int SyntheticCatPageId(int catId) => 900_000_000 + catId;

        private static readonly JsonSerializerOptions _importJsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };

        /// <summary>
        /// (Re)builds Divan content — poets, categories, poems, verses, sections, and their
        /// DivanPage routing entries — from a public data export tree, read either from a local
        /// `git clone` or fetched over HTTP. Safe to run against an empty database (bootstrap) or
        /// one that already has some content (merge): every entity is looked up by its id first
        /// and only inserted if missing, so re-running never duplicates or overwrites anything —
        /// including content a developer may have hand-edited locally after a previous import.
        /// </summary>
        /// <param name="useHttp">true: fetch over HTTP (location is a base URL). false: read from a local folder (location is a path).</param>
        /// <param name="location">base URL or local folder path of the exported data tree</param>
        /// <param name="poetId">0 imports every poet in the export's manifest; a specific id imports only that poet — useful on a slow connection, or when a developer only needs one poet's data for local testing</param>
        public RServiceResult<bool> StartImportFromPublicDataRepo(bool useHttp, string location, int poetId = 0, Guid userId = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(location))
                    return new RServiceResult<bool>(false, "location is required");

                _backgroundTaskQueue.QueueBackgroundWorkItem
                (
                    async token =>
                    {
                        // the injected _httpClient is request-scoped and already disposed by the
                        // time this queued work item runs, so the job owns its own client
                        using HttpClient importHttpClient = new HttpClient() { Timeout = TimeSpan.FromMinutes(5) };
                        using (RMuseumDbContext context = new RMuseumDbContext(new DbContextOptions<RMuseumDbContext>()))
                        {
                            LongRunningJobProgressServiceEF jobProgressServiceEF = new LongRunningJobProgressServiceEF(context);
                            var job = (await jobProgressServiceEF.NewJob("ImportFromPublicDataRepo", "Reading manifest")).Result;

                            try
                            {
                                IPublicDataSource source = useHttp
                                    ? new HttpPublicDataSource(importHttpClient, location)
                                    : new LocalFileSystemPublicDataSource(location);

                                string manifestJson = await source.ReadTextAsync("manifest.json");
                                if (manifestJson == null)
                                    throw new Exception($"manifest.json not found at '{location}' — check the path/URL");

                                var manifest = JsonSerializer.Deserialize<PublicExportManifestDto>(manifestJson, _importJsonOptions);

                                var poetsToImport = manifest.Poets;
                                if (poetId != 0)
                                {
                                    poetsToImport = manifest.Poets.Where(p => p.Id == poetId).ToList();
                                    if (poetsToImport.Count == 0)
                                        throw new Exception($"poet id {poetId} was not found in manifest.json");
                                }

                                int poetIndex = 0;
                                foreach (var poetEntry in poetsToImport)
                                {
                                    poetIndex++;
                                    await jobProgressServiceEF.UpdateJob(job.Id, (int)(100.0 * poetIndex / Math.Max(1, poetsToImport.Count)), $"Importing {poetEntry.Nickname}");
                                    await ImportPoetFromPublicData(context, source, poetEntry.Id, poetEntry.FullUrl);
                                }

                                // DivanRazor's home page groups poets by century (GetCenturiesAsync /
                                // the "PoetGroups" the view renders) — that grouping is a materialized
                                // table (GanjorCentury/DivanCenturyPoet), not computed on the fly, and
                                // newly-imported poets aren't in it until this runs. Skipping this step
                                // is exactly what leaves PoetGroups empty and throws on the home page
                                // (see Index.cshtml.cs's IsDatabaseEffectivelyEmpty check) even though
                                // poets now exist.
                                await jobProgressServiceEF.UpdateJob(job.Id, 99, "Regenerating century groupings");
                                await _RegenerateHalfCenturies(context);
                                _memoryCache.Remove("divan/centuries"); // same cache key GetCenturiesAsync/the "periods" endpoint use

                                // divan: poet/category pages are imported with empty HtmlText (upstream fills it
                                // with a separate admin TOC job); generate the tables of contents now so poet
                                // and category pages list their works. Needs a real user for the page edit records.
                                if (userId != Guid.Empty)
                                    await _RegenerateTOCsAsync(userId, context, jobProgressServiceEF);

                                await jobProgressServiceEF.UpdateJob(job.Id, 100, "", true);
                            }
                            catch (Exception exp)
                            {
                                await jobProgressServiceEF.UpdateJob(job.Id, 100, "", false, exp.ToString());
                            }
                        }
                    }
                );

                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        private async Task ImportPoetFromPublicData(RMuseumDbContext context, IPublicDataSource source, int poetId, string poetFullUrl)
        {
            string nickname = poetFullUrl;

            if (!await context.DivanPoets.AnyAsync(p => p.Id == poetId))
            {
                string poetJson = await source.ReadTextAsync($"poets{poetFullUrl}/poet.json");
                if (poetJson == null)
                    return; // referenced in manifest but file missing — skip rather than fail the whole run

                var poetDto = JsonSerializer.Deserialize<PoetPublicDto>(poetJson, _importJsonOptions);
                nickname = poetDto.Nickname;

                context.DivanPoets.Add(new DivanPoet
                {
                    Id = poetDto.Id,
                    Name = poetDto.Name,
                    Nickname = poetDto.Nickname,
                    Description = poetDto.Description,
                    Published = true,
                    BirthYearInLHijri = poetDto.BirthYearInLHijri,
                    ValidBirthDate = poetDto.ValidBirthDate,
                    DeathYearInLHijri = poetDto.DeathYearInLHijri,
                    ValidDeathDate = poetDto.ValidDeathDate,
                });
                await context.SaveChangesAsync();
            }
            else
            {
                nickname = (await context.DivanPoets.AsNoTracking().Where(p => p.Id == poetId).SingleAsync()).Nickname;
            }

            // the poet's root category IS the poet's landing page (e.g. /hafez) — imported the
            // same way as any category, just flagged as the tree's root for page-type purposes
            await ImportCatTreeFromPublicData(context, source, poetId, poetFullUrl, null, nickname, isRoot: true);
        }

        private async Task ImportCatTreeFromPublicData(RMuseumDbContext context, IPublicDataSource source, int poetId,
            string catFullUrl, int? parentPageId, string parentFullTitle, bool isRoot)
        {
            string catJson = await source.ReadTextAsync($"poets{catFullUrl}/_cat.json");
            if (catJson == null)
                return;

            var catDto = JsonSerializer.Deserialize<CatPublicDto>(catJson, _importJsonOptions);
            string fullTitle = isRoot ? parentFullTitle : $"{parentFullTitle} » {catDto.Title}";
            int catPageId = SyntheticCatPageId(catDto.Id);

            if (!await context.DivanCategories.AnyAsync(c => c.Id == catDto.Id))
            {
                context.DivanCategories.Add(new DivanCat
                {
                    Id = catDto.Id,
                    PoetId = catDto.PoetId,
                    ParentId = catDto.ParentId,
                    Title = catDto.Title,
                    UrlSlug = LastUrlSegment(catDto.FullUrl),
                    FullUrl = catDto.FullUrl,
                    Description = catDto.Description,
                    DescriptionHtml = catDto.DescriptionHtml,
                    BookName = catDto.BookName,
                    Published = true,
                    TableOfContentsStyle = DivanTOC.Analyse,
                });

                context.DivanPages.Add(new DivanPage
                {
                    Id = catPageId,
                    DivanPageType = isRoot ? DivanPageType.PoetPage : DivanPageType.CatPage,
                    Published = true,
                    PageOrder = -1,
                    Title = catDto.Title,
                    FullTitle = fullTitle,
                    UrlSlug = LastUrlSegment(catDto.FullUrl),
                    FullUrl = catDto.FullUrl,
                    HtmlText = "",
                    PoetId = poetId,
                    CatId = catDto.Id,
                    PostDate = DateTime.Now,
                    ParentId = parentPageId,
                });

                await context.SaveChangesAsync();
            }

            foreach (var poemRef in catDto.Poems)
            {
                if (await context.DivanPoems.AnyAsync(p => p.Id == poemRef.Id))
                    continue; // already imported — never re-fetch or overwrite

                await ImportPoemFromPublicData(context, source, poetId, catDto.Id, poemRef.Id, poemRef.FullUrl, catPageId, fullTitle);
            }

            foreach (var childRef in catDto.ChildCats)
            {
                await ImportCatTreeFromPublicData(context, source, poetId, childRef.FullUrl, catPageId, fullTitle, isRoot: false);
            }
        }

        private async Task ImportPoemFromPublicData(RMuseumDbContext context, IPublicDataSource source, int poetId, int catId,
            int poemId, string poemFullUrl, int parentPageId, string parentFullTitle)
        {
            string poemJson = await source.ReadTextAsync($"poets{poemFullUrl}.json");
            if (poemJson == null)
                return;

            var poemDto = JsonSerializer.Deserialize<PoemPublicDto>(poemJson, _importJsonOptions);

            var verses = poemDto.Verses.Select(v => new DivanVerse
            {
                PoemId = poemId,
                VOrder = v.VOrder,
                VersePosition = Enum.Parse<VersePosition>(v.Position),
                Text = v.Text,
                CoupletIndex = v.CoupletIndex,
                SectionIndex1 = v.SectionIndex1,
                SectionIndex2 = v.SectionIndex2,
                SectionIndex3 = v.SectionIndex3,
                SectionIndex4 = v.SectionIndex4,
            }).ToList();

            // HtmlText/PlainText aren't duplicated in the export — regenerated here with the same
            // formatting helpers the app itself uses (see DivanService-SQLiteImport.cs), so a
            // locally-imported poem renders exactly the way the current codebase renders it rather
            // than however it happened to render at export time.
            string htmlText = PrepareHtmlText(verses);
            string plainText = PreparePlainText(verses);

            var dbPoem = new DivanPoem
            {
                Id = poemId,
                CatId = catId,
                Title = poemDto.Title,
                FullTitle = poemDto.FullTitle,
                UrlSlug = LastUrlSegment(poemDto.FullUrl),
                FullUrl = poemDto.FullUrl,
                PlainText = plainText,
                HtmlText = htmlText,
                DivanMetreId = poemDto.Metre?.Id,
                RhymeLetters = poemDto.RhymeLetters,
                SourceName = poemDto.SourceName,
                SourceUrlSlug = poemDto.SourceUrlSlug,
                Language = poemDto.Language,
                PoemSummary = poemDto.PoemSummary,
                Published = true,
            };
            context.DivanPoems.Add(dbPoem);
            await context.SaveChangesAsync();

            foreach (var verse in verses)
            {
                context.DivanVerses.Add(verse);
            }
            await context.SaveChangesAsync();

            foreach (var section in poemDto.Sections)
            {
                context.DivanPoemSections.Add(new DivanPoemSection
                {
                    PoemId = poemId,
                    PoetId = poetId,
                    Index = section.Index,
                    Number = section.Number,
                    SectionType = Enum.Parse<PoemSectionType>(section.SectionType),
                    VerseType = Enum.Parse<VersePoemSectionType>(section.VerseType),
                    DivanMetreId = poemDto.Metre?.Id,
                    RhymeLetters = section.RhymeLetters,
                    PlainText = section.PlainText,
                    HtmlText = section.HtmlText,
                    PoemFormat = string.IsNullOrEmpty(section.PoemFormat) ? (DivanPoemFormat?)null : Enum.Parse<DivanPoemFormat>(section.PoemFormat),
                    Language = section.Language,
                    CoupletsCount = section.CoupletsCount,
                });
            }
            await context.SaveChangesAsync();

            context.DivanPages.Add(new DivanPage
            {
                // matches production convention: a poem's page id equals the poem's own id
                // (see DivanService-SQLiteImport.cs, dbPoemPage.Id = poemId)
                Id = poemId,
                DivanPageType = DivanPageType.PoemPage,
                Published = true,
                PageOrder = -1,
                Title = dbPoem.Title,
                FullTitle = dbPoem.FullTitle,
                UrlSlug = dbPoem.UrlSlug,
                FullUrl = dbPoem.FullUrl,
                HtmlText = dbPoem.HtmlText,
                PoetId = poetId,
                CatId = catId,
                PoemId = poemId,
                PostDate = DateTime.Now,
                ParentId = parentPageId,
            });
            await context.SaveChangesAsync();
        }

        private static string LastUrlSegment(string fullUrl)
        {
            if (string.IsNullOrEmpty(fullUrl)) return fullUrl;
            string trimmed = fullUrl.TrimEnd('/');
            int idx = trimmed.LastIndexOf('/');
            return idx == -1 ? trimmed : trimmed.Substring(idx + 1);
        }
    }
}
