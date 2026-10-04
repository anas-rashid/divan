using RMuseum.Models.Divan;

namespace RMuseum.Services.Implementation
{
    /// <summary>
    /// poem format convertor
    /// </summary>
    public static class DivanPoemFormatConvertor
    {
        /// <summary>
        /// تبدیل به رشته
        /// </summary>
        /// <param name="format"></param>
        /// <returns></returns>
        public static string GetString(DivanPoemFormat? format)
        {
            if (format == null) return "";
            switch (format)
            {
                case DivanPoemFormat.Ghazal:
                    return "غزل";
                case DivanPoemFormat.Ghaside:
                    return "قصیده";
                case DivanPoemFormat.Masnavi:
                    return "مثنوی";
                case DivanPoemFormat.Ghete:
                    return "قطعه";
                case DivanPoemFormat.Robaee:
                    return "رباعی";
                case DivanPoemFormat.Dobeyti:
                    return "دوبیتی";
                case DivanPoemFormat.Generic:
                    return "غزل/قصیده/قطعه";
                case DivanPoemFormat.TarkibBand:
                    return "ترکیب بند";
                case DivanPoemFormat.Takbeyt:
                    return "تک بیت";
                case DivanPoemFormat.Nimaee:
                    return "نیمایی";
                case DivanPoemFormat.TarjeeBand:
                    return "ترجیع بند";
                case DivanPoemFormat.Mosammat3:
                    return "مسمط مثلث";
                case DivanPoemFormat.Mostazad:
                    return "مستزاد";
                case DivanPoemFormat.RobaeeMostazad:
                    return "رباعی مستزاد";
                case DivanPoemFormat.Mosammat4:
                    return "مسمط مربع";
                case DivanPoemFormat.Mosammat5:
                    return "مسمط مخمس";
                case DivanPoemFormat.Sepeed:
                    return "سپید";
                case DivanPoemFormat.New:
                    return "نیمایی یا سپید";
                case DivanPoemFormat.Mosammat6:
                    return "مسمط مسدس";
                case DivanPoemFormat.Mosammat8:
                    return "مسمط مثمن";
                case DivanPoemFormat.Mosammat:
                    return "مسمط";
                case DivanPoemFormat.ChaharPare:
                    return "چهارپاره";
                case DivanPoemFormat.MultiBand:
                    return "چند بندی";
                case DivanPoemFormat.BahreTavil:
                    return "بحر طویل";
                case DivanPoemFormat.Unknown:
                    return "نامشخص";
            }
            return "";
        }
    }
}
