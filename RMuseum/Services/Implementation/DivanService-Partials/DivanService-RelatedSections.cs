using Microsoft.EntityFrameworkCore;
using RMuseum.DbContext;
using RMuseum.Models.Divan;
using RMuseum.Utils;
using RSecurityBackend.Models.Generic;
using RSecurityBackend.Models.Generic.Db;
using RSecurityBackend.Services.Implementation;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace RMuseum.Services.Implementation
{
    /// <summary>
    /// IDivanService implementation
    /// </summary>
    public partial class DivanService : IDivanService
    {
        /// <summary>
        /// get a section related sections
        /// </summary>
        /// <param name="poemId">poem id</param>
        /// <param name="sectionIndex">section index</param>
        /// <param name="skip"></param>
        /// <param name="itemsCount">if sent 0 or less returns all items</param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanCachedRelatedSection[]>> GetRelatedSections(int poemId, int sectionIndex, int skip, int itemsCount)
        {
            var source =
                 _context.DivanCachedRelatedSections.AsNoTracking()
                         .Where(r => r.PoemId == poemId && r.SectionIndex == sectionIndex)
                         .OrderBy(r => r.RelationOrder);

            if (itemsCount <= 0)
                return new RServiceResult<DivanCachedRelatedSection[]>(await source.ToArrayAsync());
            return new RServiceResult<DivanCachedRelatedSection[]>
                (
                await source.Skip(skip).Take(itemsCount).ToArrayAsync()
                );
        }

        /// <summary>
        /// regenerate category related sections
        /// </summary>
        /// <param name="catId"></param>
        /// <returns></returns>
        public RServiceResult<bool> StartRegeneratingCateoryRelatedSections(int catId)
        {
            try
            {
                _backgroundTaskQueue.QueueBackgroundWorkItem
                           (
                           async token =>
                           {
                               using (RMuseumDbContext context = new RMuseumDbContext(new DbContextOptions<RMuseumDbContext>())) //this is long running job, so _context might be already been freed/collected by GC
                               {
                                   LongRunningJobProgressServiceEF jobProgressServiceEF = new LongRunningJobProgressServiceEF(context);
                                   var job = (await jobProgressServiceEF.NewJob($"StartRegeneratingCateoryRelatedSections - {catId}", "Query data")).Result;
                                   try
                                   {
                                       var updatePoemListId = await context.DivanPoems.AsNoTracking().Where(p => p.CatId == catId).Select(p => p.Id).ToListAsync();
                                       for (int i = 0; i<updatePoemListId.Count; i++)
                                       {
                                           var updatePoemId = updatePoemListId[i];
                                           var sections = await context.DivanPoemSections.AsNoTracking().Where(s => s.PoemId == updatePoemId && s.DivanMetreId != null && !string.IsNullOrEmpty(s.RhymeLetters)).ToListAsync();
                                           for (int j = 0; j < sections.Count; j++)
                                           {
                                               var section = sections[j];
                                               await _UpdateRelatedSections(context, (int)section.DivanMetreId, section.RhymeLetters, jobProgressServiceEF, job, (i * 10 + j) * 100 / (10 * (updatePoemListId.Count + 1)));
                                           }
                                       }
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
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }



        /// <summary>
        /// update related sections info (after metreId or rhyme for one of these sections changes)
        /// </summary>
        /// <param name="context"></param>
        /// <param name="metreId"></param>
        /// <param name="rhyme"></param>
        /// <param name="jobProgressServiceEF"></param>
        /// <param name="job"></param>
        /// <param name="progress"></param>
        /// <returns></returns>
        private async Task<RServiceResult<bool>> _UpdateRelatedSections(RMuseumDbContext context, int metreId, string rhyme, LongRunningJobProgressServiceEF jobProgressServiceEF = null, RLongRunningJobStatus job = null , int progress = 0)
        {
            try
            {
                if(jobProgressServiceEF != null)
                {
                    await jobProgressServiceEF.UpdateJob(job.Id, progress, $"M: {metreId}, G: {rhyme}");
                }
                if (await context.UpdatingRelSectsLogs.AsNoTracking()
                    .Where(l => l.MeterId == metreId && l.RhymeLettes == rhyme && l.DateTime > DateTime.Now.AddMinutes(-10)).AnyAsync())
                    return new RServiceResult<bool>(true);//prevent parallel updates for same data

                var log = new UpdatingRelSectsLog()
                {
                    MeterId = metreId,
                    RhymeLettes = rhyme,
                    DateTime = DateTime.Now
                };
                context.Add(log);
                await context.SaveChangesAsync();

                var sections = await context.DivanPoemSections.AsNoTracking()
                    .Where(section => section.DivanMetreId == metreId && section.RhymeLetters == rhyme && section.SectionType == PoemSectionType.WholePoem)
                    .ToListAsync();
                Dictionary<int, string> poetsImagesUrls = new Dictionary<int, string>();
                foreach (var section in sections)
                {
                    await _UpdateSectionRelatedSectionsInfoNoSaveChanges(context, section, poetsImagesUrls, true);
                }

                var logs = await context.UpdatingRelSectsLogs
                    .Where(l => l.MeterId == metreId && l.RhymeLettes == rhyme).ToListAsync();
                context.RemoveRange(logs);
                await context.SaveChangesAsync();
                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                var logs = await context.UpdatingRelSectsLogs
                    .Where(l => l.MeterId == metreId && l.RhymeLettes == rhyme).ToListAsync();
                context.RemoveRange(logs);
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        private async Task _UpdateSectionRelatedSectionsInfoNoSaveChanges(RMuseumDbContext context, DivanPoemSection section, Dictionary<int, string> poetsImagesUrls, bool needsClearance)
        {
            if(needsClearance)
            {
                var oldRelations = await context.DivanCachedRelatedSections.Where(r => r.PoemId == section.PoemId && r.SectionIndex == section.Index).ToListAsync();
                context.DivanCachedRelatedSections.RemoveRange(oldRelations);
            }

            int metreId = (int)section.DivanMetreId;
            string rhyme = section.RhymeLetters;


            var relatedSections = await context.DivanPoemSections.AsNoTracking().Include(section => section.Poem).Include(section => section.Poet)
                .Where(s =>
                        s.DivanMetreId == metreId
                        &&
                        s.RhymeLetters == rhyme
                        ).ToListAsync();

            relatedSections = relatedSections.OrderBy(p => p.Poet.BirthYearInLHijri).ThenBy(p => p.PoetId).ThenBy(p => p.SectionType).ToList();

            List<DivanCachedRelatedSection> DivanCachedRelatedSections = new List<DivanCachedRelatedSection>();
            int relationOrder = 0;
            int prePoetId = -1;
            foreach (var relatedSection in relatedSections)
            {
                if (relatedSection.Id == section.Id)
                    continue;
                if (prePoetId != relatedSection.PoetId)
                {
                    relationOrder++;

                    var fullUrl = relatedSection.Poem.FullUrl;
                    if(relatedSection.CachedFirstCoupletIndex > 0)
                    {
                        fullUrl += $"#bn{relatedSection.CachedFirstCoupletIndex + 1}";
                    }

                    if(!poetsImagesUrls.TryGetValue((int)relatedSection.PoetId, out string imgUrl))
                    {
                        imgUrl = $"/api/divan/poet/image{(await context.DivanCategories.Where(c => c.ParentId == null && c.PoetId == relatedSection.PoetId).AsNoTracking().SingleAsync()).FullUrl}.gif";
                        poetsImagesUrls[(int)relatedSection.PoetId] = imgUrl;
                    }

                    DivanCachedRelatedSection newRelatedPoem = new DivanCachedRelatedSection()
                    {
                        PoemId = section.PoemId,
                        SectionIndex = section.Index,
                        PoetId = (int)relatedSection.PoetId,
                        RelationOrder = relationOrder,
                        PoetName = relatedSection.Poet.Nickname,
                        PoetImageUrl = imgUrl,
                        FullTitle = relatedSection.Poem.FullTitle,
                        FullUrl = fullUrl,
                        PoetMorePoemsLikeThisCount = 0,
                        HtmlExcerpt = DivanPoemTools.GetPoemHtmlExcerpt(relatedSection.HtmlText),
                        TargetPoemId = relatedSection.PoemId,
                        TargetSectionIndex = relatedSection.Index,
                    };

                    DivanCachedRelatedSections.Add(newRelatedPoem);

                    prePoetId = (int)relatedSection.PoetId;
                }
                else
                {
                    DivanCachedRelatedSections[DivanCachedRelatedSections.Count - 1].PoetMorePoemsLikeThisCount++;
                }
            }
            if (DivanCachedRelatedSections.Count > 0)
            {
                context.DivanCachedRelatedSections.AddRange(DivanCachedRelatedSections);
            }
        }

        /// <summary>
        /// start generating related sections info
        /// </summary>
        /// <param name="regenerate"></param>
        /// <returns></returns>
        public RServiceResult<bool> StartGeneratingRelatedSectionsInfo(bool regenerate)
        {
            try
            {
                _backgroundTaskQueue.QueueBackgroundWorkItem
                            (
                            async token =>
                            {
                                using (RMuseumDbContext context = new RMuseumDbContext(new DbContextOptions<RMuseumDbContext>())) //this is long running job, so _context might be already been freed/collected by GC
                                {
                                    LongRunningJobProgressServiceEF jobProgressServiceEF = new LongRunningJobProgressServiceEF(context);
                                    var job = (await jobProgressServiceEF.NewJob($"GeneratingRelatedSectionsInfo - Whole Poems", "Query")).Result;
                                    int number = 0;
                                    try
                                    {

                                        await jobProgressServiceEF.UpdateJob(job.Id, 0, $"Query");

                                        var sectionsInfo = 
                                            await context.DivanPoemSections.AsNoTracking()
                                            .Where(p => p.SectionType == PoemSectionType.WholePoem 
                                            && !string.IsNullOrEmpty(p.RhymeLetters) && p.DivanMetreId != null)
                                            .Select(s => new { s.Id, s.PoemId, s.Index })
                                            .ToListAsync();

                                        await jobProgressServiceEF.UpdateJob(job.Id, 0, $"Updating Related Sections for {sectionsInfo.Count} sections");
                                        Dictionary<int, string> poetsImagesUrls = new Dictionary<int, string>();
                                        for (int i = 0; i < sectionsInfo.Count; i++)
                                        {
                                            number++;
                                            
                                            if (!regenerate)
                                            {
                                                if (await context.DivanCachedRelatedSections.AnyAsync(r => r.PoemId == sectionsInfo[i].PoemId && r.SectionIndex == sectionsInfo[i].Index))
                                                    continue;
                                            }
                                            var section = await context.DivanPoemSections.AsNoTracking().SingleAsync(s => s.Id == sectionsInfo[i].Id);
                                            await _UpdateSectionRelatedSectionsInfoNoSaveChanges(context, section, poetsImagesUrls, !regenerate);

                                           
                                            if(number % 100 == 0)
                                                await jobProgressServiceEF.UpdateJob(job.Id, number);
                                        }

                                        await jobProgressServiceEF.UpdateJob(job.Id, 100, "", true);
                                    }
                                    catch (Exception exp)
                                    {
                                        await jobProgressServiceEF.UpdateJob(job.Id, number, "", false, exp.ToString());
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
    }
}