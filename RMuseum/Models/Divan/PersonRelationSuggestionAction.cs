namespace RMuseum.Models.Divan
{
    /// <summary>
    /// what a DivanPersonRelationEditSuggestion proposes doing to the kinship graph
    /// </summary>
    public enum PersonRelationSuggestionAction
    {
        /// <summary>
        /// create a brand new DivanPersonRelation between Person1Id and Person2Id
        /// </summary>
        Add = 0,

        /// <summary>
        /// change the RelationType/DegreeHint/Note of the existing relation ExistingRelationId points to
        /// </summary>
        Modify = 1,

        /// <summary>
        /// remove the existing relation ExistingRelationId points to outright
        /// </summary>
        Remove = 2,
    }
}
