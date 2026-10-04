using Microsoft.AspNetCore.Mvc.RazorPages;
using RMuseum.Models.Divan;
using RMuseum.Models.Divan.ViewModels;
using System.Collections.Generic;

namespace DivanRazor.Pages
{
    public class _BNumPartialModel : PageModel
    {
        /// <summary>
        /// is logged on
        /// </summary>
        public bool LoggedIn { get; set; }

        /// <summary>
        /// poem id
        /// </summary>
        public int PoemId { get; set; }

        /// <summary>
        /// couplet index
        /// </summary>
        public int CoupletIndex { get; set; }

        /// <summary>
        /// is bookmarked
        /// </summary>
        public bool IsBookmarked { get; set; }

        /// <summary>
        /// bookmaring text
        /// </summary>
        public string BookmarkingText
        {
            get
            {
                return IsBookmarked ? "حذف نشان" : "نشان کردن";
            }
        }

        /// <summary>
        /// bookmarking icon
        /// </summary>
        public string BookmarkingIcon
        {
            get
            {
                return IsBookmarked ? "star" : "star_border";
            }
        }

        /// <summary>
        /// numbers
        /// </summary>
        public List<DivanCoupletNumberViewModel> Numbers { get; set; }

        /// <summary>
        /// comments
        /// </summary>
        public List<DivanCommentSummaryViewModel> Comments { get; set; }

        /// <summary>
        /// sections
        /// </summary>
        public List<DivanPoemSection> Sections { get; set; }


        /// <summary>
        /// a subset of sections
        /// </summary>
        public List<DivanPoemSection> SectionsWithMetreAndRhymes { get; set; }

        /// <summary>
        /// verses
        /// </summary>
        public List<DivanVerseViewModel> Verses { get; set; }

        public _CommentPartialModel GetCommentModel(DivanCommentSummaryViewModel comment)
        {
            return new _CommentPartialModel()
            {
                Comment = comment,
                Error = "",
                InReplyTo = null,
                LoggedIn = LoggedIn,
                DivSuffix = $"-{comment.CoupletIndex}",
                PoemId = PoemId,
                Bookmarked = comment.IsBookmarked,
            };
        }



    }
}
