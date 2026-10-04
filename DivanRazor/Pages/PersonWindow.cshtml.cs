using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using RMuseum.Models.Divan;
using RMuseum.Models.Divan.ViewModels;

namespace DivanRazor.Pages
{
    /// <summary>
    /// one person's bio, kinship/affiliation ties and tagged poems, rendered as a bare fragment
    /// (Layout = null - no header, footer or site chrome) and opened as an inline modal via
    /// PersonWindow.open(id) in personwindow.js, from wherever a person is referenced: a poem's
    /// tagged-persons list, the category/whole-site graph, the family-tree chart, the
    /// suggestion/moderation forms. This used to be the standalone page Person.cshtml; it has no
    /// entry point of its own any more; every "view this person" link on the site now calls
    /// PersonWindow.open(id) instead of navigating here.
    /// </summary>
    public class PersonWindowModel : LoginPartialEnabledPageModel
    {
        public PersonWindowModel(HttpClient httpClient, IConfiguration configuration) : base(httpClient, configuration)
        {
        }

        public string LastError { get; set; }

        public DivanRelatedPerson Person { get; set; }

        /// <summary>
        /// one row per kinship edge, already resolved into display-ready text so the .cshtml
        /// doesn't need to know the directional meaning of PersonRelationType
        /// </summary>
        public List<PersonRelationDisplayRow> RelationRows { get; set; } = new List<PersonRelationDisplayRow>();

        /// <summary>
        /// one row per non-family tie, same purpose as RelationRows
        /// </summary>
        public List<PersonAffiliationDisplayRow> AffiliationRows { get; set; } = new List<PersonAffiliationDisplayRow>();

        public PoemGeoDateTag[] Poems { get; set; }

        public class PersonRelationDisplayRow
        {
            /// <summary>
            /// the underlying DivanPersonRelation row's own id - used to link to
            /// /SuggestPersonRelationEdit/{RelationId} for suggesting a change/removal of this edge
            /// </summary>
            public int RelationId { get; set; }

            /// <summary>
            /// the other person's role relative to the subject (e.g. "اولاد", "والد/والدہ", "جدّ")
            /// </summary>
            public string Label { get; set; }
            public int OtherPersonId { get; set; }
            public string OtherPersonName { get; set; }
            public string Note { get; set; }
        }

        public class PersonAffiliationDisplayRow
        {
            /// <summary>
            /// the underlying DivanPersonAffiliation row's own id - used to link to
            /// /User/SuggestPersonRelationEdit?affiliationId={AffiliationId} for suggesting a
            /// change/removal of this edge, same role RelationId plays on PersonRelationDisplayRow
            /// </summary>
            public int AffiliationId { get; set; }

            /// <summary>
            /// full Persian sentence with a "{0}" placeholder for where the other person's linked
            /// name goes (kept as a placeholder, rather than a pre-built string, so the .cshtml can
            /// still render the name as a link)
            /// </summary>
            public string SentenceBeforeOtherName { get; set; }
            public string SentenceAfterOtherName { get; set; }
            public int OtherPersonId { get; set; }
            public string OtherPersonName { get; set; }
            public string Note { get; set; }
        }

        private static string RelationLabel(DivanPersonRelationInfo r)
        {
            switch (r.RelationType)
            {
                case PersonRelationType.Parent:
                    return r.SubjectIsPerson1 ? "اولاد" : "والد/والدہ";
                case PersonRelationType.Sibling:
                    return "بہن/بھائی";
                case PersonRelationType.Spouse:
                    return "شریکِ حیات";
                case PersonRelationType.Ancestor:
                    var word = r.SubjectIsPerson1 ? "نسل" : "جدّ";
                    return r.DegreeHint != null ? $"{word} (فاصلہ {r.DegreeHint} پشت)" : word;
                default:
                    return r.RelationType.ToString();
            }
        }

