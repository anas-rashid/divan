namespace RMuseum.Models.Divan.ViewModels
{
    /// <summary>
    /// Divan Page View Model
    /// </summary>
    public class DivanPageCompleteViewModel
    {
        /// <summary>
        /// id
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// Divan Page Type
        /// </summary>
        public DivanPageType DivanPageType { get; set; }

        /// <summary>
        /// title
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// cat + parent cats title + title
        /// </summary>
        public string FullTitle { get; set; }

        /// <summary>
        /// url => slug
        /// </summary>
        public string UrlSlug { get; set; }

        /// <summary>
        /// sample: /hafez/ghazal/sh1
        /// </summary>
        public string FullUrl { get; set; }

        /// <summary>
        /// Html Text
        /// </summary>
        public string HtmlText { get; set; }

        /// <summary>
        /// no index (search engines are blocked to index the page)
        /// </summary>
        public bool NoIndex { get; set; }

        /// <summary>
        /// if a page url is changed, store the old URL here to be redirected automatically
        /// </summary>
        public string RedirectFromFullUrl { get; set; }

        /// <summary>
        /// Poet or Cat
        /// </summary>
        public DivanPoetCompleteViewModel PoetOrCat { get; set; }

        /// <summary>
        /// Poem
        /// </summary>
        public DivanPoemCompleteViewModel Poem { get; set; }

        /// <summary>
        /// Second Poet
        /// </summary>
        public DivanPoetViewModel SecondPoet { get; set; }

        /// <summary>
        /// next normal page
        /// </summary>
        public DivanPageSummaryViewModel Next { get; set; }

        /// <summary>
        /// previous normal page
        /// </summary>
        public DivanPageSummaryViewModel Previous { get; set; }
    }
}
