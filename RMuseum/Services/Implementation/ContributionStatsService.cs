using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RMuseum.DbContext;
using RMuseum.Models.Divan.ViewModels;
using RMuseum.Models.Generic.ViewModels;
using RSecurityBackend.Models.Generic;
using RSecurityBackend.Services;
using RSecurityBackend.Services.Implementation;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RMuseum.Services.Implementation
{
    /// <summary>
    /// contributions stats service implementation
    /// </summary>
    public class ContributionStatsService : IContributionStatsService
    {

        /// <summary>
        /// user contributions
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<UserContributionsViewModel>> GetUserContributionsAsync(Guid userId)
        {
            try
            {
                return new RServiceResult<UserContributionsViewModel>
                    (
                    new UserContributionsViewModel()
                    {
                        Id = userId,
                        CreateDate = (await _context.Users.Where(u => u.Id == userId).SingleAsync()).CreateDate,
                        PoemCorrections = await _context.DivanPoemCorrections.Where(c => c.AffectedThePoem && c.UserId == userId).CountAsync(),
                        SectionCorrections = await _context.DivanPoemSectionCorrections.Where(c => c.AffectedThePoem && c.UserId == userId).CountAsync(),
                        CatCorrections = await _context.DivanCatCorrections.Where(c => c.Result == Models.Divan.CorrectionReviewResult.Approved && c.UserId == userId).CountAsync(),
                        SuggestedSongs = await _context.DivanPoemMusicTracks.Where(c => c.Approved && c.SuggestedById == userId).CountAsync(),
                        QuotedPoems = await _context.DivanQuotedPoems.Where(c => c.Published && c.SuggestedById == userId).CountAsync(),
                        Comments = await _context.DivanComments.Where(c => c.Status == Models.Artifact.PublishStatus.Published && c.UserId == userId).CountAsync(),
                        Recitations = await _context.Recitations.Where(c => c.ReviewStatus == Models.DivanAudio.AudioReviewStatus.Approved && c.OwnerId == userId).CountAsync(),
                        MuseumLinks = await _context.DivanLinks.Where(c => c.ReviewResult == Models.DivanIntegration.ReviewResult.Approved && c.SuggestedById == userId).CountAsync(),
                        PinterestLinks = await _context.PinterestLinks.Where(c => c.HumanReviewed && c.ReviewResult == Models.DivanIntegration.ReviewResult.Approved && c.SuggestedById == userId).CountAsync(),
                        PoetSpecLines = await _context.DivanPoetSuggestedSpecLines.Where(c => c.Published && c.SuggestedById == userId).CountAsync(),
                        PoetPictures = await _context.DivanPoetSuggestedPictures.Where(c => c.Published && c.SuggestedById == userId).CountAsync(),
                        PublicUserNotes = await _context.UserNotes.Where(c => c.Status == Models.Artifact.PublishStatus.Published && c.NoteType == Models.Note.RNoteType.Public && c.RAppUserId == userId).CountAsync()
                    }
                    );
                  
            }
            catch (Exception exp)
            {
                return new RServiceResult<UserContributionsViewModel>(null, exp.ToString());
            }
        }

        /// <summary>
        /// users grouped by signup date
        /// </summary>
        /// <param name="paging"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>> GetUsersGroupedByDateAsync(PagingParameterModel paging)
        {
            try
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>(
                    await QueryablePaginator<GroupedByDateViewModel>.Paginate(
                   _context.Users
                        .GroupBy(a => a.CreateDate.Date)
                        .Select(a => new GroupedByDateViewModel()
                        {
                            Date = a.Key.Date.ToString(),
                            Number = a.Count(),
                        }).OrderByDescending(s => s.Date)
                   , paging));
            }
            catch (Exception e)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>((null, null), e.ToString());
            }
        }

        /// <summary>
        /// summed up stats of users (Days and UserIds are invalid)
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<SummedUpViewModel>> GetUsersSummedUpStatsAsync()
        {
            try
            {
                return new RServiceResult<SummedUpViewModel>
                    (
                    new SummedUpViewModel()
                    {
                        Days = -1,
                        TotalCount = await _context.Users.CountAsync(),
                        UserIds = -1,
                    }
                    );

            }
            catch (Exception e)
            {
                return new RServiceResult<SummedUpViewModel>(null, e.ToString());
            }
        }



        /// <summary>
        /// approved edits daily
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>> GetApprovedEditsGroupedByDateAsync(PagingParameterModel paging, Guid? userId)
        {
            try
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>(
                    await QueryablePaginator<GroupedByDateViewModel>.Paginate(
                   _context.DivanPoemCorrections
                        .Where(c =>
                        c.AffectedThePoem == true
                        &&
                        (userId == null || c.UserId == userId)
                        )
                        .GroupBy(a => a.Date.Date)
                        .Select(a => new GroupedByDateViewModel()
                        {
                            Date = a.Key.Date.ToString(),
                            Number = a.Count(),
                        }).OrderByDescending(s => s.Date)
                   , paging));
            }
            catch (Exception e)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>((null, null), e.ToString());
            }
        }

        /// <summary>
        /// approved edits grouped by user
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="day"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>> GetApprovedEditsGroupedByUserAsync(PagingParameterModel paging, DateTime? day, Guid? userId)
        {
            try
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>(
                    await QueryablePaginator<GroupedByUserViewModel>.Paginate(
                        _context.DivanPoemCorrections
                        .Join
                        (
                            _context.Users,
                            correction => correction.UserId,
                            user => user.Id,
                            (correction, user) => new
                            {
                                correction.Date,
                                UserId = user.Id,
                                UserName = user.NickName,
                                correction.AffectedThePoem,
                            }
                        )
                        .Where(f =>
                         f.AffectedThePoem
                        &&
                        (day == null || f.Date.Date == day) && (userId == null || f.UserId == userId))
                        .GroupBy(a => new { a.UserId, a.UserName }).Select(a => new GroupedByUserViewModel()
                        {
                            UserId = a.Key.UserId,
                            UserName = a.Key.UserName,
                            Number = a.Count(),
                        }).OrderByDescending(s => s.Number)
                        , paging));

            }
            catch (Exception e)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>((null, null), e.ToString());
            }
        }

        /// <summary>
        /// summed up stats of approved poem corrections
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<SummedUpViewModel>> GetApprrovedEditsSummedUpStatsAsync()
        {
            try
            {
                return new RServiceResult<SummedUpViewModel>
                    (
                    new SummedUpViewModel()
                    {
                        Days = await _context.DivanPoemCorrections
                        .Where(f => f.AffectedThePoem
                        )
                        .GroupBy(f => f.Date.Date).CountAsync(),
                        TotalCount = await _context.DivanPoemCorrections

                        .Where(f => f.AffectedThePoem
                        )
                        .CountAsync(),
                        UserIds = await _context.DivanPoemCorrections
                        .Where(f => f.AffectedThePoem
                        )
                        .GroupBy(f => f.UserId).CountAsync(),
                    }
                    );

            }
            catch (Exception e)
            {
                return new RServiceResult<SummedUpViewModel>(null, e.ToString());
            }
        }

        /// <summary>
        /// approved section edits daily
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>> GetApprovedSectionEditsGroupedByDateAsync(PagingParameterModel paging, Guid? userId)
        {
            try
            {
                string systemEmail = $"{Configuration.GetSection("Divan")["SystemEmail"]}";
                var systemUserId = (Guid)(await _appUserService.FindUserByEmail(systemEmail)).Result.Id;

                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>(
                    await QueryablePaginator<GroupedByDateViewModel>.Paginate(
                   _context.DivanPoemSectionCorrections
                        .Where(c =>
                        c.AffectedThePoem == true && c.UserId != systemUserId
                        &&
                        (userId == null || c.UserId == userId)
                        )
                        .GroupBy(a => a.Date.Date)
                        .Select(a => new GroupedByDateViewModel()
                        {
                            Date = a.Key.Date.ToString(),
                            Number = a.Count(),
                        }).OrderByDescending(s => s.Date)
                   , paging));
            }
            catch (Exception e)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>((null, null), e.ToString());
            }
        }

        /// <summary>
        /// approved section edits grouped by user
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="day"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>> GetApprovedSectionEditsGroupedByUserAsync(PagingParameterModel paging, DateTime? day, Guid? userId)
        {
            try
            {
                string systemEmail = $"{Configuration.GetSection("Divan")["SystemEmail"]}";
                var systemUserId = (Guid)(await _appUserService.FindUserByEmail(systemEmail)).Result.Id;

                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>(
                    await QueryablePaginator<GroupedByUserViewModel>.Paginate(
                        _context.DivanPoemSectionCorrections
                        .Join
                        (
                            _context.Users,
                            correction => correction.UserId,
                            user => user.Id,
                            (correction, user) => new
                            {
                                correction.Date,
                                UserId = user.Id,
                                UserName = user.NickName,
                                correction.AffectedThePoem,
                            }
                        )
                        .Where(f =>
                         f.AffectedThePoem && f.UserId != systemUserId
                        &&
                        (day == null || f.Date.Date == day) && (userId == null || f.UserId == userId))
                        .GroupBy(a => new { a.UserId, a.UserName }).Select(a => new GroupedByUserViewModel()
                        {
                            UserId = a.Key.UserId,
                            UserName = a.Key.UserName,
                            Number = a.Count(),
                        }).OrderByDescending(s => s.Number)
                        , paging));

            }
            catch (Exception e)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>((null, null), e.ToString());
            }
        }

        /// <summary>
        /// summed up stats of approved section corrections
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<SummedUpViewModel>> GetApprrovedSectionEditsSummedUpStatsAsync()
        {
            try
            {
                string systemEmail = $"{Configuration.GetSection("Divan")["SystemEmail"]}";
                var systemUserId = (Guid)(await _appUserService.FindUserByEmail(systemEmail)).Result.Id;

                return new RServiceResult<SummedUpViewModel>
                    (
                    new SummedUpViewModel()
                    {
                        Days = await _context.DivanPoemSectionCorrections
                        .Where(f => f.AffectedThePoem && f.UserId != systemUserId
                        )
                        .GroupBy(f => f.Date.Date).CountAsync(),
                        TotalCount = await _context.DivanPoemSectionCorrections

                        .Where(f => f.AffectedThePoem && f.UserId != systemUserId
                        )
                        .CountAsync(),
                        UserIds = await _context.DivanPoemSectionCorrections
                        .Where(f => f.AffectedThePoem && f.UserId != systemUserId
                        )
                        .GroupBy(f => f.UserId).CountAsync(),
                    }
                    );

            }
            catch (Exception e)
            {
                return new RServiceResult<SummedUpViewModel>(null, e.ToString());
            }
        }


        /// <summary>
        /// approved cat edits daily
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>> GetApprovedCatEditsGroupedByDateAsync(PagingParameterModel paging, Guid? userId)
        {
            try
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>(
                    await QueryablePaginator<GroupedByDateViewModel>.Paginate(
                   _context.DivanCatCorrections
                        .Where(c =>
                        c.Result == Models.Divan.CorrectionReviewResult.Approved
                        &&
                        (userId == null || c.UserId == userId)
                        )
                        .GroupBy(a => a.Date.Date)
                        .Select(a => new GroupedByDateViewModel()
                        {
                            Date = a.Key.Date.ToString(),
                            Number = a.Count(),
                        }).OrderByDescending(s => s.Date)
                   , paging));
            }
            catch (Exception e)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>((null, null), e.ToString());
            }
        }

        /// <summary>
        /// approved cat edits grouped by user
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="day"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>> GetApprovedCatEditsGroupedByUserAsync(PagingParameterModel paging, DateTime? day, Guid? userId)
        {
            try
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>(
                    await QueryablePaginator<GroupedByUserViewModel>.Paginate(
                        _context.DivanCatCorrections
                        .Join
                        (
                            _context.Users,
                            correction => correction.UserId,
                            user => user.Id,
                            (correction, user) => new
                            {
                                correction.Date,
                                UserId = user.Id,
                                UserName = user.NickName,
                                correction.Result,
                            }
                        )
                        .Where(f =>
                         f.Result == Models.Divan.CorrectionReviewResult.Approved
                        &&
                        (day == null || f.Date.Date == day) && (userId == null || f.UserId == userId))
                        .GroupBy(a => new { a.UserId, a.UserName }).Select(a => new GroupedByUserViewModel()
                        {
                            UserId = a.Key.UserId,
                            UserName = a.Key.UserName,
                            Number = a.Count(),
                        }).OrderByDescending(s => s.Number)
                        , paging));

            }
            catch (Exception e)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>((null, null), e.ToString());
            }
        }

        /// <summary>
        /// summed up stats of approved cat corrections
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<SummedUpViewModel>> GetApprrovedCatEditsSummedUpStatsAsync()
        {
            try
            {
                return new RServiceResult<SummedUpViewModel>
                    (
                    new SummedUpViewModel()
                    {
                        Days = await _context.DivanCatCorrections
                        .Where(f => f.Result == Models.Divan.CorrectionReviewResult.Approved
                        )
                        .GroupBy(f => f.Date.Date).CountAsync(),
                        TotalCount = await _context.DivanCatCorrections

                        .Where(f => f.Result == Models.Divan.CorrectionReviewResult.Approved
                        )
                        .CountAsync(),
                        UserIds = await _context.DivanCatCorrections
                        .Where(f => f.Result == Models.Divan.CorrectionReviewResult.Approved
                        )
                        .GroupBy(f => f.UserId).CountAsync(),
                    }
                    );

            }
            catch (Exception e)
            {
                return new RServiceResult<SummedUpViewModel>(null, e.ToString());
            }
        }


        /// <summary>
        /// approved related songs daily
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>> GetApprovedRelatedSongsGroupedByDateAsync(PagingParameterModel paging, Guid? userId)
        {
            try
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>(
                    await QueryablePaginator<GroupedByDateViewModel>.Paginate(
                   _context.DivanPoemMusicTracks
                        .Where(c =>
                        c.Approved
                        &&
                        (userId == null || c.SuggestedById == userId)
                        )
                        .GroupBy(a => a.ApprovalDate.Date)
                        .Select(a => new GroupedByDateViewModel()
                        {
                            Date = a.Key.Date.ToString(),
                            Number = a.Count(),
                        }).OrderByDescending(s => s.Date)
                   , paging));
            }
            catch (Exception e)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>((null, null), e.ToString());
            }
        }

        /// <summary>
        /// approved related songs grouped by user
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="day"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>> GetApprovedRelatedSongsGroupedByUserAsync(PagingParameterModel paging, DateTime? day, Guid? userId)
        {
            try
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>(
                    await QueryablePaginator<GroupedByUserViewModel>.Paginate(
                        _context.DivanPoemMusicTracks
                        .Join
                        (
                            _context.Users,
                            correction => correction.SuggestedById,
                            user => user.Id,
                            (correction, user) => new
                            {
                                correction.ApprovalDate,
                                UserId = user.Id,
                                UserName = user.NickName,
                                correction.Approved,
                            }
                        )
                        .Where(f =>
                         f.Approved
                        &&
                        (day == null || f.ApprovalDate.Date == day) && (userId == null || f.UserId == userId))
                        .GroupBy(a => new { a.UserId, a.UserName }).Select(a => new GroupedByUserViewModel()
                        {
                            UserId = a.Key.UserId,
                            UserName = a.Key.UserName,
                            Number = a.Count(),
                        }).OrderByDescending(s => s.Number)
                        , paging));

            }
            catch (Exception e)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>((null, null), e.ToString());
            }
        }

        /// <summary>
        /// summed up stats of approved related songs
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<SummedUpViewModel>> GetApprovedRelatedSongsSummedUpStatsAsync()
        {
            try
            {
                return new RServiceResult<SummedUpViewModel>
                    (
                    new SummedUpViewModel()
                    {
                        Days = await _context.DivanPoemMusicTracks
                        .Where(f => f.Approved
                        )
                        .GroupBy(f => f.ApprovalDate.Date).CountAsync(),
                        TotalCount = await _context.DivanPoemMusicTracks

                        .Where(f => f.Approved
                        )
                        .CountAsync(),
                        UserIds = await _context.DivanPoemMusicTracks
                        .Where(f => f.Approved
                        )
                        .GroupBy(f => f.SuggestedById).CountAsync(),
                    }
                    );

            }
            catch (Exception e)
            {
                return new RServiceResult<SummedUpViewModel>(null, e.ToString());
            }
        }

        /// <summary>
        /// approved quoted poems daily
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>> GetApprovedQuotedPoemsGroupedByDateAsync(PagingParameterModel paging, Guid? userId)
        {
            try
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>(
                    await QueryablePaginator<GroupedByDateViewModel>.Paginate(
                   _context.DivanQuotedPoems
                        .Where(c =>
                        c.Published && c.SuggestionDate != null
                        &&
                        (userId == null || c.SuggestedById == userId)
                        )
                        .GroupBy(a => a.SuggestionDate!.Value.Date)
                        .Select(a => new GroupedByDateViewModel()
                        {
                            Date = a.Key.Date.ToString(),
                            Number = a.Count(),
                        }).OrderByDescending(s => s.Date)
                   , paging));
            }
            catch (Exception e)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>((null, null), e.ToString());
            }
        }

        /// <summary>
        /// approved quoted poems grouped by user
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="day"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>> GetApprovedQuotedPoemsGroupedByUserAsync(PagingParameterModel paging, DateTime? day, Guid? userId)
        {
            try
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>(
                    await QueryablePaginator<GroupedByUserViewModel>.Paginate(
                        _context.DivanQuotedPoems
                        .Join
                        (
                            _context.Users,
                            correction => correction.SuggestedById,
                            user => user.Id,
                            (correction, user) => new
                            {
                                correction.SuggestionDate,
                                UserId = user.Id,
                                UserName = user.NickName,
                                correction.Published,
                            }
                        )
                        .Where(f =>
                         f.Published && f.SuggestionDate != null
                        &&
                        (day == null || f.SuggestionDate!.Value.Date == day) && (userId == null || f.UserId == userId))
                        .GroupBy(a => new { a.UserId, a.UserName }).Select(a => new GroupedByUserViewModel()
                        {
                            UserId = a.Key.UserId,
                            UserName = a.Key.UserName,
                            Number = a.Count(),
                        }).OrderByDescending(s => s.Number)
                        , paging));

            }
            catch (Exception e)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>((null, null), e.ToString());
            }
        }

        /// <summary>
        /// summed up stats of approved quoted poems
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<SummedUpViewModel>> GetApprovedQuotedPoemsSummedUpStatsAsync()
        {
            try
            {
                return new RServiceResult<SummedUpViewModel>
                    (
                    new SummedUpViewModel()
                    {
                        Days = await _context.DivanQuotedPoems
                        .Where(f => f.Published && f.SuggestionDate != null
                        )
                        .GroupBy(f => f.SuggestionDate!.Value.Date).CountAsync(),
                        TotalCount = await _context.DivanQuotedPoems

                        .Where(f => f.Published
                        )
                        .CountAsync(),
                        UserIds = await _context.DivanQuotedPoems
                        .Where(f => f.Published
                        )
                        .GroupBy(f => f.SuggestedById).CountAsync(),
                    }
                    );

            }
            catch (Exception e)
            {
                return new RServiceResult<SummedUpViewModel>(null, e.ToString());
            }
        }

        /// <summary>
        /// approved comments daily
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>> GetApprovedCommentsGroupedByDateAsync(PagingParameterModel paging, Guid? userId)
        {
            try
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>(
                    await QueryablePaginator<GroupedByDateViewModel>.Paginate(
                   _context.DivanComments
                        .Where(c =>
                        c.Status == Models.Artifact.PublishStatus.Published
                        &&
                        (userId == null || c.UserId == userId)
                        )
                        .GroupBy(a => a.CommentDate.Date)
                        .Select(a => new GroupedByDateViewModel()
                        {
                            Date = a.Key.Date.ToString(),
                            Number = a.Count(),
                        }).OrderByDescending(s => s.Date)
                   , paging));
            }
            catch (Exception e)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>((null, null), e.ToString());
            }
        }

        /// <summary>
        /// approved comments grouped by user
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="day"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>> GetApprovedCommentsGroupedByUserAsync(PagingParameterModel paging, DateTime? day, Guid? userId)
        {
            try
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>(
                    await QueryablePaginator<GroupedByUserViewModel>.Paginate(
                        _context.DivanComments
                        .Join
                        (
                            _context.Users,
                            correction => correction.UserId,
                            user => user.Id,
                            (correction, user) => new
                            {
                                correction.CommentDate,
                                UserId = user.Id,
                                UserName = user.NickName,
                                correction.Status,
                            }
                        )
                        .Where(f =>
                         f.Status == Models.Artifact.PublishStatus.Published 
                        &&
                        (day == null || f.CommentDate.Date == day) && (userId == null || f.UserId == userId))
                        .GroupBy(a => new { a.UserId, a.UserName }).Select(a => new GroupedByUserViewModel()
                        {
                            UserId = a.Key.UserId,
                            UserName = a.Key.UserName,
                            Number = a.Count(),
                        }).OrderByDescending(s => s.Number)
                        , paging));

            }
            catch (Exception e)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>((null, null), e.ToString());
            }
        }

        /// <summary>
        /// summed up stats of approved comments
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<SummedUpViewModel>> GetApprovedCommentsSummedUpStatsAsync()
        {
            try
            {
                return new RServiceResult<SummedUpViewModel>
                    (
                    new SummedUpViewModel()
                    {
                        Days = await _context.DivanComments
                        .Where(f => f.Status == Models.Artifact.PublishStatus.Published
                        )
                        .GroupBy(f => f.CommentDate.Date).CountAsync(),
                        TotalCount = await _context.DivanComments

                        .Where(f => f.Status == Models.Artifact.PublishStatus.Published
                        )
                        .CountAsync(),
                        UserIds = await _context.DivanComments
                        .Where(f => f.Status == Models.Artifact.PublishStatus.Published
                        )
                        .GroupBy(f => f.UserId).CountAsync(),
                    }
                    );

            }
            catch (Exception e)
            {
                return new RServiceResult<SummedUpViewModel>(null, e.ToString());
            }
        }

        /// <summary>
        /// approved recitations daily
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>> GetApprovedRecitationsGroupedByDateAsync(PagingParameterModel paging, Guid? userId)
        {
            try
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>(
                    await QueryablePaginator<GroupedByDateViewModel>.Paginate(
                   _context.Recitations
                        .Where(c =>
                        c.ReviewStatus == Models.DivanAudio.AudioReviewStatus.Approved
                        &&
                        (userId == null || c.OwnerId == userId)
                        )
                        .GroupBy(a => a.UploadDate.Date)
                        .Select(a => new GroupedByDateViewModel()
                        {
                            Date = a.Key.Date.ToString(),
                            Number = a.Count(),
                        }).OrderByDescending(s => s.Date)
                   , paging));
            }
            catch (Exception e)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>((null, null), e.ToString());
            }
        }

        /// <summary>
        /// approved recitations grouped by user
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="day"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>> GetApprovedRecitationsGroupedByUserAsync(PagingParameterModel paging, DateTime? day, Guid? userId)
        {
            try
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>(
                    await QueryablePaginator<GroupedByUserViewModel>.Paginate(
                        _context.Recitations
                        .Join
                        (
                            _context.Users,
                            correction => correction.OwnerId,
                            user => user.Id,
                            (correction, user) => new
                            {
                                correction.UploadDate,
                                UserId = user.Id,
                                UserName = user.NickName,
                                correction.ReviewStatus,
                            }
                        )
                        .Where(f =>
                         f.ReviewStatus == Models.DivanAudio.AudioReviewStatus.Approved
                        &&
                        (day == null || f.UploadDate.Date == day) && (userId == null || f.UserId == userId))
                        .GroupBy(a => new { a.UserId, a.UserName }).Select(a => new GroupedByUserViewModel()
                        {
                            UserId = a.Key.UserId,
                            UserName = a.Key.UserName,
                            Number = a.Count(),
                        }).OrderByDescending(s => s.Number)
                        , paging));

            }
            catch (Exception e)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>((null, null), e.ToString());
            }
        }

        /// <summary>
        /// summed up stats of approved recitations
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<SummedUpViewModel>> GetApprovedRecitationsSummedUpStatsAsync()
        {
            try
            {
                return new RServiceResult<SummedUpViewModel>
                    (
                    new SummedUpViewModel()
                    {
                        Days = await _context.Recitations
                        .Where(f => f.ReviewStatus == Models.DivanAudio.AudioReviewStatus.Approved
                        )
                        .GroupBy(f => f.UploadDate.Date).CountAsync(),
                        TotalCount = await _context.Recitations

                        .Where(f => f.ReviewStatus == Models.DivanAudio.AudioReviewStatus.Approved
                        )
                        .CountAsync(),
                        UserIds = await _context.Recitations
                        .Where(f => f.ReviewStatus == Models.DivanAudio.AudioReviewStatus.Approved
                        )
                        .GroupBy(f => f.OwnerId).CountAsync(),
                    }
                    );

            }
            catch (Exception e)
            {
                return new RServiceResult<SummedUpViewModel>(null, e.ToString());
            }
        }

        /// <summary>
        /// approved museum links daily
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>> GetApprovedMuseumLinksGroupedByDateAsync(PagingParameterModel paging, Guid? userId)
        {
            try
            {
                string systemEmail = $"{Configuration.GetSection("Divan")["SystemEmail"]}";
                var systemUserId = (Guid)(await _appUserService.FindUserByEmail(systemEmail)).Result.Id;

                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>(
                    await QueryablePaginator<GroupedByDateViewModel>.Paginate(
                   _context.DivanLinks
                        .Where(c =>
                        c.ReviewResult == Models.DivanIntegration.ReviewResult.Approved && c.SuggestedById != systemUserId
                        &&
                        (userId == null || c.SuggestedById == userId)
                        )
                        .GroupBy(a => a.SuggestionDate.Date)
                        .Select(a => new GroupedByDateViewModel()
                        {
                            Date = a.Key.Date.ToString(),
                            Number = a.Count(),
                        }).OrderByDescending(s => s.Date)
                   , paging));
            }
            catch (Exception e)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>((null, null), e.ToString());
            }
        }

        /// <summary>
        /// approved museum links grouped by user
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="day"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>> GetApprovedMuseumLinksGroupedByUserAsync(PagingParameterModel paging, DateTime? day, Guid? userId)
        {
            try
            {
                string systemEmail = $"{Configuration.GetSection("Divan")["SystemEmail"]}";
                var systemUserId = (Guid)(await _appUserService.FindUserByEmail(systemEmail)).Result.Id;

                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>(
                    await QueryablePaginator<GroupedByUserViewModel>.Paginate(
                        _context.DivanLinks
                        .Join
                        (
                            _context.Users,
                            correction => correction.SuggestedById,
                            user => user.Id,
                            (correction, user) => new
                            {
                                correction.SuggestionDate,
                                UserId = user.Id,
                                UserName = user.NickName,
                                correction.ReviewResult,
                            }
                        )
                        .Where(f =>
                         f.ReviewResult == Models.DivanIntegration.ReviewResult.Approved && f.UserId != systemUserId
                        &&
                        (day == null || f.SuggestionDate.Date == day) && (userId == null || f.UserId == userId))
                        .GroupBy(a => new { a.UserId, a.UserName }).Select(a => new GroupedByUserViewModel()
                        {
                            UserId = a.Key.UserId,
                            UserName = a.Key.UserName,
                            Number = a.Count(),
                        }).OrderByDescending(s => s.Number)
                        , paging));

            }
            catch (Exception e)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>((null, null), e.ToString());
            }
        }

        /// <summary>
        /// summed up stats of approved museum links
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<SummedUpViewModel>> GetApprovedMuseumLinksSummedUpStatsAsync()
        {
            try
            {
                string systemEmail = $"{Configuration.GetSection("Divan")["SystemEmail"]}";
                var systemUserId = (Guid)(await _appUserService.FindUserByEmail(systemEmail)).Result.Id;
                return new RServiceResult<SummedUpViewModel>
                    (
                    new SummedUpViewModel()
                    {
                        Days = await _context.DivanLinks
                        .Where(f => f.ReviewResult == Models.DivanIntegration.ReviewResult.Approved && f.SuggestedById != systemUserId
                        )
                        .GroupBy(f => f.SuggestionDate.Date).CountAsync(),
                        TotalCount = await _context.DivanLinks

                        .Where(f => f.ReviewResult == Models.DivanIntegration.ReviewResult.Approved && f.SuggestedById != systemUserId
                        )
                        .CountAsync(),
                        UserIds = await _context.DivanLinks
                        .Where(f => f.ReviewResult == Models.DivanIntegration.ReviewResult.Approved && f.SuggestedById != systemUserId
                        )
                        .GroupBy(f => f.SuggestedById).CountAsync(),
                    }
                    );

            }
            catch (Exception e)
            {
                return new RServiceResult<SummedUpViewModel>(null, e.ToString());
            }
        }

        /// <summary>
        /// approved pinterest links daily
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>> GetApprovedPinterestLinksGroupedByDateAsync(PagingParameterModel paging, Guid? userId)
        {
            try
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>(
                    await QueryablePaginator<GroupedByDateViewModel>.Paginate(
                   _context.PinterestLinks
                        .Where(c =>
                        c.HumanReviewed
                        &&
                        c.ReviewResult == Models.DivanIntegration.ReviewResult.Approved
                        &&
                        (userId == null || c.SuggestedById == userId)
                        )
                        .GroupBy(a => a.SuggestionDate.Date)
                        .Select(a => new GroupedByDateViewModel()
                        {
                            Date = a.Key.Date.ToString(),
                            Number = a.Count(),
                        }).OrderByDescending(s => s.Date)
                   , paging));
            }
            catch (Exception e)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>((null, null), e.ToString());
            }
        }

        /// <summary>
        /// approved pinterest links grouped by user
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="day"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>> GetApprovedPinterestLinksGroupedByUserAsync(PagingParameterModel paging, DateTime? day, Guid? userId)
        {
            try
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>(
                    await QueryablePaginator<GroupedByUserViewModel>.Paginate(
                        _context.PinterestLinks
                        .Join
                        (
                            _context.Users,
                            correction => correction.SuggestedById,
                            user => user.Id,
                            (correction, user) => new
                            {
                                correction.SuggestionDate,
                                UserId = user.Id,
                                UserName = user.NickName,
                                correction.ReviewResult,
                                correction.HumanReviewed
                            }
                        )
                        .Where(f =>
                         f.HumanReviewed
                         &&
                         f.ReviewResult == Models.DivanIntegration.ReviewResult.Approved
                        &&
                        (day == null || f.SuggestionDate.Date == day) && (userId == null || f.UserId == userId))
                        .GroupBy(a => new { a.UserId, a.UserName }).Select(a => new GroupedByUserViewModel()
                        {
                            UserId = a.Key.UserId,
                            UserName = a.Key.UserName,
                            Number = a.Count(),
                        }).OrderByDescending(s => s.Number)
                        , paging));

            }
            catch (Exception e)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>((null, null), e.ToString());
            }
        }

        /// <summary>
        /// summed up stats of approved pinterest links
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<SummedUpViewModel>> GetApprovedPinterestLinksSummedUpStatsAsync()
        {
            try
            {
                return new RServiceResult<SummedUpViewModel>
                    (
                    new SummedUpViewModel()
                    {
                        Days = await _context.PinterestLinks
                        .Where(f => f.HumanReviewed && f.ReviewResult == Models.DivanIntegration.ReviewResult.Approved
                        )
                        .GroupBy(f => f.SuggestionDate.Date).CountAsync(),
                        TotalCount = await _context.PinterestLinks

                        .Where(f => f.HumanReviewed && f.ReviewResult == Models.DivanIntegration.ReviewResult.Approved
                        )
                        .CountAsync(),
                        UserIds = await _context.PinterestLinks
                        .Where(f => f.HumanReviewed && f.ReviewResult == Models.DivanIntegration.ReviewResult.Approved
                        )
                        .GroupBy(f => f.SuggestedById).CountAsync(),
                    }
                    );

            }
            catch (Exception e)
            {
                return new RServiceResult<SummedUpViewModel>(null, e.ToString());
            }
        }

        /// <summary>
        /// approved poet spec lines daily
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>> GetApprovedPoetSpecLinesGroupedByDateAsync(PagingParameterModel paging, Guid? userId)
        {
            try
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>(
                    await QueryablePaginator<GroupedByDateViewModel>.Paginate(
                   _context.DivanPoetSuggestedSpecLines
                        .Where(c =>
                        c.Published
                        &&
                        (userId == null || c.SuggestedById == userId)
                        )
                        .GroupBy(a => a.PublicationDate.Date)
                        .Select(a => new GroupedByDateViewModel()
                        {
                            Date = a.Key.Date.ToString(),
                            Number = a.Count(),
                        }).OrderByDescending(s => s.Date)
                   , paging));
            }
            catch (Exception e)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>((null, null), e.ToString());
            }
        }

        /// <summary>
        /// approved poet spec lines grouped by user
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="day"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>> GetApprovedPoetSpecLinesGroupedByUserAsync(PagingParameterModel paging, DateTime? day, Guid? userId)
        {
            try
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>(
                    await QueryablePaginator<GroupedByUserViewModel>.Paginate(
                        _context.DivanPoetSuggestedSpecLines
                        .Join
                        (
                            _context.Users,
                            correction => correction.SuggestedById,
                            user => user.Id,
                            (correction, user) => new
                            {
                                correction.PublicationDate,
                                UserId = user.Id,
                                UserName = user.NickName,
                                correction.Published
                            }
                        )
                        .Where(f =>
                         f.Published
                        &&
                        (day == null || f.PublicationDate.Date == day) && (userId == null || f.UserId == userId))
                        .GroupBy(a => new { a.UserId, a.UserName }).Select(a => new GroupedByUserViewModel()
                        {
                            UserId = a.Key.UserId,
                            UserName = a.Key.UserName,
                            Number = a.Count(),
                        }).OrderByDescending(s => s.Number)
                        , paging));

            }
            catch (Exception e)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>((null, null), e.ToString());
            }
        }

        /// <summary>
        /// summed up stats of approved poet spec lines
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<SummedUpViewModel>> GetApprovedPoetSpecLinesSummedUpStatsAsync()
        {
            try
            {
                return new RServiceResult<SummedUpViewModel>
                    (
                    new SummedUpViewModel()
                    {
                        Days = await _context.DivanPoetSuggestedSpecLines
                        .Where(f => f.Published
                        )
                        .GroupBy(f => f.PublicationDate.Date).CountAsync(),
                        TotalCount = await _context.DivanPoetSuggestedSpecLines

                        .Where(f => f.Published
                        )
                        .CountAsync(),
                        UserIds = await _context.DivanPoetSuggestedSpecLines
                        .Where(f => f.Published
                        )
                        .GroupBy(f => f.SuggestedById).CountAsync(),
                    }
                    );

            }
            catch (Exception e)
            {
                return new RServiceResult<SummedUpViewModel>(null, e.ToString());
            }
        }

        /// <summary>
        /// approved poet photos daily
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>> GetApprovedPoetPicturesGroupedByDateAsync(PagingParameterModel paging, Guid? userId)
        {
            try
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>(
                    await QueryablePaginator<GroupedByDateViewModel>.Paginate(
                   _context.DivanPoetSuggestedPictures
                        .Where(c =>
                        c.Published
                        &&
                        (userId == null || c.SuggestedById == userId)
                        )
                        .GroupBy(a => a.PublicationDate.Date)
                        .Select(a => new GroupedByDateViewModel()
                        {
                            Date = a.Key.Date.ToString(),
                            Number = a.Count(),
                        }).OrderByDescending(s => s.Date)
                   , paging));
            }
            catch (Exception e)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>((null, null), e.ToString());
            }
        }

        /// <summary>
        /// approved poet photos grouped by user
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="day"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>> GetApprovedPoetPicturesGroupedByUserAsync(PagingParameterModel paging, DateTime? day, Guid? userId)
        {
            try
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>(
                    await QueryablePaginator<GroupedByUserViewModel>.Paginate(
                        _context.DivanPoetSuggestedPictures
                        .Join
                        (
                            _context.Users,
                            correction => correction.SuggestedById,
                            user => user.Id,
                            (correction, user) => new
                            {
                                correction.PublicationDate,
                                UserId = user.Id,
                                UserName = user.NickName,
                                correction.Published
                            }
                        )
                        .Where(f =>
                         f.Published
                        &&
                        (day == null || f.PublicationDate.Date == day) && (userId == null || f.UserId == userId))
                        .GroupBy(a => new { a.UserId, a.UserName }).Select(a => new GroupedByUserViewModel()
                        {
                            UserId = a.Key.UserId,
                            UserName = a.Key.UserName,
                            Number = a.Count(),
                        }).OrderByDescending(s => s.Number)
                        , paging));

            }
            catch (Exception e)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>((null, null), e.ToString());
            }
        }

        /// <summary>
        /// summed up stats of approved poet photos
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<SummedUpViewModel>> GetApprovedPoetPicturesSummedUpStatsAsync()
        {
            try
            {
                return new RServiceResult<SummedUpViewModel>
                    (
                    new SummedUpViewModel()
                    {
                        Days = await _context.DivanPoetSuggestedPictures
                        .Where(f => f.Published
                        )
                        .GroupBy(f => f.PublicationDate.Date).CountAsync(),
                        TotalCount = await _context.DivanPoetSuggestedPictures

                        .Where(f => f.Published
                        )
                        .CountAsync(),
                        UserIds = await _context.DivanPoetSuggestedPictures
                        .Where(f => f.Published
                        )
                        .GroupBy(f => f.SuggestedById).CountAsync(),
                    }
                    );

            }
            catch (Exception e)
            {
                return new RServiceResult<SummedUpViewModel>(null, e.ToString());
            }
        }

        /// <summary>
        /// approved user notes
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>> GetApprovedUserNotesGroupedByDateAsync(PagingParameterModel paging, Guid? userId)
        {
            try
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>(
                    await QueryablePaginator<GroupedByDateViewModel>.Paginate(
                   _context.UserNotes
                        .Where(c =>
                        c.Status == Models.Artifact.PublishStatus.Published && c.NoteType == Models.Note.RNoteType.Public
                        &&
                        (userId == null || c.RAppUserId == userId)
                        )
                        .GroupBy(a => a.DateTime.Date)
                        .Select(a => new GroupedByDateViewModel()
                        {
                            Date = a.Key.Date.ToString(),
                            Number = a.Count(),
                        }).OrderByDescending(s => s.Date)
                   , paging));
            }
            catch (Exception e)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByDateViewModel[] Tracks)>((null, null), e.ToString());
            }
        }

        /// <summary>
        /// approved user notes grouped by user
        /// </summary>
        /// <param name="paging"></param>
        /// <param name="day"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>> GetApprovedUserNotesGroupedByUserAsync(PagingParameterModel paging, DateTime? day, Guid? userId)
        {
            try
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>(
                    await QueryablePaginator<GroupedByUserViewModel>.Paginate(
                        _context.UserNotes
                        .Join
                        (
                            _context.Users,
                            correction => correction.RAppUserId,
                            user => user.Id,
                            (correction, user) => new
                            {
                                correction.DateTime,
                                UserId = user.Id,
                                UserName = user.NickName,
                                correction.Status,
                                correction.NoteType
                            }
                        )
                        .Where(f =>
                         f.Status == Models.Artifact.PublishStatus.Published && f.NoteType == Models.Note.RNoteType.Public
                        &&
                        (day == null || f.DateTime.Date == day) && (userId == null || f.UserId == userId))
                        .GroupBy(a => new { a.UserId, a.UserName }).Select(a => new GroupedByUserViewModel()
                        {
                            UserId = a.Key.UserId,
                            UserName = a.Key.UserName,
                            Number = a.Count(),
                        }).OrderByDescending(s => s.Number)
                        , paging));

            }
            catch (Exception e)
            {
                return new RServiceResult<(PaginationMetadata PagingMeta, GroupedByUserViewModel[] Tracks)>((null, null), e.ToString());
            }
        }

        /// <summary>
        /// summed up stats of approved user notes
        /// </summary>
        /// <returns></returns>
        public async Task<RServiceResult<SummedUpViewModel>> GetApprovedUserNotesSummedUpStatsAsync()
        {
            try
            {
                return new RServiceResult<SummedUpViewModel>
                    (
                    new SummedUpViewModel()
                    {
                        Days = await _context.UserNotes
                        .Where(f => f.Status == Models.Artifact.PublishStatus.Published && f.NoteType == Models.Note.RNoteType.Public
                        )
                        .GroupBy(f => f.DateTime.Date).CountAsync(),
                        TotalCount = await _context.UserNotes

                        .Where(f => f.Status == Models.Artifact.PublishStatus.Published && f.NoteType == Models.Note.RNoteType.Public
                        )
                        .CountAsync(),
                        UserIds = await _context.UserNotes
                        .Where(f => f.Status == Models.Artifact.PublishStatus.Published && f.NoteType == Models.Note.RNoteType.Public
                        )
                        .GroupBy(f => f.RAppUserId).CountAsync(),
                    }
                    );

            }
            catch (Exception e)
            {
                return new RServiceResult<SummedUpViewModel>(null, e.ToString());
            }
        }




        /// <summary>
        /// Database Context
        /// </summary>
        protected readonly RMuseumDbContext _context;

        /// <summary>
        /// Configuration
        /// </summary>
        protected IConfiguration Configuration { get; }

        /// <summary>
        /// IAppUserService instance
        /// </summary>
        protected IAppUserService _appUserService;




        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="context"></param>
        /// <param name="configuration"></param>
        /// <param name="appUserService"></param>
        public ContributionStatsService(RMuseumDbContext context, IConfiguration configuration, IAppUserService appUserService)
        {
            _context = context;
            Configuration = configuration;
            _appUserService = appUserService;
        }
    }
}
