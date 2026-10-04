using Microsoft.EntityFrameworkCore;
using RMuseum.Models.Artifact;
using RMuseum.Models.Bookmark;
using RMuseum.Models.DivanAudio;
using RMuseum.Models.UploadSession;
using RMuseum.Models.DivanIntegration;
using RMuseum.Models.ImportJob;
using RMuseum.Models.Note;
using RSecurityBackend.DbContext;
using RSecurityBackend.Models.Auth.Db;
using System;
using RMuseum.Models.Divan;
using RMuseum.Models.Divan.SemanticSearch;
using RMuseum.Models.MusicCatalogue;
using RMuseum.Models.Accounting;
using Microsoft.Extensions.Configuration;
using System.IO;
using RMuseum.Models.FAQ;
using RMuseum.Models.PDFLibrary;
using RMuseum.Models.ExternalFTPUpload;

namespace RMuseum.DbContext
{
    /// <summary>
    /// Museum Database Context
    /// </summary>
    public class RMuseumDbContext : RSecurityDbContext<RAppUser, RAppRole, Guid>
    {
        public RMuseumDbContext(DbContextOptions<RMuseumDbContext> options) : base(options)
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                   .SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json").AddEnvironmentVariables() // divan: honour env overrides (Docker)
                   .Build();
            if (bool.Parse(configuration["DatabaseMigrate"]))
            {
                Database.SetCommandTimeout(18000);//set time out for migration to 5 hours
                Database.Migrate();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="builder"></param>
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<RArtifactMasterRecord>()
                .HasIndex(m => m.FriendlyUrl)
                .IsUnique();

            builder.Entity<RArtifactItemRecord>()
                .HasIndex(i => new { i.RArtifactMasterRecordId, i.FriendlyUrl })
                .IsUnique();

            builder.Entity<RArtifactItemRecord>()
                .HasIndex(i => new { i.RArtifactMasterRecordId, i.Order })
                .IsUnique();

            builder.Entity<RTag>()
                .HasIndex(t => t.FriendlyUrl);

            builder.Entity<RTagValue>()
                .HasIndex(t => t.FriendlyUrl);

            builder.Entity<Recitation>()
                .HasIndex(p => p.DivanPostId);

            builder.Entity<DivanCat>()
                .HasIndex(c => c.FullUrl);

            builder.Entity<DivanPoem>()
                .HasIndex(c => c.FullUrl);

            builder.Entity<DivanPage>()
                 .HasIndex(c => c.FullUrl);

            builder.Entity<DivanSinger>()
                .HasIndex(c => c.Name);

            builder.Entity<DivanTrack>()
                .HasIndex(c => c.Name);

            builder.Entity<DivanComment>()
                .HasIndex(c => c.CommentDate);

            builder.Entity<ImportJob>()
               .Property(c => c.ProgressPercent)
               .HasColumnType("decimal(18,2)");

            builder.Entity<DivanComment>()
                .HasIndex(c => c.Status);

            builder.Entity<DivanPoem>()
                .HasIndex(c => c.Id);

            builder.Entity<Recitation>()
                .HasIndex(c => c.DivanAudioId);

            builder.Entity<Recitation>()
                .HasIndex(c => new { c.ReviewStatus, c.DivanPostId });

            builder.Entity<RArtifactMasterRecord>()
                .HasIndex(c => c.LastModified);

            builder.Entity<DivanPoet>()
               .HasIndex(c => new { c.Published, c.Id })
               .IncludeProperties(c => new { c.Name, c.Nickname, c.RImageId });

            builder.Entity<DivanCat>()
                .HasIndex(c => new { c.ParentId, c.PoetId })
                .IncludeProperties(c => c.Id);

            builder.Entity<PoemMusicTrack>()
                .HasIndex(c => new { c.Approved, c.Rejected });

            builder.Entity<RArtifactMasterRecord>()
                .HasIndex(c => new { c.CoverItemIndex, c.Status });


            builder.Entity<DivanLanguage>()
                .HasIndex(m => m.Name)
                .IsUnique();

            // DivanPersonRelation has two required FKs to the same table (DivanRelatedPerson) -
            // left at their EF Core default (Cascade, since both are required/non-nullable), SQL
            // Server refuses to create the second FK with "may cause cycles or multiple cascade
            // paths". Restricting one side (Person2) is enough to break the ambiguity; deleting a
            // person that's still referenced by a relation should be prevented at the application
            // level anyway (via a "still has family tree entries" check), not silently cascaded.
            builder.Entity<DivanPersonRelation>()
                .HasOne(r => r.Person1)
                .WithMany()
                .HasForeignKey(r => r.Person1Id)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<DivanPersonRelation>()
                .HasOne(r => r.Person2)
                .WithMany()
                .HasForeignKey(r => r.Person2Id)
                .OnDelete(DeleteBehavior.Restrict);

            // same two-required-FKs-to-the-same-table situation as DivanPersonRelation above
            builder.Entity<DivanPersonAffiliation>()
                .HasOne(a => a.Person1)
                .WithMany()
                .HasForeignKey(a => a.Person1Id)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<DivanPersonAffiliation>()
                .HasOne(a => a.Person2)
                .WithMany()
                .HasForeignKey(a => a.Person2Id)
                .OnDelete(DeleteBehavior.Restrict);

            // same two-required-FKs-to-the-same-table situation as DivanPersonRelation above, plus
            // a third optional FK to DivanPersonRelations itself (ExistingRelationId) - also
            // restricted, since a relation that still has a pending suggestion against it should be
            // resolved (or the suggestion rejected) before it can be deleted directly, not silently
            // orphan the suggestion
            builder.Entity<DivanPersonRelationEditSuggestion>()
                .HasOne(s => s.Person1)
                .WithMany()
                .HasForeignKey(s => s.Person1Id)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<DivanPersonRelationEditSuggestion>()
                .HasOne(s => s.Person2)
                .WithMany()
                .HasForeignKey(s => s.Person2Id)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<DivanPersonRelationEditSuggestion>()
                .HasOne(s => s.ExistingRelation)
                .WithMany()
                .HasForeignKey(s => s.ExistingRelationId)
                .OnDelete(DeleteBehavior.Restrict);

            // same Restrict treatment as ExistingRelationId above, but for Kind == Affiliation
            // suggestions targeting a DivanPersonAffiliation instead
            builder.Entity<DivanPersonRelationEditSuggestion>()
                .HasOne(s => s.ExistingAffiliation)
                .WithMany()
                .HasForeignKey(s => s.ExistingAffiliationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<DivanUserBookmark>()
                .HasIndex(b => new { b.UserId, b.PoemId, b.CoupletIndex });

            builder.Entity<DivanVerseNumber>()
                .HasIndex(n => new { n.NumberingId, n.PoemId, n.CoupletIndex })
                .IsUnique();

            builder.Entity<DivanVerseNumber>()
                .HasIndex(n => new { n.PoemId, n.CoupletIndex });

            builder.Entity<DivanVerse>()
                .HasIndex(v => v.PoemId);//the next statement causes a drop index in the migration which this line prevents it

            builder.Entity<DivanVerse>()
                .HasIndex(v => new { v.PoemId, v.CoupletIndex });

            builder.Entity<DivanCachedRelatedPoem>()
                .HasIndex(c => new { c.PoemId });

            builder.Entity<DivanCachedRelatedPoem>()
                .HasIndex(c => new { c.FullUrl });

            builder.Entity<DivanDonation>()
              .Property(c => c.Amount)
              .HasColumnType("decimal(18,2)");

            builder.Entity<DivanDonation>()
             .Property(c => c.Remaining)
             .HasColumnType("decimal(18,2)");

            builder.Entity<DivanExpense>()
             .Property(c => c.Amount)
             .HasColumnType("decimal(18,2)");

            builder.Entity<DonationExpenditure>()
            .Property(c => c.Amount)
            .HasColumnType("decimal(18,2)");

            builder.Entity<DivanGeoLocation>()
              .Property(c => c.Latitude)
              .HasColumnType("decimal(12,9)");

            builder.Entity<DivanGeoLocation>()
              .Property(c => c.Longitude)
              .HasColumnType("decimal(12,9)");

            builder.Entity<RecitationUserUpVote>()
               .HasIndex(v => new { v.RecitationId, v.UserId })
               .IsUnique();

            builder.Entity<DivanUserPoemVisit>()
                .HasIndex(v => v.UserId);

            builder.Entity<DivanUserPoemVisit>()
                .HasIndex(v => new { v.UserId, v.PoemId });

            builder.Entity<DivanPoemSection>()
                .HasIndex(v => new { v.PoemId, v.Index });

            builder.Entity<DivanPoemSection>()
                .HasIndex(v => new { v.RhymeLetters });

            builder.Entity<DivanPoemSection>()
                .HasIndex(v => new { v.DivanMetreId, v.RhymeLetters });

            builder.Entity<DivanPoemSection>()
                .HasIndex(v => new { v.DivanMetreId, v.RhymeLetters, v.Id});

            builder.Entity<DivanPoemSection>()
                .HasIndex(v => new { v.DivanMetreId, v.RhymeLetters, v.SectionType });

            builder.Entity<DivanCachedRelatedSection>()
                .HasIndex(v => new { v.PoemId, v.SectionIndex });

            builder.Entity<CategoryWordCount>()
               .HasIndex(v => new { v.CatId, v.Word })
               .IsUnique();

            builder.Entity<CategoryWordCountSummary>()
            .HasIndex(v => new { v.CatId })
            .IsUnique();


            builder.Entity<DivanVerse>()
              .Property(e => e.CoupletSummary)
              .HasMaxLength(4000);

           builder.Entity<DivanPoem>()
            .Property(e => e.Title)
            .HasMaxLength(1500);

            builder.Entity<DivanPoem>()
            .Property(e => e.FullTitle)
            .HasMaxLength(1500);

            builder.Entity<DivanPoem>()
            .Property(e => e.UrlSlug)
            .HasMaxLength(32);

            builder.Entity<DivanPoem>()
           .Property(e => e.RhymeLetters)
           .HasMaxLength(64);

            builder.Entity<DivanPoem>()
              .Property(e => e.SourceName)
              .HasMaxLength(64);

            builder.Entity<DivanPoem>()

             .Property(e => e.SourceUrlSlug)
             .HasMaxLength(16);

            builder.Entity<DivanPoem>()
              .Property(e => e.OldTag)
              .HasMaxLength(16);

            builder.Entity<DivanPoem>()
             .Property(e => e.OldTagPageUrl)
             .HasMaxLength(32);

            builder.Entity<DivanPoem>()
            .Property(e => e.Language)
            .HasMaxLength(8);

            builder.Entity<DivanPoem>()
            .Property(e => e.PoemSummary)
            .HasMaxLength(3000);

            builder.Entity<DivanCommentReaction>()
               .HasIndex(i => new { i.DivanCommentId, i.UserId })
               .IsUnique();

            builder.Entity<DivanCommentReaction>()
               .HasIndex(i => new { i.PoemId, i.UserId });

            builder.Entity<DivanComment>()
               .HasIndex(i => new { i.PoemId, i.SortKey });

            builder.Entity<DivanComment>()
               .HasIndex(i => new { i.PoemId, i.CommentDate });

        }


        /// <summary>
        /// Picture Files
        /// </summary>
        public DbSet<RPictureFile> PictureFiles { get; set; }

        /// <summary>
        /// Item Attributes
        /// </summary>
        public DbSet<RTag> Tags { get; set; }

        /// <summary>
        /// Artifacts
        /// </summary>
        public DbSet<RArtifactMasterRecord> Artifacts { get; set; }

        /// <summary>
        /// Items
        /// </summary>
        public DbSet<RArtifactItemRecord> Items { get; set; }


        /// <summary>
        /// Import Jobs
        /// </summary>
        public DbSet<ImportJob> ImportJobs { get; set; }

        /// <summary>
        /// Tags
        /// </summary>
        public DbSet<RTagValue> TagValues { get; set; }

        /// <summary>
        /// User Bookmarks
        /// </summary>
        public DbSet<RUserBookmark> UserBookmarks { get; set; }

        /// <summary>
        /// User Notes
        /// </summary>
        public DbSet<RUserNote> UserNotes { get; set; }

        /// <summary>
        /// Divan Links
        /// </summary>
        public DbSet<DivanLink> DivanLinks { get; set; }


        /// <summary>
        /// Pinterest Links
        /// </summary>
        public DbSet<PinterestLink> PinterestLinks { get; set; }

        /// <summary>
        /// Divan Audio Files
        /// </summary>
        public DbSet<Recitation> Recitations { get; set; }

        /// <summary>
        /// Upload Sessions
        /// </summary>
        public DbSet<UploadSession> UploadSessions { get; set; }

        /// <summary>
        /// Uploaded files
        /// </summary>
        public DbSet<UploadSessionFile> UploadedFiles { get; set; }

        /// <summary>
        /// User Recitation Profiles
        /// </summary>
        public DbSet<UserRecitationProfile> UserRecitationProfiles { get; set; }

        /// <summary>
        /// Divan Poets
        /// </summary>
        public DbSet<DivanPoet> DivanPoets { get; set; }

        /// <summary>
        /// Divan Categories
        /// </summary>
        public DbSet<DivanCat> DivanCategories { get; set; }

        /// <summary>
        /// Divan Poems
        /// </summary>
        public DbSet<DivanPoem> DivanPoems { get; set; }

        /// <summary>
        /// Semantic search query log — see SemanticSearchQueryLog for what is (and deliberately
        /// isn't) recorded
        /// </summary>
        public DbSet<SemanticSearchQueryLog> SemanticSearchQueryLogs { get; set; }

        /// <summary>
        /// Divan Verses
        /// </summary>
        public DbSet<DivanVerse> DivanVerses { get; set; }

        /// <summary>
        /// Narration Publishing Tracker
        /// </summary>
        public DbSet<RecitationPublishingTracker> RecitationPublishingTrackers { get; set; }

        /// <summary>
        /// Divan Pages
        /// </summary>
        public DbSet<DivanPage> DivanPages { get; set; }

        /// <summary>
        /// Divan Metres
        /// </summary>
        public DbSet<DivanMetre> DivanMetres { get; set; }


        /// <summary>
        /// singers
        /// </summary>
        public DbSet<DivanSinger> DivanSingers { get; set; }

        /// <summary>
        /// music tracks
        /// </summary>
        public DbSet<DivanTrack> DivanMusicCatalogueTracks { get; set; }

        /// <summary>
        /// golha tracks
        /// </summary>
        public DbSet<GolhaTrack> GolhaTracks { get; set; }

        /// <summary>
        /// GolhaCollection 
        /// </summary>
        public DbSet<GolhaCollection> GolhaCollections { get; set; }

        /// <summary>
        /// GolhaPrograms 
        /// </summary>
        public DbSet<GolhaProgram> GolhaPrograms { get; set; }

        /// <summary>
        /// PoemMusicTracks
        /// </summary>
        public DbSet<PoemMusicTrack> DivanPoemMusicTracks { get; set; }

        /// <summary>
        /// Divan Comments
        /// </summary>
        public DbSet<DivanComment> DivanComments { get; set; }

        /// <summary>
        /// Divan Reported Comments
        /// </summary>
        public DbSet<DivanCommentAbuseReport> DivanReportedComments { get; set; }

        /// <summary>
        /// Divan Page Snapshots
        /// </summary>
        public DbSet<DivanPageSnapshot> DivanPageSnapshots { get; set; }


        /// <summary>
        /// Divan Site Bannaers
        /// </summary>
        public DbSet<DivanSiteBanner> DivanSiteBanners { get; set; }

        /// <summary>
        /// Divan Health Check Errors
        /// </summary>
        public DbSet<DivanHealthCheckError> DivanHealthCheckErrors { get; set; }

        /// <summary>
        /// donations
        /// </summary>
        public DbSet<DivanDonation> DivanDonations { get; set; }

        /// <summary>
        /// expenses
        /// </summary>
        public DbSet<DivanExpense> DivanExpenses { get; set; }

        /// <summary>
        /// donation expenditures
        /// </summary>
        public DbSet<DonationExpenditure> DonationExpenditure { get; set; }

        /// <summary>
        /// poem corrections
        /// </summary>
        public DbSet<DivanPoemCorrection> DivanPoemCorrections { get; set; }

        /// <summary>
        /// languages for translation
        /// </summary>
        public DbSet<DivanLanguage> DivanLanguages { get; set; }

        /// <summary>
        /// divan bookmarks
        /// </summary>
        public DbSet<DivanUserBookmark> DivanUserBookmarks { get; set; }

        /// <summary>
        /// divan numbering schemas
        /// </summary>
        public DbSet<DivanNumbering> DivanNumberings { get; set; }

        /// <summary>
        /// divan verse numbers
        /// </summary>
        public DbSet<DivanVerseNumber> DivanVerseNumbers { get; set; }

        /// <summary>
        /// divan half centuries
        /// </summary>
        public DbSet<DivanCentury> DivanCenturies { get; set; }

        /// <summary>
        /// divan cities
        /// </summary>
        public DbSet<DivanGeoLocation> DivanGeoLocations { get; set; }

        /// <summary>
        /// related poems to each poem (having same rhyme letters and prosody metre)
        /// </summary>
        public DbSet<DivanCachedRelatedPoem> DivanCachedRelatedPoems { get; set; }

        /// <summary>
        /// Reported User Notes
        /// </summary>
        public DbSet<RUserNoteAbuseReport> ReportedUserNotes { get; set; }

        /// <summary>
        /// recitation error reports
        /// </summary>
        public DbSet<RecitationErrorReport> RecitationErrorReports { get; set; }

        /// <summary>
        /// recitation user up votes
        /// </summary>
        public DbSet<RecitationUserUpVote> RecitationUserUpVotes { get; set; }

        /// <summary>
        /// recitation approved mistakes
        /// </summary>
        public DbSet<RecitationApprovedMistake> RecitationApprovedMistakes { get; set; }

        /// <summary>
        /// probable metres
        /// </summary>
        public DbSet<DivanPoemProbableMetre> DivanPoemProbableMetres { get; set; }

        /// <summary>
        /// divan user history track items (stored by his or her choice)
        /// </summary>
        public DbSet<DivanUserPoemVisit> DivanUserPoemVisits { get; set; }

        /// <summary>
        /// suggested spec line for poets
        /// </summary>
        public DbSet<DivanPoetSuggestedSpecLine> DivanPoetSuggestedSpecLines { get; set; }

        /// <summary>
        /// suggested pictures for poets
        /// </summary>
        public DbSet<DivanPoetSuggestedPicture> DivanPoetSuggestedPictures { get; set; }

        /// <summary>
        /// faq categories
        /// </summary>
        public DbSet<FAQCategory> FAQCategories { get; set; }

        /// <summary>
        /// faq items
        /// </summary>
        public DbSet<FAQItem> FAQItems { get; set; }

        /// <summary>
        /// Temporary Model contianing duplicated poems information
        /// </summary>
        public DbSet<DivanDuplicate> DivanDuplicates { get; set; }

        /// <summary>
        /// poem sections
        /// </summary>
        public DbSet<DivanPoemSection> DivanPoemSections { get; set; }

        /// <summary>
        /// related sections to each section (having same rhyme letters and prosody metre)
        /// </summary>
        public DbSet<DivanCachedRelatedSection> DivanCachedRelatedSections { get; set; }

        /// <summary>
        /// section correctons
        /// </summary>
        public DbSet<DivanPoemSectionCorrection> DivanPoemSectionCorrections { get; set; }


        /// <summary>
        /// Updating related sections logs
        /// </summary>
        public DbSet<UpdatingRelSectsLog> UpdatingRelSectsLogs{ get; set; }

        /// <summary>
        /// PoemGeoDateTags
        /// </summary>
        public DbSet<PoemGeoDateTag> PoemGeoDateTags { get; set; }

        /// <summary>
        /// People tags
        /// </summary>
        public DbSet<DivanRelatedPerson> DivanRelatedPersons { get; set; }

        /// <summary>
        /// approved kinship edges between people (family tree) - see DivanPersonRelation
        /// </summary>
        public DbSet<DivanPersonRelation> DivanPersonRelations { get; set; }

        /// <summary>
        /// approved non-family ties between people (e.g. minister-to-king) - see DivanPersonAffiliation
        /// </summary>
        public DbSet<DivanPersonAffiliation> DivanPersonAffiliations { get; set; }

        /// <summary>
        /// pending/reviewed suggested edits to an already-approved DivanRelatedPerson's own fields -
        /// see DivanPersonEditSuggestion
        /// </summary>
        public DbSet<DivanPersonEditSuggestion> DivanPersonEditSuggestions { get; set; }

        /// <summary>
        /// pending/reviewed suggested additions, changes or removals of a kinship edge between two
        /// already-approved people - see DivanPersonRelationEditSuggestion
        /// </summary>
        public DbSet<DivanPersonRelationEditSuggestion> DivanPersonRelationEditSuggestions { get; set; }

        /// <summary>
        /// Books (PDF Library)
        /// </summary>
        public DbSet<Book> Books { get; set; }

        /// <summary>
        /// Authurs
        /// </summary>
        public DbSet<Author> Authors { get; set; }

        /// <summary>
        /// Multi Volume PDF Collections
        /// </summary>
        public DbSet<MultiVolumePDFCollection> MultiVolumePDFCollections { get; set; }

        /// <summary>
        /// PDF Books
        /// </summary>
        public DbSet<PDFBook> PDFBooks { get; set; }

        /// <summary>
        /// PDF Pages
        /// </summary>
        public DbSet<PDFPage> PDFPages { get; set; }

        /// <summary>
        /// PDF Sources
        /// </summary>
        public DbSet<PDFSource> PDFSources { get; set; }

        /// <summary>
        /// Queued FTP Uploads
        /// </summary>
        public DbSet<QueuedFTPUpload> QueuedFTPUploads { get; set; }

        /// <summary>
        /// PDF Divan Links
        /// </summary>
        public DbSet<PDFDivanLink> PDFDivanLinks { get; set; }

        /// <summary>
        /// OCR Queue Items
        /// </summary>
        public DbSet<OCRQueue> OCRQueuedItems { get; set; }

        /// <summary>
        /// PDF Download Queue
        /// </summary>
        public DbSet<QueuedPDFBook> QueuedPDFBooks { get; set; }

        /// <summary>
        /// Related Poems
        /// </summary>
        public DbSet<DivanQuotedPoem> DivanQuotedPoems { get; set; }


        /// <summary>
        /// discover quoted q items
        /// </summary>
        public DbSet<DiscoverQuotedQueueItem> DiscoverQuotedQueueItems { get; set; }

        /// <summary>
        /// paper sources
        /// </summary>
        public DbSet<DivanPaperSource> DivanPaperSources { get; set; }

        /// <summary>
        /// digital sources
        /// </summary>
        public DbSet<DigitalSource> DigitalSources { get; set; }

        /// <summary>
        /// Category Word Counts
        /// </summary>
        public DbSet<CategoryWordCount> CategoryWordCounts { get; set; }

        /// <summary>
        /// Category Word Count Summaries
        /// </summary>
        public DbSet<CategoryWordCountSummary> CategoryWordCountSummaries { get; set; }

        /// <summary>
        /// divan cat corrections
        /// </summary>
        public DbSet<DivanCatCorrection> DivanCatCorrections { get; set; }

        /// <summary>
        /// comment reactions
        /// </summary>
        public DbSet<DivanCommentReaction> DivanCommentReactions { get; set; }

    }
}
