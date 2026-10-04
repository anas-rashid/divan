using Microsoft.AspNetCore.Http;
using RMuseum.Models.DivanIntegration;
using System;
using System.Collections.Generic;

namespace RMuseum.Models.Divan.ViewModels
{
    /// <summary>
    /// cat view model
    /// </summary>
    public class DivanCatViewModel
    {
        /// <summary>
        /// id
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// text
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// url => slug
        /// </summary>
        public string UrlSlug { get; set; }

        /// <summary>
        /// full url
        /// </summary>
        public string FullUrl { get; set; }

        /// <summary>
        /// TOC Style
        /// </summary>
        public DivanTOC TableOfContentsStyle { get; set; }

        /// <summary>
        /// Category Type
        /// </summary>
        public DivanCatType CatType { get; set; }

        /// <summary>
        /// additional description or note
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// html mode of additional description or note
        /// </summary>
        public string DescriptionHtml { get; set; }

        /// <summary>
        /// order when mixed with poems
        /// </summary>
        public int MixedModeOrder { get; set; }

        /// <summary>
        /// published
        /// </summary>
        public bool Published { get; set; }

        /// <summary>
        /// category book name
        /// </summary>
        public string BookName { get; set; }

        /// <summary>
        /// user image id
        /// </summary>
        public Guid? RImageId { get; set; }

        /// <summary>
        /// sum up sub categories geo locations
        /// </summary>
        public bool SumUpSubsGeoLocations { get; set; }

        /// <summary>
        /// category map name
        /// </summary>
        public string MapName { get; set; }

        /// <summary>
        /// Next Category without Ancestors/Children/Poems info
        /// </summary>
        public DivanCatViewModel Next { get; set; }

        /// <summary>
        /// Previous Category without Ancestors/Children/Poems info
        /// </summary>
        public DivanCatViewModel Previous { get; set; }

        /// <summary>
        /// ancestors
        /// </summary>
        public ICollection<DivanCatViewModel> Ancestors { get; set; }

        /// <summary>
        /// cat children
        /// </summary>
        public ICollection<DivanCatViewModel> Children { get; set; }

        /// <summary>
        /// poems
        /// </summary>
        public ICollection<DivanPoemSummaryViewModel> Poems { get; set; }

        /// <summary>
        /// paper sources
        /// </summary>
        public ICollection<DivanPaperSource> PaperSources { get; set; }

        /// <summary>
        /// new image
        /// </summary>
        public IFormFile NewImage { get; set; }

        public override string ToString()
        {
            return Title;
        }


    }
}
