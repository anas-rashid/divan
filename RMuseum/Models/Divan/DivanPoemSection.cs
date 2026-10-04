using System.ComponentModel.DataAnnotations.Schema;

namespace RMuseum.Models.Divan
{
    /// <summary>
    /// Divan Poem Section
    /// </summary>
    public class DivanPoemSection
    {
        /// <summary>
        /// record id
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// Poem Id
        /// </summary>
        public int PoemId { get; set; }

        /// <summary>
        /// Poem
        /// </summary>
        public DivanPoem Poem { get; set; }

        /// <summary>
        /// poet id might be different from DivanPoem.PoetId
        /// </summary>
        public int? PoetId { get; set; }

        /// <summary>
        /// poet might be different from DivanPoem.PoetId
        /// </summary>
        public virtual DivanPoet Poet { get; set; }

        /// <summary>
        /// use this field instead of Id for referencing to ease record deletion for verses, Index starts at 0, each poem should have at least one non-virtual part ordered by Index without break
        /// </summary>
        public int Index { get; set; }

        /// <summary>
        /// for lots of poems Number equals to Index + 1, but for rare ones such as a paragraph containing multi-band poem this could be different, 0 means no visible numbers
        /// </summary>
        public int Number { get; set; }

        /// <summary>
        /// section type
        /// </summary>
        public PoemSectionType SectionType { get; set; }

        /// <summary>
        /// how DivanPoemSection is linked to DivanVerse
        /// </summary>
        public VersePoemSectionType VerseType { get; set; }

        /// <summary>
        /// prosody information
        /// </summary>
        /// <remarks>
        /// in fact this should be a many-to-many relationship, but our current dataset lacks such a relationship instance
        /// </remarks>
        public int? DivanMetreId { get; set; }

        /// <summary>
        /// metre
        /// </summary>
        public virtual DivanMetre DivanMetre { get; set; }

        /// <summary>
        /// for non WholePoem sections, there must be a reference section for meters in order to make them updatable
        /// </summary>
        public int? DivanMetreRefSectionIndex { get; set; }

        /// <summary>
        /// rhyme letters
        /// </summary>
        public string RhymeLetters { get; set; }

        /// <summary>
        /// verses text
        /// </summary>
        public string PlainText { get; set; }

        /// <summary>
        /// verses text as html (ganjoor.net format)
        /// </summary>
        public string HtmlText { get; set; }

        /// <summary>
        /// valid for whole poem sections
        /// </summary>
        public DivanPoemFormat? PoemFormat { get; set; }

        /// <summary>
        /// first couplet index
        /// </summary>
        public int CachedFirstCoupletIndex { get; set; }

        /// <summary>
        /// language, null means farsi
        /// </summary>
        public string Language { get; set; }

        /// <summary>
        /// couplets count
        /// </summary>
        public int CoupletsCount { get; set; }

        /// <summary>
        /// top 6 related sections
        /// </summary>
        [NotMapped]
        public DivanCachedRelatedSection[] Top6RelatedSections { get; set; }

        /// <summary>
        /// old metre id to see it needs refreshing related sections
        /// </summary>
        [NotMapped]
        public int? OldDivanMetreId { get; set; }

        /// <summary>
        /// old rhyme letters to see it needs refreshing related sections
        /// </summary>
        [NotMapped]
        public string OldRhymeLetters { get; set; }

        /// <summary>
        /// modified
        /// </summary>
        [NotMapped]
        public bool Modified { get; set; }

        /// <summary>
        /// excerpt
        /// </summary>
        [NotMapped]
        public string Excerpt { get; set; }
    }
}
