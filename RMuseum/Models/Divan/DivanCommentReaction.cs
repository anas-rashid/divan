using RSecurityBackend.Models.Auth.Db;
using System;

namespace RMuseum.Models.Divan
{
    public class DivanCommentReaction
    {
        public int Id { get; set; }

        public int DivanCommentId { get; set; }
        public virtual DivanComment DivanComment { get; set; }

        public int PoemId { get; set; }
        public Guid UserId { get; set; }
        public virtual RAppUser User { get; set; }

        /// <summary>
        /// +1 = Like
        /// -1 = Dislike
        /// </summary>
        public short Value { get; set; }

        public DateTime ReactionDate { get; set; }
    }
}
