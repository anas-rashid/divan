using RMuseum.Models.Divan;
using RMuseum.Models.Divan.ViewModels;
using RSecurityBackend.Models.Generic;
using System;
using System.Threading.Tasks;

namespace RMuseum.Services
{
    /// <summary>
    /// translation service implementation
    /// </summary>
    public interface IDivanTranslationService
    {
        /// <summary>
        /// add language
        /// </summary>
        /// <param name="lang"></param>
        /// <returns></returns>
        Task<RServiceResult<DivanLanguage>> AddLanguageAsync(DivanLanguage lang);


        /// <summary>
        /// update an existing language
        /// </summary>
        /// <param name="updated"></param>
        /// <returns></returns>
        Task<RServiceResult<bool>> UpdateLangaugeAsync(DivanLanguage updated);


        /// <summary>
        /// delete language
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<RServiceResult<bool>> DeleteLangaugeAsync(int id);


        /// <summary>
        /// get langauge by id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        Task<RServiceResult<DivanLanguage>> GetLanguageAsync(int id);

        /// <summary>
        /// get all languages
        /// </summary>
        /// <returns></returns>
        Task<RServiceResult<DivanLanguage[]>> GetLanguagesAsync();
    }
}