        // Persian word for the subordinate/serving-role side of each directional affiliation type -
        // used to build a full sentence below, since (unlike family relations) there's no natural
        // short label for "the person this one served"
        private static readonly System.Collections.Generic.Dictionary<PersonAffiliationType, string> _affiliationRoleWords =
            new System.Collections.Generic.Dictionary<PersonAffiliationType, string>()
            {
                { PersonAffiliationType.Minister, "وزیرِ" },
                { PersonAffiliationType.Advisor, "مشیرِ" },
                { PersonAffiliationType.Courtier, "درباریِ" },
                { PersonAffiliationType.Patron, "سرپرستِ" },
                { PersonAffiliationType.Ally, "حلیفِ" },
                { PersonAffiliationType.Rival, "رقیبِ" },
                { PersonAffiliationType.Servant, "خادمِ" },
                { PersonAffiliationType.Companion, "رفیقِ" },
                { PersonAffiliationType.Successor, "جانشینِ" },
                { PersonAffiliationType.Panegyrized, "مداحِ" },
                { PersonAffiliationType.Satirized, "ہجو گوئے" },
                { PersonAffiliationType.MilitaryCommander, "سردارِ" },
                { PersonAffiliationType.Champion, "پہلوانِ" },
                { PersonAffiliationType.Contemporary, "ہم عصرِ" },
                { PersonAffiliationType.Killer, "قاتلِ" },
                { PersonAffiliationType.Other, "سے نسبت رکھتا تھا (نوٹ دیکھیں):" },
            };

        private static bool IsSymmetricAffiliation(PersonAffiliationType t) =>
            t == PersonAffiliationType.Ally || t == PersonAffiliationType.Rival ||
            t == PersonAffiliationType.Companion || t == PersonAffiliationType.Contemporary ||
            t == PersonAffiliationType.Other;

        public async Task<IActionResult> OnGetAsync(int id)
        {
            InitializeCommonPageState();

            var personResponse = await _httpClient.GetAsync($"{APIRoot.Url}/api/people/{id}");
            if (!personResponse.IsSuccessStatusCode)
            {
                LastError = await ReadErrorMessageAsync(personResponse);
                return Page();
            }
            Person = JsonConvert.DeserializeObject<DivanRelatedPerson>(await personResponse.Content.ReadAsStringAsync());
            if (Person == null)
            {
                LastError = "اس کوڈ کی کوئی شخصیت نہیں ملی.";
                return Page();
            }

            var relationsResponse = await _httpClient.GetAsync($"{APIRoot.Url}/api/people/{id}/relations");
            if (!relationsResponse.IsSuccessStatusCode)
            {
                LastError = await ReadErrorMessageAsync(relationsResponse);
                return Page();
            }
            var relationsData = JsonConvert.DeserializeObject<DivanPersonRelationsViewModel>(await relationsResponse.Content.ReadAsStringAsync());

            foreach (var r in relationsData.Relations ?? new List<DivanPersonRelationInfo>())
            {
                RelationRows.Add(new PersonRelationDisplayRow()
                {
                    RelationId = r.Id,
                    Label = RelationLabel(r),
                    OtherPersonId = r.OtherPersonId,
                    OtherPersonName = r.OtherPersonName,
                    Note = r.Note,
                });
            }

            foreach (var a in relationsData.Affiliations ?? new List<DivanPersonAffiliationInfo>())
            {
                string roleWord = _affiliationRoleWords.TryGetValue(a.AffiliationType, out var w) ? w : a.AffiliationType.ToString();
                bool subjectServes = a.SubjectIsPerson1 || IsSymmetricAffiliation(a.AffiliationType);
                AffiliationRows.Add(new PersonAffiliationDisplayRow()
                {
                    AffiliationId = a.Id,
                    SentenceBeforeOtherName = subjectServes ? $"یہ شخصیت {roleWord} " : "",
                    SentenceAfterOtherName = subjectServes ? " تھا" : $" {roleWord} یہ شخصیت تھی",
                    OtherPersonId = a.OtherPersonId,
                    OtherPersonName = a.OtherPersonName,
                    Note = a.Note,
                });
            }

            var poemsResponse = await _httpClient.GetAsync($"{APIRoot.Url}/api/people/{id}/poems");
            if (!poemsResponse.IsSuccessStatusCode)
            {
                LastError = await ReadErrorMessageAsync(poemsResponse);
                return Page();
            }
            Poems = JsonConvert.DeserializeObject<PoemGeoDateTag[]>(await poemsResponse.Content.ReadAsStringAsync());

            return Page();
        }
    }
}
