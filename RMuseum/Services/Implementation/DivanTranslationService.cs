using Microsoft.EntityFrameworkCore;
using RMuseum.DbContext;
using RMuseum.Models.Divan;
using RMuseum.Models.Divan.ViewModels;
using RSecurityBackend.Models.Generic;
using RSecurityBackend.Services.Implementation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RMuseum.Services.Implementation
{
    /// <summary>
    /// translation service implementation
    /// </summary>
    public class DivanTranslationService : IDivanTranslationService
    {
        /// <summary>
        /// add language
        /// </summary>
        /// <param name="lang"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanLanguage>> AddLanguageAsync(DivanLanguage lang)
        {
            lang.Name = lang.Name == null ? lang.Name : lang.Name.Trim();
            lang.Code = lang.Code == null ? lang.Code : lang.Code.Trim();
            if (string.IsNullOrEmpty(lang.Name) || string.IsNullOrEmpty(lang.Code))
                return new RServiceResult<DivanLanguage>(null, "ورود کد و نام زبان اجباری است.");
            if (string.IsNullOrEmpty(lang.NativeName))
                lang.NativeName = lang.Name;
            var existing = await _context.DivanLanguages.Where(l => l.Name == lang.Name || l.Code == lang.Code).FirstOrDefaultAsync();
            if (existing != null)
                return new RServiceResult<DivanLanguage>(null, "اطلاعات تکراری است.");
            try
            {
                _context.DivanLanguages.Add(lang);
                await _context.SaveChangesAsync();
                return new RServiceResult<DivanLanguage>(lang);
            }
            catch (Exception exp)
            {
                return new RServiceResult<DivanLanguage>(null, exp.ToString());
            }
        }

        /// <summary>
        /// update an existing language
        /// </summary>
        /// <param name="updated"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> UpdateLangaugeAsync(DivanLanguage updated)
        {
            try
            {
                var lang = await _context.DivanLanguages.Where(l => l.Id == updated.Id).SingleOrDefaultAsync();
                if (lang == null)
                    return new RServiceResult<bool>(false, "اطلاعات زبان یافت نشد.");

                lang.Name = updated.Name;
                lang.RightToLeft = updated.RightToLeft;
                lang.Code = updated.Code;
                lang.NativeName = updated.NativeName;
                lang.Description = updated.Description;
                _context.Update(lang);

                await _context.SaveChangesAsync();
                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// delete language
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<bool>> DeleteLangaugeAsync(int id)
        {
            try
            {
                var lang = await _context.DivanLanguages.Where(l => l.Id == id).SingleOrDefaultAsync();
                if (lang == null)
                    return new RServiceResult<bool>(false, "اطلاعات زبان یافت نشد.");

                _context.Remove(lang);

                await _context.SaveChangesAsync();
                return new RServiceResult<bool>(true);
            }
            catch (Exception exp)
            {
                return new RServiceResult<bool>(false, exp.ToString());
            }
        }

        /// <summary>
        /// get langauge by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanLanguage>> GetLanguageAsync(int id)
        {
            try
            {
                return new RServiceResult<DivanLanguage>(await _context.DivanLanguages.Where(l => l.Id == id).SingleOrDefaultAsync());
            }
            catch (Exception exp)
            {
                return new RServiceResult<DivanLanguage>(null, exp.ToString());
            }
        }

        /// <summary>
        /// get all languages
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<DivanLanguage[]>> GetLanguagesAsync()
        {
            try
            {
                return new RServiceResult<DivanLanguage[]>(await _context.DivanLanguages.OrderBy(l => l.Id).ToArrayAsync());
            }
            catch (Exception exp)
            {
                return new RServiceResult<DivanLanguage[]>(null, exp.ToString());
            }
        }

        /// <summary>
        /// Database Context
        /// </summary>
        protected readonly RMuseumDbContext _context;

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="context"></param>
        public DivanTranslationService(RMuseumDbContext context)
        {
            _context = context;
        }
    }
}
