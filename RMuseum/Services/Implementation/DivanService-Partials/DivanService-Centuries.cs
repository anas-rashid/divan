using Microsoft.EntityFrameworkCore;
using RMuseum.DbContext;
using RMuseum.Models.Divan;
using RSecurityBackend.Models.Generic;
using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using RMuseum.Models.Divan.ViewModels;
using System.Globalization;

namespace RMuseum.Services.Implementation
{
    /// <summary>
    /// IDivanService implementation
    /// </summary>
    public partial class DivanService : IDivanService
    {
        /// <summary>
        /// get centuries with published poets
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<DivanCenturyViewModel[]>> GetCenturiesAsync()
        {
            var poets = (await GetPoets(true, false)).Result;
            var dbCenturies = await _context.DivanCenturies.AsNoTracking().Include(c => c.Poets).OrderBy(c => c.HalfCenturyOrder).ToListAsync();

            List<DivanCenturyViewModel> res = new List<DivanCenturyViewModel>();

            var pinned = await _context.DivanPoets.AsNoTracking().Where(p => p.PinOrder != 0).OrderBy(p => p.PinOrder).ToListAsync();
            if (pinned.Count > 0)
            {
                DivanCenturyViewModel model = new DivanCenturyViewModel()
                {
                    Id = 0,
                    Name = "",
                    HalfCenturyOrder = 0,
                    ShowInTimeLine = false,
                    StartYear = 0,
                    EndYear = 0,
                    Poets = new List<DivanPoetViewModel>()
                };
                foreach (var poet in pinned)
                {
                    model.Poets.Add(poets.Where(p => p.Id == poet.Id).Single());
                }
                res.Add(model);
            }
            var fa = new CultureInfo("ur-PK");
            foreach (var dbCentury in dbCenturies)
            {
                DivanCenturyViewModel model = new DivanCenturyViewModel()
                {
                    Id = dbCentury.Id,
                    Name = dbCentury.Name,
                    HalfCenturyOrder = dbCentury.HalfCenturyOrder,
                    ShowInTimeLine = dbCentury.ShowInTimeLine,
                    StartYear = dbCentury.StartYear,
                    EndYear = dbCentury.EndYear,
                    Poets = new List<DivanPoetViewModel>()
                };

                foreach (var poet in dbCentury.Poets)
                {
                    model.Poets.Add(poets.Where(p => p.Id == poet.PoetId).Single());
                }

                model.Poets.Sort((a, b) => fa.CompareInfo.Compare(a.Nickname, b.Nickname));//sort each century poets alphabetically

                res.Add(model);
            }

            return new RServiceResult<DivanCenturyViewModel[]>(res.ToArray());
        }


        /// <summary>
        /// regenerate half centuries
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> RegenerateHalfCenturiesAsync()
        {
            return await _RegenerateHalfCenturies(_context);
        }

