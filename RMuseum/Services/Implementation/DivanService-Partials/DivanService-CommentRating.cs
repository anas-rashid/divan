using Microsoft.EntityFrameworkCore;
using RMuseum.Models.Divan;
using RMuseum.Models.Divan.ViewModels;
using RMuseum.Utils;
using RSecurityBackend.Models.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RMuseum.Services.Implementation
{
    /// <summary>
    /// IDivanService implementation
    /// </summary>
    public partial class DivanService : IDivanService
    {
        /// <summary>
        /// like / dislike / clear rating for a comment
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="commentId"></param>
        /// <param name="value">+1: like, -1: dislike, 0: remove previous rating</param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanCommentRatingResultViewModel>> RateCommentAsync(Guid userId, int commentId, short value)
        {
            if (value != 1 && value != -1 && value != 0)
            {
                return new RServiceResult<DivanCommentRatingResultViewModel>(null, "value != 1 && value != -1 && value != 0");
            }
            try
            {
                var comment = await _context.DivanComments.Where(c => c.Id == commentId).SingleOrDefaultAsync();
                if (comment == null)
                {
                    return new RServiceResult<DivanCommentRatingResultViewModel>(null, "comment == null");
                }

                if (comment.UserId != null && comment.UserId == userId)
                {
                    return new RServiceResult<DivanCommentRatingResultViewModel>(null, "امکان رأی دادن به حاشیهٔ خودتان وجود ندارد.");
                }

                var oldRating = await _context.DivanCommentReactions.Where(c => c.DivanCommentId == commentId && c.UserId == userId).SingleOrDefaultAsync();
                if (value == 0 && oldRating == null)
                {
                    return await _CurrentCommentRatingResultAsync(comment, 0);
                }
                if (oldRating != null)
                {
                    if (oldRating.Value == value)
                    {
                        return await _CurrentCommentRatingResultAsync(comment, oldRating.Value);
                    }
                    if (value == 0)
                    {
                        _context.Remove(oldRating);
                    }
                    else
                    {
                        oldRating.Value = value;
                        oldRating.ReactionDate = DateTime.Now;
                        _context.Update(oldRating);
                    }
                    await _context.SaveChangesAsync();

                    return await ReCalculateCommentSortKeyAsync(comment, value);
                }
                else
                {
                    var rating = new DivanCommentReaction()
                    {
                        DivanCommentId = commentId,
                        PoemId = comment.PoemId,
                        UserId = userId,
                        Value = value,
                        ReactionDate = DateTime.Now,
                    };
                    _context.Add(rating);
                    await _context.SaveChangesAsync();
                    return await ReCalculateCommentSortKeyAsync(comment, value);
                }

            }
            catch (Exception exp)
            {
                return new RServiceResult<DivanCommentRatingResultViewModel>(null, exp.ToString());
            }
        }

        /// <summary>
        /// get the requesting user's own rating values for all comments of a poem
        /// (meant to be merged client-side into an already fetched, anonymously cacheable comment list)
        /// </summary>
        /// <param name="poemId"></param>
        /// <param name="userId"></param>
        /// <returns></returns>
        public async Task<RServiceResult<DivanCommentUserRatingViewModel[]>> GetUserCommentRatings(int poemId, Guid userId)
        {
            try
            {
                var ratings =
                    await _context.DivanCommentReactions.AsNoTracking()
                    .Where(r => r.PoemId == poemId && r.UserId == userId)
                    .Select(r => new DivanCommentUserRatingViewModel()
                    {
                        CommentId = r.DivanCommentId,
                        Value = r.Value,
                    })
                    .ToArrayAsync();
                return new RServiceResult<DivanCommentUserRatingViewModel[]>(ratings);
            }
            catch (Exception exp)
            {
                return new RServiceResult<DivanCommentUserRatingViewModel[]>(null, exp.ToString());
            }
        }

        private Task<RServiceResult<DivanCommentRatingResultViewModel>> _CurrentCommentRatingResultAsync(DivanComment comment, short currentUserRatingValue)
        {
            return Task.FromResult(new RServiceResult<DivanCommentRatingResultViewModel>(
                new DivanCommentRatingResultViewModel()
                {
                    CommentId = comment.Id,
                    LikeCount = comment.LikeCount,
                    DislikeCount = comment.DislikeCount,
                    CurrentUserRatingValue = currentUserRatingValue,
                }));
        }

        private async Task<RServiceResult<DivanCommentRatingResultViewModel>> ReCalculateCommentSortKeyAsync(DivanComment comment, short currentUserRatingValue)
        {
            try
            {
                var likes = await _context.DivanCommentReactions.AsNoTracking().CountAsync(r => r.DivanCommentId == comment.Id && r.Value == 1);
                var dislikes = await _context.DivanCommentReactions.AsNoTracking().CountAsync(r => r.DivanCommentId == comment.Id && r.Value == -1);
                comment.LikeCount = likes;
                comment.DislikeCount = dislikes;
                comment.SortKey = DivanCommentRankingScoreCalculator.ComputeRankingScore(likes, dislikes);
                _context.Update(comment);
                await _context.SaveChangesAsync();
                await CacheCleanForComment(comment.Id);
                return new RServiceResult<DivanCommentRatingResultViewModel>(
                    new DivanCommentRatingResultViewModel()
                    {
                        CommentId = comment.Id,
                        LikeCount = comment.LikeCount,
                        DislikeCount = comment.DislikeCount,
                        CurrentUserRatingValue = currentUserRatingValue,
                    });
            }
            catch (Exception exp)
            {
                return new RServiceResult<DivanCommentRatingResultViewModel>(null, exp.ToString());
            }
        }
    }
}
