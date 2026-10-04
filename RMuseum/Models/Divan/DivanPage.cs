using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RMuseum.Models.Divan
{
    /// <summary>
    /// Divan Page
    /// </summary>
    public class DivanPage
    {
        /// <summary>
        /// id
        /// </summary>
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }

        /// <summary>
        /// Page Type
        /// </summary>
        public DivanPageType DivanPageType { get; set; }

        /// <summary>
        /// Published
        /// </summary>
        public bool Published { get; set; }

        /// <summary>
        /// page order
        /// </summary>
        public int PageOrder { get; set; }

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
        /// parent id
        /// </summary>
        public int? ParentId { get; set; }

        /// <summary>
        /// parent
        /// </summary>
        public virtual DivanPage Parent { get; set; }

        /// <summary>
        /// related poet id
        /// </summary>
        public int? PoetId { get; set; }

        /// <summary>
        /// related poet
        /// </summary>
        public virtual DivanPoet Poet { get; set; }

        /// <summary>
        /// related category id
        /// </summary>
        public int? CatId { get; set; }

        /// <summary>
        /// related category
        /// </summary>
        public virtual DivanCat Cat { get; set; }

        /// <summary>
        /// related poem id
        /// </summary>
        public int? PoemId { get; set; }

        /// <summary>
        /// related poem
        /// </summary>
        public virtual DivanPoem Poem { get; set; }

        /// <summary>
        /// second poet id
        /// </summary>
        public int? SecondPoetId { get; set; }

        /// <summary>
        /// second poet
        /// </summary>
        public virtual DivanPoet SecondPoet { get; set; }

        /// <summary>
        /// post date
        /// </summary>
        public DateTime PostDate { get; set; }

        /// <summary>
        /// no index (search engines are blocked to index the page)
        /// </summary>
        public bool NoIndex { get; set; }

        /// <summary>
        /// if a page url is changed, store the old URL here to be redirected automatically
        /// </summary>
        public string RedirectFromFullUrl { get; set; }
    }
}