        /// <summary>
        /// same as <see cref="RegenerateHalfCenturiesAsync"/>, but against an explicitly-passed
        /// context instead of the injected <see cref="_context"/> — needed so background jobs
        /// that construct their own short-lived <see cref="RMuseumDbContext"/> (because the
        /// request-scoped <see cref="_context"/> may already be disposed by the time a background
        /// job runs) can call this too, e.g. right after a public-data import finishes.
        /// </summary>
        private async Task<RServiceResult<bool>> _RegenerateHalfCenturies(RMuseumDbContext context)
        {
            try
            {
                await context.Database.ExecuteSqlRawAsync("DELETE FROM DivanCenturyPoet");
                var oldOnes = await context.DivanCenturies.ToArrayAsync();
                context.RemoveRange(oldOnes);
                await context.SaveChangesAsync();


                var periods = new List<DivanCentury>
                {
                    new DivanCentury()
                    {
                        HalfCenturyOrder = 1,
                        Name = "تیسری صدی ہجری",
                        StartYear = 0,
                        EndYear = 299,
                        ShowInTimeLine = true,
                    },
                    new DivanCentury()
                    {
                        HalfCenturyOrder = 2,
                        Name = "چوتھی صدی ہجری",
                        StartYear = 300,
                        EndYear = 399,
                        ShowInTimeLine = true,
                    },
                    new DivanCentury()
                    {
                        HalfCenturyOrder = 3,
                        Name = "پانچویں صدی ہجری",
                        StartYear = 400,
                        EndYear = 499,
                        ShowInTimeLine = true,
                    },
                    new DivanCentury()
                    {
                        HalfCenturyOrder = 4,
                        Name = "چھٹی صدی ہجری",
                        StartYear = 500,
                        EndYear = 599,
                        ShowInTimeLine = true,
                    },
                    new DivanCentury()
                    {
                        HalfCenturyOrder = 5,
                        Name = "ساتویں صدی ہجری",
                        StartYear = 600,
                        EndYear = 699,
                        ShowInTimeLine = true,
                    },
                    new DivanCentury()
                    {
                        HalfCenturyOrder = 6,
                        Name = "آٹھویں صدی ہجری",
                        StartYear = 700,
                        EndYear = 799,
                        ShowInTimeLine = true,
                    },
                     new DivanCentury()
                    {
                        HalfCenturyOrder = 7,
                        Name = "نویں صدی ہجری",
                        StartYear = 800,
                        EndYear = 899,
                        ShowInTimeLine = true,
                    },
                     new DivanCentury()
                    {
                        HalfCenturyOrder = 8,
                        Name = "دسویں صدی ہجری",
                        StartYear = 900,
                        EndYear = 999,
                        ShowInTimeLine = true,
                    },
                    new DivanCentury()
                    {
                        HalfCenturyOrder = 9,
                        Name = "گیارہویں صدی ہجری",
                        StartYear = 1000,
                        EndYear = 1099,
                        ShowInTimeLine = true,
                    },

                     new DivanCentury()
                    {
                        HalfCenturyOrder = 10,
                        Name = "بارہویں صدی ہجری",
                        StartYear = 1100,
                        EndYear = 1199,
                        ShowInTimeLine = true,
                    },
                     new DivanCentury()
                    {
                        HalfCenturyOrder = 11,
                        Name = "تیرہویں صدی ہجری",
                        StartYear = 1200,
                        EndYear = 1299,
                        ShowInTimeLine = true,
                    },
                    new DivanCentury()
                    {
                        HalfCenturyOrder = 12,
                        Name = "چودہویں صدی ہجری",
                        StartYear = 1300,
                        EndYear = 1500,
                        ShowInTimeLine = true,
                    },


                };

                var poets = await context.DivanPoets.AsNoTracking().Where(p => p.Published && p.BirthYearInLHijri != 0).OrderBy(p => p.BirthYearInLHijri).ToArrayAsync();

                foreach (var poet in poets)
                {
                    DivanCentury period = null;


                    var firstPeriod = periods.Where(p => p.StartYear <= poet.BirthYearInLHijri).LastOrDefault();
                    var lastPeriod = periods.Where(p => p.EndYear >= poet.DeathYearInLHijri).FirstOrDefault();

                    if (firstPeriod != null)
                    {
                        period = firstPeriod;
                        if(lastPeriod != null)
                        {
                            if ((poet.DeathYearInLHijri - lastPeriod.StartYear) > (firstPeriod.EndYear - poet.BirthYearInLHijri))
                                period = lastPeriod;
                        }
                    }
                    else
                    {
                        period = lastPeriod;
                    }



                    if (period != null)
                    {
                        if (period.Poets == null)
                            period.Poets = new List<DivanCenturyPoet>();
                        period.Poets.Add
                            (
                            new DivanCenturyPoet()
                            {
                                PoetOrder = period.Poets.Count,
                                PoetId = poet.Id
                            }
                            );
                    }
                }

                foreach (var period in periods)
                {
                    if (period.Poets != null)
                    {
                        context.Add(period);
                        await context.SaveChangesAsync();
                    }
                }

                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }
    }
}
