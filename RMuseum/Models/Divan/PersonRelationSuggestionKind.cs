namespace RMuseum.Models.Divan
{
    /// <summary>
    /// which kind of edge a DivanPersonRelationEditSuggestion is about: a kinship tie
    /// (DivanPersonRelation, via SuggestedRelationType/ExistingRelationId) or a non-family
    /// affiliation tie (DivanPersonAffiliation, via SuggestedAffiliationType/ExistingAffiliationId).
    /// Defaults to Family (0), so every suggestion row created before this field existed keeps being
    /// read exactly as it was before - a plain kinship suggestion.
    /// </summary>
    public enum PersonRelationSuggestionKind
    {
        Family = 0,

        Affiliation = 1,
    }
}
