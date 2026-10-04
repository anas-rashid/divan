using Microsoft.EntityFrameworkCore;
using RMuseum.DbContext;
using RMuseum.Models.Divan;
using RMuseum.Models.Divan.ViewModels;
using RSecurityBackend.Models.Generic;
using RSecurityBackend.Services;
using RSecurityBackend.Services.Implementation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RMuseum.Services.Implementation
{
    /// <summary>
    /// numbering service implementation
    /// </summary>
    public class DivanNumberingService : IDivanNumberingService
    {
        /// <summary>
        /// add numbering
        /// </summary>
        /// <param name="numbering"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanNumbering>> AddNumberingAsync(DivanNumbering numbering)
        {
            numbering.Name = numbering.Name == null ? numbering.Name : numbering.Name.Trim();
            if (string.IsNullOrEmpty(numbering.Name))
                return new RServiceResult<DivanNumbering>(null, "ورود نام طرح شماره‌گذاری اجباری است.");
            var existing = await _context.DivanNumberings.Where(l => l.Name == numbering.Name && l.StartCatId == numbering.StartCatId).FirstOrDefaultAsync();
            if (existing != null)
                return new RServiceResult<DivanNumbering>(null, "نام در این بخش تکراری است.");
            try
            {
                _context.DivanNumberings.Add(numbering);
                await _context.SaveChangesAsync();
                Recount(numbering.Id); //start counting
                return new RServiceResult<DivanNumbering>(numbering);
            }
            catch (Exception exp)
            {
                return new RServiceResult<DivanNumbering>(null, exp.ToString());
            }
        }

        /// <summary>
        /// update an existing numbering (only name)
        /// </summary>
        /// <param name="updated"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> UpdateNumberingAsync(DivanNumbering updated)
        {
            try
            {
                var numbering = await _context.DivanNumberings.Where(l => l.Id == updated.Id).SingleOrDefaultAsync();
                if (numbering == null)
                    return new RServiceResult<bool>(false, "اطلاعات طرح شماره گذاری یافت نشد.");

                numbering.Name = updated.Name;
                _context.Update(numbering);

                await _context.SaveChangesAsync();
                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// delete numbering
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> DeleteNumberingAsync(int id)
        {
            try
            {
                var numbering = await _context.DivanNumberings.Where(l => l.Id == id).SingleOrDefaultAsync();
                if (numbering == null)
                    return new RServiceResult<bool>(false, "اطلاعات طرح شماره گذاری یافت نشد.");

                _context.Remove(numbering);

                await _context.SaveChangesAsync();
                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// get numbering by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanNumbering>> GetNumberingAsync(int id)
        {
            try
            {
                return new RServiceResult<DivanNumbering>(await _context.DivanNumberings.Where(l => l.Id == id).SingleOrDefaultAsync());
            }
            catch (Exception exp)
            {
                return new RServiceResult<DivanNumbering>(null, exp.ToString());
            }
        }

        /// <summary>
        /// get all numberings
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<DivanNumbering[]>> GetNumberingsAsync()
        {
            try
            {
                return new RServiceResult<DivanNumbering[]>(await _context.DivanNumberings.OrderBy(l => l.Id).ToArrayAsync());
            }
            catch (Exception exp)
            {
                return new RServiceResult<DivanNumbering[]>(null, exp.ToString());
            }
        }

        /// <summary>
        /// get numberings for a cat
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<DivanNumbering[]>> GetNumberingsForCatAsync(int catId)
        {
            try
            {
                return new RServiceResult<DivanNumbering[]>(await _context.DivanNumberings.Where(n => n.StartCatId == catId).OrderBy(l => l.Id).ToArrayAsync());
            }
            catch (Exception exp)
            {
                return new RServiceResult<DivanNumbering[]>(null, exp.ToString());
            }
        }

        /// <summary>
        /// get numberings for direct subcats of parent cat
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<DivanNumbering[]>> GetNumberingsForDirectSubCatsAsync(int parentCatId)
        {
            try
            {
                var subcatIds = await _context.DivanCategories.AsNoTracking().Where(c => c.ParentId == parentCatId).Select(c => c.Id).ToListAsync();
                return new RServiceResult<DivanNumbering[]>(await _context.DivanNumberings.Where(n => subcatIds.Contains(n.StartCatId)).OrderBy(l => l.Id).ToArrayAsync());
            }
            catch (Exception exp)
            {
                return new RServiceResult<DivanNumbering[]>(null, exp.ToString());
            }
        }

        /// <summary>
        /// get all numbering patterns for a couplet
        /// </summary>
        /// <param name="poemId"></param>
        /// <param name="coupletIndex"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanCoupletNumberViewModel[]>> GetNumberingsForCouplet(int poemId, int coupletIndex)
        {
            return new RServiceResult<DivanCoupletNumberViewModel[]>
            (
            await _context.DivanVerseNumbers.Include(n => n.Numbering)
                    .Where(n => n.PoemId == poemId && n.CoupletIndex == coupletIndex)
                    .OrderBy(n => n.NumberingId)
                    .Select
                    (
                    n =>
                    new DivanCoupletNumberViewModel()
                    {
                        NumberingName = n.Numbering.Name,
                        IsPoemVerse = n.IsPoemVerse,
                        Number = n.Number,
                        SameTypeNumber = n.SameTypeNumber,
                        TotalLines = n.Numbering.TotalLines,
                        TotalCouplets = n.Numbering.TotalCouplets,
                        TotalParagraphs = n.Numbering.TotalParagraphs
                    }
                    ).ToArrayAsync()
            );
        }

        private async Task<List<DivanCat>> _FindAllSubCategories(RMuseumDbContext context, int catId)
        {
            List<DivanCat> cats = new List<DivanCat>();

            foreach (var cat in await context.DivanCategories.AsNoTracking().Where(c => c.ParentId == catId).OrderBy(c => c.Id).ToListAsync())
            {
                cats.Add(cat);

                var subCats = await _FindAllSubCategories(context, cat.Id);

                if (subCats.Count > 0)
                {
                    cats.AddRange(subCats);
                }
            }
            return cats;
        }

        private async Task<List<DivanCat>> _FindCategoryRanges(RMuseumDbContext context, DivanCat startCat, int endCatId)
        {
            var parents = await context.DivanCategories.AsNoTracking().Where(c => c.ParentId == startCat.ParentId && c.Id >= startCat.Id && c.Id <= endCatId).OrderBy(c => c.Id).ToListAsync();
            List<DivanCat> cats = new List<DivanCat>();
            foreach (DivanCat parentCat in parents)
            {
                cats.Add(parentCat);
                var subCats = await _FindAllSubCategories(context, parentCat.Id);
                if (subCats.Count > 0)
                {
                    cats.AddRange(subCats);
                }
            }
            return cats;
        }


        private async Task<List<DivanCat>> _FindTargetCategories(RMuseumDbContext context, DivanCat startCat, int? endCatId)
        {
            if (endCatId == null || startCat.Id == endCatId)
            {
                List<DivanCat> cats = new List<DivanCat>();
                cats.Add(startCat);
                cats.AddRange(await _FindAllSubCategories(context, startCat.Id));
                return cats;
            }
            else
            {
                return await _FindCategoryRanges(context, startCat, (int)endCatId);
            }
        }

        /// <summary>
        /// start counting
        /// </summary>
        /// <returns></returns>
        public RServiceResult<bool> Recount(int numberingId)
        {
            _backgroundTaskQueue.QueueBackgroundWorkItem
            (
            async token =>
            {
                using (RMuseumDbContext context = new RMuseumDbContext(new DbContextOptions<RMuseumDbContext>())) //this is long running job, so _context might be already been freed/collected by GC
                {
                    LongRunningJobProgressServiceEF jobProgressServiceEF = new LongRunningJobProgressServiceEF(context);
                    var job = (await jobProgressServiceEF.NewJob("NumberingService::Count", "Query Cats")).Result;

                    try
                    {
                        var numbering = await context.DivanNumberings.Include(n => n.StartCat).Where(n => n.Id == numberingId).SingleOrDefaultAsync();
                        var cats = await _FindTargetCategories(context, numbering.StartCat, numbering.EndCatId);

                        await jobProgressServiceEF.UpdateJob(job.Id, 0, "Deleting Old Data");

                        var oldNumbers = await context.DivanVerseNumbers.Where(n => n.NumberingId == numberingId).ToListAsync();
                        context.DivanVerseNumbers.RemoveRange(oldNumbers);
                        await context.SaveChangesAsync();

                        await jobProgressServiceEF.UpdateJob(job.Id, 0, "Counting");

                        int number = 0;
                        int coupletnumber = 0;
                        int paragraphnumber = 0;
                        int totalVerseCount = 0;

                        foreach (var cat in cats)
                        {
                            var poems = await context.DivanPoems.AsNoTracking().Where(p => p.CatId == cat.Id).OrderBy(p => p.Id).ToListAsync();

                            await jobProgressServiceEF.UpdateJob(job.Id, 0, $"Counting:: {cat.Title}");

                            foreach (var poem in poems)
                            {
                                totalVerseCount += await context.DivanVerses.Where(v => v.PoemId == poem.Id).CountAsync();
                                var verses = await context.DivanVerses.AsNoTracking()
                                                .Where(v => v.PoemId == poem.Id && v.VersePosition != VersePosition.Left && v.VersePosition != VersePosition.CenteredVerse2 && v.VersePosition != VersePosition.Comment)
                                                .OrderBy(v => v.VOrder)
                                                .ToListAsync();
                                for (int coupletIndex = 0; coupletIndex < verses.Count; coupletIndex++)
                                {
                                    number++;
                                    bool isPoemVerse = verses[coupletIndex].VersePosition == VersePosition.Right || verses[coupletIndex].VersePosition == VersePosition.CenteredVerse1;
                                    if (isPoemVerse)
                                        coupletnumber++;
                                    else
                                        paragraphnumber++;
                                    DivanVerseNumber verseNumber = new DivanVerseNumber()
                                    {
                                        NumberingId = numberingId,
                                        PoemId = poem.Id,
                                        CoupletIndex = coupletIndex,
                                        Number = number,
                                        IsPoemVerse = isPoemVerse,
                                        SameTypeNumber = isPoemVerse ? coupletnumber : paragraphnumber
                                    };
                                    context.DivanVerseNumbers.Add(verseNumber);
                                }
                            }
                        }

                        numbering.TotalLines = number;
                        numbering.TotalVerses = totalVerseCount;
                        numbering.TotalCouplets = coupletnumber;
                        numbering.TotalParagraphs = paragraphnumber;
                        numbering.LastCountingDate = DateTime.Now;
                        context.DivanNumberings.Update(numbering);

                        await context.SaveChangesAsync();

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

        /// <summary>
        /// generate missing default numberings and start counting
        /// </summary>
        /// <returns></returns>
        public RServiceResult<bool> GenerateMissingDefaultNumberings()
        {
            _backgroundTaskQueue.QueueBackgroundWorkItem
            (
            async token =>
            {
                using (RMuseumDbContext context = new RMuseumDbContext(new DbContextOptions<RMuseumDbContext>())) //this is long running job, so _context might be already been freed/collected by GC
                {
                    LongRunningJobProgressServiceEF jobProgressServiceEF = new LongRunningJobProgressServiceEF(context);
                    var job = (await jobProgressServiceEF.NewJob("GenerateMissingDefaultNumberings", "Query")).Result;

                    try
                    {
                        var cats = await context.DivanCategories.Where(c => c.ParentId != null).ToListAsync();

                        foreach (var cat in cats)
                        {
                            var numbering = await context.DivanNumberings.Where(n => n.StartCatId == cat.Id && n.Name == cat.Title).FirstOrDefaultAsync();
                            if(numbering == null)
                            {
                                await jobProgressServiceEF.UpdateJob(job.Id, 0, cat.FullUrl);
                                numbering = new DivanNumbering()
                                {
                                    Name = cat.Title,
                                    StartCatId = cat.Id,
                                    EndCatId = cat.Id
                                };
                                context.DivanNumberings.Add(numbering);
                                await context.SaveChangesAsync();
                                Recount(numbering.Id); //start counting
                            }
                        }

                        await context.SaveChangesAsync();

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

        /// <summary>
        /// Database Context
        /// </summary>
        protected readonly RMuseumDbContext _context;

        /// <summary>
        /// Background Task Queue Instance
        /// </summary>
        protected readonly IBackgroundTaskQueue _backgroundTaskQueue;

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="context"></param>
        /// <param name="backgroundTaskQueue"></param>
        public DivanNumberingService(RMuseumDbContext context, IBackgroundTaskQueue backgroundTaskQueue)
        {
            _context = context;
            _backgroundTaskQueue = backgroundTaskQueue;
        }
    }
}
