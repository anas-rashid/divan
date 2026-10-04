using DNTPersianUtils.Core;
using RMuseum.Models.Divan;
using RMuseum.Models.Divan.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace RMuseum.Services.Implementation
{
    /// <summary>
    /// Language Utilitis
    /// </summary>
    public static class LanguageUtils
    {
        /// <summary>
        /// Persian Alphabet
        /// </summary>
        public const string PersianAlphabet = "آابپتثجچحخدذرزژسشصضطظعغفقکگلمنوهیئؤء";

        /// <summary>
        /// English Alphabet
        /// </summary>
        public const string EnglishLowerCaseAlphabet = "abcdefghijklmnopqrstuvwxyz";

        /// <summary>
        /// Check to see if input Contains Only Specific Characters
        /// </summary>
        /// <param name="input"></param>
        /// <param name="specfics"></param>
        /// <param name="additional"></param>
        /// <returns></returns>
        public static string GetFirstNotMatchingCharacter(string input, string specfics, string additional = "")
        {
            string all = specfics + additional;
            foreach (char c in input)
                if (all.IndexOf(c) == -1)
                    return (c + "");
            return "";
        }

        /// <summary>
        /// make text searchable
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        public static string MakeTextSearchable(string text)
        {
            return text.Replace("‌", " ")//replace zwnj with space
                       .Replace("ّ", "")//tashdid
                       .Replace("َ", "")//a
                       .Replace("ِ", "")//e
                       .Replace("ُ", "")//o
                       .Replace("ً", "")//an
                       .Replace("ٍ", "")//en
                       .Replace("ٌ", "")//on
                       .Replace(".", "")//dot
                       .Replace("،", "")//virgool
                       .Replace("!", "")
                       .Replace("؟", "")
                       .Replace("ٔ", "")//ye
                       .Replace(":", "")
                       .Replace("ئ", "ی")
                       .Replace("؛", "")
                       .Replace(";", "")
                       .Replace("*", "")
                       .Replace(")", "")
                       .Replace("(", "")
                       .Replace("[", "")
                       .Replace("]", "")
                       .Replace("\"", "")
                       .Replace("'", "")
                       .Replace("«", "")
                       .Replace("»", "")
                       .Replace("ْ", "")//sokoon
                       // divan: Urdu normalisation (same rules apply to stored text and search terms)
                       .Replace("\u064A", "\u06CC")//arabic yeh -> farsi/urdu yeh
                       .Replace("\u0649", "\u06CC")//alef maksura -> yeh
                       .Replace("\u0643", "\u06A9")//arabic kaf -> keheh
                       .Replace("\u06C2", "\u06C1")//heh goal with hamza -> heh goal
                       .Replace("\u0647", "\u06C1")//arabic heh -> heh goal (do-chashmi heh U+06BE stays distinct)
                       .Replace("\u06D3", "\u06D2")//yeh barree with hamza -> yeh barree
                       .Replace("\u0623", "\u0627").Replace("\u0625", "\u0627")//alef with hamza -> alef
                       .Replace("\u06D4", "")//urdu full stop
                       .Replace("\u0670", "").Replace("\u0657", "").Replace("\u0658", "").Replace("\u0615", "")//urdu diacritics
                       .Replace("\u200D", "").Replace("\u200F", "").Replace("\u200E", "")//zwj, rtl/ltr marks
                       ;
        }

        /// <summary>
        /// divan: SQL LIKE patterns for a search term (replaces SQL Server full-text CONTAINS, which the
        /// Linux SQL Server image does not ship). "quoted phrase" -> one pattern; otherwise one pattern per
        /// word, all of which must match. Terms are normalised like stored PlainText.
        /// </summary>
        public static string[] SearchLikePatterns(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
                return Array.Empty<string>();
            term = term.Trim();
            bool phrase = term.Length > 1 && term.StartsWith("\"") && term.EndsWith("\"");
            string normalised = MakeTextSearchable(term.ApplyCorrectYeKe().Replace("\"", "").Replace("'", ""));
            string[] parts = phrase ? new[] { normalised.Trim() } : normalised.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Where(p => p.Length > 0)
                        .Select(p => "%" + p.Replace("[", "[[]").Replace("%", "[%]").Replace("_", "[_]") + "%")
                        .ToArray();
        }

        public static string CleanTextForTransileration(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.Replace("‌", " ")//replace zwnj with space
                       .Replace("ّ", "")//tashdid
                       .Replace("َ", "")//a
                       .Replace("ِ", "")//e
                       .Replace("ُ", "")//o
                       .Replace("ً", "")//an
                       .Replace("ٍ", "")//en
                       .Replace("ٌ", "")//on
                       .Replace("ٔ", "")//ye
                       .Replace("ْ", "")//sokoon
                       ;
        }


        public static string SameSoundLetters(string text)
        {
            return text.Replace("آ", "ا").Replace("ث", "س").Replace("ح", "ه").Replace("ذ", "ز").Replace("ص", "س").Replace("ض", "ز").Replace("ط", "ت").Replace("ظ", "ز").Replace("غ", "ق");
        }

        private static string PrepareTextForFindingRhyme(string text)
        {
            return MakeTextSearchable(text)
                    .Replace("لله", "للاه")
                    .Replace("آ", "ا")
                    .Replace("‍", "")
                    .Replace("‏", "")
                    .Replace("‌", "")
                    .Replace(" ", "")
                    .Trim();
        }


        /// <summary>
        /// find rhyme
        /// </summary>
        /// <param name="verses"></param>
        /// <param name="secondPhase"></param>
        /// <param name="bandCouplets"></param>
        /// <param name="tryWholeVerses"></param>
        /// <returns></returns>
        public static DivanRhymeAnalysisResult FindRhyme(List<DivanVerse> verses, bool secondPhase = false, bool bandCouplets = false, bool tryWholeVerses = false)
        {
            try
            {
                List<string> verseTextList = verses.Count == 2 || tryWholeVerses ? verses.Select(v => v.Text).ToList()
                                                           : bandCouplets ?
                                                           verses.Where(v => v.VersePosition == VersePosition.CenteredVerse2).Select(v => v.Text).ToList()
                                                           : verses.Where(v => v.VersePosition == VersePosition.Left).Select(v => v.Text).ToList();
                if (verseTextList.Count > 1)
                {
                    string rhyme = PrepareTextForFindingRhyme(verseTextList[0]);
                    if (string.IsNullOrEmpty(rhyme))
                    {
                        return new DivanRhymeAnalysisResult()
                        {
                            Rhyme = "",
                            FailVerse = verseTextList[0]
                        };
                    }
                    if (secondPhase)
                    {
                        if (rhyme.Length > 0 && rhyme[rhyme.Length - 1] == 'ی')
                            rhyme = rhyme.Remove(rhyme.Length - 1);
                    }

                    for (int j = 1; j < verseTextList.Count; j++)
                    {
                        string verseText = PrepareTextForFindingRhyme(verseTextList[j]);
                        if (secondPhase)
                        {
                            if (verseText.Length > 0 && verseText[verseText.Length - 1] == 'ی')
                            {
                                verseText = verseText.Remove(verseText.Length - 1);
                            }
                        }
                        string oldRhyme = rhyme;
                        rhyme = "";
                        int i = oldRhyme.Length - 1;
                        while (
                            (oldRhyme[i] == verseText[verseText.Length - oldRhyme.Length + i])
                            ||
                            (
                            (oldRhyme[i] == 'ذ')
                            &&
                            (verseText[verseText.Length - oldRhyme.Length + i] == 'د')
                            )
                            ||
                            (
                            (oldRhyme[i] == 'د')
                            &&
                            (verseText[verseText.Length - oldRhyme.Length + i] == 'ذ')
                            )
                            ||

                            (
                            (oldRhyme[i] == 'ی')
                            &&
                            (verseText[verseText.Length - oldRhyme.Length + i] == 'ا')
                            )
                            ||
                            (
                            (oldRhyme[i] == 'ا')
                            &&
                            (verseText[verseText.Length - oldRhyme.Length + i] == 'ی')
                            )

                            ||

                            (oldRhyme[i] == verseText[verseText.Length - oldRhyme.Length + i])
                            ||
                            (
                            (oldRhyme[i] == 'پ')
                            &&
                            (verseText[verseText.Length - oldRhyme.Length + i] == 'ب')
                            )
                            ||
                            (
                            (oldRhyme[i] == 'ب')
                            &&
                            (verseText[verseText.Length - oldRhyme.Length + i] == 'پ')
                            )

                            ||
                            (oldRhyme[i] == 'ة')
                            &&
                            (verseText[verseText.Length - oldRhyme.Length + i] == 'ت')

                            ||
                            (oldRhyme[i] == 'ت')
                            &&
                            (verseText[verseText.Length - oldRhyme.Length + i] == 'ة')



                            )
                        {
                            rhyme = oldRhyme[i] + rhyme;
                            i--;
                            if (i == -1)
                                break;
                        }
                        if (rhyme.Length == 0)
                        {
                            if (verses.Count == 2 && verseTextList.Count == 2)
                            {
                                rhyme = PrepareTextForFindingRhyme(verseTextList[1]);
                                if(rhyme.Length > 50)
                                {
                                    rhyme = "";
                                }
                                return new DivanRhymeAnalysisResult()
                                {
                                    Rhyme = rhyme, //rhyme.Length > 50 condition check removed to search using prosody meters
                                    FailVerse = "",
                                };
                            }
                            return new DivanRhymeAnalysisResult()
                            {
                                Rhyme = "",
                                FailVerse = verseText
                            };
                        }

                    }

                    if (!secondPhase)
                    {
                        if (string.IsNullOrEmpty(rhyme))
                        {
                            return FindRhyme(verses, true);
                        }
                    }

                    ////rhyme.Length > 50 condition check removed to search using prosody meters
                    /*if (rhyme.Length > 50)
                    {
                        return new DivanRhymeAnalysisResult()
                        {
                            Rhyme = "",
                            FailVerse = "",
                        };
                    }*/

                    if (rhyme.Length > 50)
                    {
                        rhyme = "";
                    }

                    return new DivanRhymeAnalysisResult()
                    {
                        Rhyme = rhyme,
                        FailVerse = ""
                    };

                }
            }
            catch
            {

            }

            return new DivanRhymeAnalysisResult() { Rhyme = "", FailVerse = "" };
        }


        /// <summary>
        /// format money
        /// </summary>
        /// <param name="amount"></param>
        /// <returns></returns>

        public static string FormatMoney(decimal amount)
        {
            return amount.ToString("N0", new CultureInfo("ur-PK")).ToPersianNumbers();
        }

        /// <summary>
        /// format datetime
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public static string FormatDate(DateTime dateTime)
        {
            return $"{dateTime.ToPersianYearMonthDay().Day.ToPersianNumbers()}م {PersianCulture.GetPersianMonthName(dateTime.ToPersianYearMonthDay().Month)} {dateTime.ToPersianYearMonthDay().Year.ToPersianNumbers()}";
        }

        /// <summary>
        /// short date
        /// </summary>
        /// <param name="dateTime"></param>
        /// <returns></returns>
        public static string FormatInShortDate(DateTime dateTime)
        {
            return dateTime.Year < 623 ? "" : $"{dateTime.ToPersianYearMonthDay().Year.ToPersianNumbers()}/{dateTime.ToPersianYearMonthDay().Month.ToString().PadLeft(2, '0').ToPersianNumbers()}/{dateTime.ToPersianYearMonthDay().Day.ToString().PadLeft(2, '0').ToPersianNumbers()}";
        }

    }
}
