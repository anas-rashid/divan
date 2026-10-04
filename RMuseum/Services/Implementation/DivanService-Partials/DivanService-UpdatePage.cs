using Microsoft.EntityFrameworkCore;
using RMuseum.DbContext;
using RMuseum.Models.Divan;
using RMuseum.Models.Divan.ViewModels;
using RSecurityBackend.Models.Generic;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using RSecurityBackend.Services.Implementation;

namespace RMuseum.Services.Implementation
{
    /// <summary>
    /// IDivanService implementation
    /// </summary>
    public partial class DivanService : IDivanService
    {

        private async Task<RServiceResult<DivanPageCompleteViewModel>> _UpdatePoemAsync(RMuseumDbContext context, int id, Guid editingUserId, DivanModifyPageViewModel pageData, bool needsReturn)
        {
            try
            {
                var dbPage = await context.DivanPages.Where(p => p.Id == id).SingleOrDefaultAsync();
                if (dbPage == null)
                    return new RServiceResult<DivanPageCompleteViewModel>(null);//not found
                if (dbPage.DivanPageType != DivanPageType.PoemPage)
                {
                    return new RServiceResult<DivanPageCompleteViewModel>(null, "از _UpdatePageAsync استفاده کنید.");
                }
                dbPage.NoIndex = pageData.NoIndex;
                dbPage.RedirectFromFullUrl = string.IsNullOrEmpty(pageData.RedirectFromFullUrl) ? null : pageData.RedirectFromFullUrl;
                context.DivanPages.Update(dbPage);

                var dbPoem = await context.DivanPoems.Where(p => p.Id == id).SingleOrDefaultAsync();
                dbPoem.MixedModeOrder = pageData.MixedModeOrder;
                context.Update(dbPoem);

                await context.SaveChangesAsync();
                CacheCleanForPageByUrl(dbPage.FullUrl);

                if (needsReturn)
                {
                    return await GetPageByUrl(dbPage.FullUrl);
                }
                return new RServiceResult<DivanPageCompleteViewModel>(null);
            }
            catch (Exception exp)
            {
                return new RServiceResult<DivanPageCompleteViewModel>(null, exp.ToString());
            }
        }
        private async Task<RServiceResult<DivanPageCompleteViewModel>> _UpdatePageAsync(RMuseumDbContext context, int id, Guid editingUserId, DivanModifyPageViewModel pageData, bool needsReturn)
        {
            try
            {
                var dbPage = await context.DivanPages.Where(p => p.Id == id).SingleOrDefaultAsync();
                if (dbPage == null)
                    return new RServiceResult<DivanPageCompleteViewModel>(null);//not found

                if (dbPage.DivanPageType == DivanPageType.PoemPage)
                {
                    return new RServiceResult<DivanPageCompleteViewModel>(null, "به‌روزرسانی متن شعر از طریق _UpdatePageAsync غیرفعال شده است.");
                }


                DivanPageSnapshot snapshot = new DivanPageSnapshot()
                {
                    DivanPageId = id,
                    MadeObsoleteByUserId = editingUserId,
                    RecordDate = DateTime.Now,
                    Note = pageData.Note,
                    Title = dbPage.Title,
                    UrlSlug = dbPage.UrlSlug,
                    HtmlText = dbPage.HtmlText,
                };


                context.DivanPageSnapshots.Add(snapshot);
                await context.SaveChangesAsync();

                dbPage.HtmlText = pageData.HtmlText;
                dbPage.NoIndex = pageData.NoIndex;
                dbPage.RedirectFromFullUrl = string.IsNullOrEmpty(pageData.RedirectFromFullUrl) ? null : pageData.RedirectFromFullUrl;
                bool messWithTitles = dbPage.Title != pageData.Title;
                bool messWithUrls = dbPage.UrlSlug != pageData.UrlSlug;

                if (dbPage.DivanPageType == DivanPageType.CatPage || dbPage.DivanPageType == DivanPageType.PoetPage)
                {
                    DivanCat cat = await context.DivanCategories.Where(c => c.Id == dbPage.CatId).SingleAsync();
                    cat.Published = pageData.Published;
                    cat.MixedModeOrder = pageData.MixedModeOrder;
                    cat.TableOfContentsStyle = pageData.TableOfContentsStyle;
                    cat.CatType = pageData.CatType;
                    cat.Description = pageData.Description;
                    cat.DescriptionHtml = pageData.DescriptionHtml;

                    context.DivanCategories.Update(cat);
                    await context.SaveChangesAsync();

                    if (dbPage.DivanPageType == DivanPageType.PoetPage)
                    {
                        var poet = await context.DivanPoets.Where(p => p.Id == dbPage.PoetId).SingleAsync();
                        poet.Description = cat.Description;
                        context.Update(poet);
                        await context.SaveChangesAsync();
                    }
                }

                if (messWithTitles || messWithUrls)
                {

                    dbPage.Title = pageData.Title;
                    dbPage.UrlSlug = pageData.UrlSlug;

                    if (dbPage.ParentId != null)
                    {
                        DivanPage parent = await context.DivanPages.AsNoTracking().Where(p => p.Id == dbPage.ParentId).SingleAsync();
                        if (messWithUrls)
                        {
                            dbPage.FullUrl = parent.FullUrl + "/" + pageData.UrlSlug;
                        }
                        if (messWithTitles)
                        {
                            dbPage.FullTitle = parent.FullTitle + " » " + pageData.Title;
                        }
                    }
                    else
                    {
                        if (messWithUrls)
                        {
                            dbPage.FullUrl = "/" + pageData.UrlSlug;
                        }

                        if (messWithTitles)
                        {
                            dbPage.FullTitle = pageData.Title;
                        }

                    }

                    switch (dbPage.DivanPageType)
                    {
                        case DivanPageType.CatPage:
                            {
                                DivanCat cat = await context.DivanCategories.Where(c => c.Id == dbPage.CatId).SingleAsync();
                                if (messWithTitles)
                                    cat.Title = dbPage.Title;
                                if (messWithUrls)
                                {
                                    cat.UrlSlug = dbPage.UrlSlug;
                                    cat.FullUrl = dbPage.FullUrl;
                                }

                                context.DivanCategories.Update(cat);
                                await context.SaveChangesAsync();
                            }
                            break;
                    }
                    _backgroundTaskQueue.QueueBackgroundWorkItem
                       (
                       async token =>
                       {
                           using (RMuseumDbContext inlineContext = new RMuseumDbContext(new DbContextOptions<RMuseumDbContext>())) //this is long running job, so context might be already been freed/collected by GC
                           {
                               LongRunningJobProgressServiceEF jobProgressServiceEF = new LongRunningJobProgressServiceEF(inlineContext);
                               var job = (await jobProgressServiceEF.NewJob($"Updating PageChildren for {dbPage.Id}", "Updating")).Result;
                               try
                               {


                                   await _UpdatePageChildrenTitleAndUrl(inlineContext, dbPage, messWithTitles, messWithUrls);

                                   await jobProgressServiceEF.UpdateJob(job.Id, 100, "", true);
                               }
                               catch (Exception expUpdateBatch)
                               {
                                   await jobProgressServiceEF.UpdateJob(job.Id, 100, "", false, expUpdateBatch.ToString());
                               }
                           }

                       }
                       );

                }

                if (dbPage.DivanPageType == DivanPageType.PoetPage && (messWithTitles || messWithUrls))
                {
                    if (messWithTitles)
                    {
                        DivanPoet poet = await context.DivanPoets.Where(p => p.Id == dbPage.PoetId).SingleAsync();
                        poet.Nickname = dbPage.Title;
                        context.DivanPoets.Update(poet);
                    }


                    DivanCat cat = await context.DivanCategories.Where(c => c.Id == dbPage.CatId).SingleAsync();
                    if (messWithTitles)
                    {
                        cat.Title = dbPage.Title;
                    }
                    if (messWithUrls)
                    {
                        cat.UrlSlug = dbPage.UrlSlug;
                        cat.FullUrl = dbPage.FullUrl;
                    }


                    context.DivanCategories.Update(cat);

                    await context.SaveChangesAsync();

                    CleanPoetCache((int)dbPage.PoetId);
                }

                context.DivanPages.Update(dbPage);
                
                await context.SaveChangesAsync();
                CacheCleanForPageByUrl(dbPage.FullUrl);

                if(needsReturn)
                {
                    return await GetPageByUrl(dbPage.FullUrl);
                }
                return new RServiceResult<DivanPageCompleteViewModel>(null);
               
            }
            catch (Exception exp)
            {
                return new RServiceResult<DivanPageCompleteViewModel>(null, exp.ToString());
            }
        }
    }
}
