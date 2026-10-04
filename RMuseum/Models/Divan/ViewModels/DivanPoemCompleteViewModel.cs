using RMuseum.Models.DivanAudio.ViewModels;

namespace RMuseum.Models.Divan.ViewModels
{
    /// <summary>
    /// a more complete DivanPoem View Model
    /// </summary>
    public class DivanPoemCompleteViewModel
    {
        /// <summary>
        /// Id
        /// </summary>
        public int Id { get; set; }

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
        /// verses text
        /// </summary>
        public string PlainText { get; set; }

        /// <summary>
        /// verses text as html (ganjoor.net format)
        /// </summary>
        public string HtmlText { get; set; }

        /// <summary>
        /// source name
        /// </summary>
        public string SourceName { get; set; }

        /// <summary>
        /// source url slug
        /// </summary>
        public string SourceUrlSlug { get; set; }

        /// <summary>
        /// old collection or book name for Saadi's ghazalyiat (طیبات، خواتیم و ....)
        /// </summary>
        public string OldTag { get; set; }

        /// <summary>
        /// old collection page url e.g /saadi/tayyebat
        /// </summary>
        public string OldTagPageUrl { get; set; }

        /// <summary>
        /// order when mixed with categories
        /// </summary>
        public int MixedModeOrder { get; set; }

        /// <summary>
        /// published
        /// </summary>
        public bool Published { get; set; }

        /// <summary>
        /// language
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// poem summary
        /// </summary>
        public string PoemSummary { get; set; }

        /// <summary>
        /// category
        /// </summary>
        public DivanPoetCompleteViewModel Category { get; set; }

        /// <summary>
        /// Next Poem
        /// </summary>
        public DivanPoemSummaryViewModel Next { get; set; }

        /// <summary>
        /// Previous Poem
        /// </summary>
        public DivanPoemSummaryViewModel Previous { get; set; }

        /// <summary>
        /// verses
        /// </summary>
        public DivanVerseViewModel[] Verses { get; set; }

        /// <summary>
        /// Recitations
        /// </summary>
        public PublicRecitationViewModel[] Recitations { get; set; }

        /// <summary>
        /// Images
        /// </summary>
        public PoemRelatedImage[] Images { get; set; }

        /// <summary>
        /// Songs
        /// </summary>
        public PoemMusicTrackViewModel[] Songs { get; set; }

        /// <summary>
        /// Comments
        /// </summary>
        public DivanCommentSummaryViewModel[] Comments { get; set; }

        /// <summary>
        /// poem sections
        /// </summary>
        public DivanPoemSection[] Sections { get; set; }

        /// <summary>
        /// geo/date tags
        /// </summary>
        public PoemGeoDateTag[] GeoDateTags { get; set; }

        /// <summary>
        /// top 6 quoted poems
        /// </summary>
        public DivanQuotedPoemViewModel[] Top6QuotedPoems { get; set; }

        /// <summary>
        /// section index
        /// </summary>
        public int? SectionIndex { get; set; }

        /// <summary>
        /// poem is claimed by multiple poets
        /// </summary>
        public bool ClaimedByMultiplePoets { get; set; }

        /// <summary>
        /// couplets count
        /// </summary>
        public int? CoupletsCount { get; set; }
    }
}
