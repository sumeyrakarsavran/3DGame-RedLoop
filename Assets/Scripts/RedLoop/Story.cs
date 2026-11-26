namespace RedLoop
{
    public enum LevelKind { Courtyard, Forest, Harbor, Unknown }

    public struct Line
    {
        public string speaker;
        public string text;
        public Line(string speaker, string text) { this.speaker = speaker; this.text = text; }
    }

    // RED LOOP — Kızıl Döngü
    // Oyunun bütün hikâye metinleri burada. Metinleri değiştirmek için sadece bu dosyayı düzenlemek yeterli.
    public static class Story
    {
        public const string Ayla = "AYLA";
        public const string Echo = "YANKI";
        public const string Deniz = "DENİZ";
        public const string Memory = "HATIRA";
        public const string Narrator = "";

        public const string GameTitle = "RED LOOP";
        public const string GameSubtitle = "K I Z I L   D Ö N G Ü";

        public const string Prologue =
            "Şehrin saati bir gece 03:33'te durdu.\n" +
            "O geceden beri aynı kızıl gece yeniden ve yeniden yaşanıyor.\n" +
            "Buna <color=#ff4a4a>Kızıl Döngü</color> dediler.\n\n" +
            "Döngüye teslim olanlar paslı muhafızlara dönüştü.\n" +
            "Okçu Ayla ise her düştüğünde aynı avluda uyanıyor.\n" +
            "Ama bu gece bir şey farklı: zamanın kırık parçaları ona sesleniyor.";

        public const string HowToPlay =
            "<b>W A S D</b> / Yön tuşları — Hareket\n" +
            "<b>Fare</b> — Kamera\n" +
            "<b>Space</b> — Zıpla\n" +
            "<b>Shift</b> — Koş\n" +
            "<b>Sol tık / F</b> — Ok at (nişangâh ekranın ortasında)\n" +
            "<b>Esc</b> — Duraklat\n" +
            "<b>Space / E / Tık</b> — Diyaloğu ilerlet\n\n" +
            "Robotlar atıştan önce kızarıp uğuldar: o an yer değiştir!\n" +
            "Üç darbe alırsan döngü seni başa sarar. Pembe kalpler can verir.\n" +
            "Ekrandaki <color=#ffd36b>sarı elmas</color> işareti seni bir sonraki hedefe götürür.";

        public static string ChapterNumber(LevelKind k)
        {
            switch (k)
            {
                case LevelKind.Courtyard: return "BÖLÜM I";
                case LevelKind.Forest: return "BÖLÜM II";
                case LevelKind.Harbor: return "BÖLÜM III";
                default: return "BÖLÜM ?";
            }
        }

        public static string ChapterName(LevelKind k)
        {
            switch (k)
            {
                case LevelKind.Courtyard: return "Kızıl Avlu";
                case LevelKind.Forest: return "Yankılar Ormanı";
                case LevelKind.Harbor: return "Eski Liman";
                default: return "Bilinmeyen Yer";
            }
        }

        public static Line[] Intro(LevelKind k)
        {
            switch (k)
            {
                case LevelKind.Courtyard:
                    return new[]
                    {
                        new Line(Narrator, "Gökyüzü yine kızıl. Rüzgâr yine aynı yönden esiyor."),
                        new Line(Ayla, "Kaçıncı kez uyandım? Yüz mü, bin mi... Artık saymıyorum."),
                        new Line(Echo, "Ayla. Beni duyabiliyor musun? Etrafındaki altın ışıltılar zaman parçaları."),
                        new Line(Echo, "Döngü kendini onlarla bir arada tutuyor. Yeterince topla; merdivenin tepesindeki kapı ancak o zaman açılır."),
                        new Line(Ayla, "Ya muhafızlar?"),
                        new Line(Echo, "Onlar da bir zamanlar senin gibi kaçmaya çalıştı. Atmadan önce kızarıp uğuldarlar; o an yerinde durma."),
                        new Line(Echo, "Yayın hâlâ elinde, Ayla. İki ok bir muhafızı devirir ve onu döngüden kurtarır. Üç darbe alırsan... her şey başa döner."),
                    };
                case LevelKind.Forest:
                    return new[]
                    {
                        new Line(Narrator, "Kapının öbür yanında hava serinledi. Palmiyelerin arasındaki karanlık sanki nefes alıyordu."),
                        new Line(Ayla, "Burayı tanıyorum... Deniz'le saklambaç oynadığımız bahçe bu."),
                        new Line(Echo, "Bu orman senin hatıralarından yapılmış. Döngü, en çok acıtanları karanlığa gömdü."),
                        new Line(Echo, "Fenerin yolunu aydınlatacak. Dört hatıra ışığını bul; geçit ancak hatırladığında açılır."),
                        new Line(Echo, "Dikkat et: karanlıkta mor gölgeler dolaşıyor. Korkularından doğdular. Tek ok onları dağıtır."),
                    };
                case LevelKind.Harbor:
                    return new[]
                    {
                        new Line(Narrator, "Eski liman. Dalgalar kızıl ışığı kıyıya taşıyor."),
                        new Line(Ayla, "Söz verdiğimiz yer... Güneşin doğuşunu buradan izleyecektik. Ama güneş hiç doğmadı."),
                        new Line(Echo, "Döngünün Kalbi en yüksek çatıda atıyor. Kızıl ışık sütununu takip et."),
                        new Line(Echo, "Kalp seni hissettiğinde gökten kızıl küreler yağdıracak. Yerdeki kızıl halkalara dikkat et ve durma."),
                        new Line(Ayla, "Deniz... Bu sefer geliyorum."),
                    };
                default:
                    return new[] { new Line(Echo, "Burası da döngünün bir parçası. Yolunu bul, Ayla.") };
            }
        }

        // Kızıl Avlu'da zaman parçası toplandıkça gelen kısa replikler (kaçıncı parça -> replik).
        public static bool ShardLine(int taken, int total, out Line line)
        {
            if (total <= 0) { line = default(Line); return false; }
            if (taken == total) { line = default(Line); return false; } // kapı açılma repliği ayrı
            if (taken == 1) { line = new Line(Ayla, "Sıcak... Parçayı tutunca bir an güneşi hatırladım."); return true; }
            if (taken == 3 && total > 4) { line = new Line(Ayla, "Bir çocuk kahkahası duydum. Deniz? Kardeşim miydi o?"); return true; }
            if (taken == total / 2 && total >= 6) { line = new Line(Echo, "Hissediyor musun? Döngü titriyor. Devam et."); return true; }
            if (taken == total - 1) { line = new Line(Echo, "Son parça kaldı. Merdivenin tepesine bak."); return true; }
            line = default(Line);
            return false;
        }

        public static readonly Line[] Memories =
        {
            new Line(Deniz, "\"Abla, güneş doğunca limana gidelim mi? Söz mü?\""),
            new Line(Memory, "Saat 03:33. Sirenler çaldı ve gökyüzü bir anda kızıla boyandı."),
            new Line(Ayla, "Kalabalıkta Deniz'in elini bıraktım. Sadece bir anlığına..."),
            new Line(Deniz, "\"Beni limanda, en yüksek çatıda bekle abla. Ben korkmayacağım.\""),
        };

        public static Line PortalOpened(LevelKind k)
        {
            switch (k)
            {
                case LevelKind.Forest: return new Line(Echo, "Artık hatırlıyorsun. Geçit seni limana götürecek.");
                default: return new Line(Echo, "Kapı açıldı! Işığa yürü, Ayla.");
            }
        }

        public static string PortalLocked(LevelKind k, int remaining)
        {
            if (k == LevelKind.Forest) return "Geçit kapalı. " + remaining + " hatıra ışığı daha bulmalısın.";
            return "Kapı mühürlü. " + remaining + " zaman parçası daha gerek.";
        }

        public static string Objective(LevelKind k, bool complete)
        {
            switch (k)
            {
                case LevelKind.Courtyard:
                    return complete ? "Merdivenin tepesindeki kapıya ulaş" : "Zaman parçalarını topla";
                case LevelKind.Forest:
                    return complete ? "Geçide gir" : "Hatıra ışıklarını bul";
                case LevelKind.Harbor:
                    return "Kızıl ışık sütununu takip et — Döngünün Kalbi'ne ulaş";
                default:
                    return complete ? "Çıkışı bul" : "Etrafı keşfet";
            }
        }

        public static readonly Line LowHealth = new Line(Ayla, "Bir darbe daha alırsam... her şey başa döner.");
        public static readonly Line Fell = new Line(Echo, "Boşluk seni geri tükürdü. Döngü kolay kolay bırakmaz.");
        public static readonly Line NearHeart = new Line(Echo, "Yaklaştın... Kalp hızlanıyor. Durma!");

        public static string RetryCaption(int loop)
        {
            return "Döngü #" + loop + " — Ayla yeniden uyandı.";
        }

        public static readonly Line[] Finale =
        {
            new Line(Narrator, "Kalbin kızıl ışığında küçük bir siluet belirdi."),
            new Line(Deniz, "Abla? Bu sefer... gerçekten geldin."),
            new Line(Echo, "Döngüyü ben başlattım, Ayla. O gece seni kaybetmemek için zamanı durdurdum."),
            new Line(Echo, "Ben Deniz'in korkusuyum. Sabah gelmesin diye aynı geceyi sonsuza dek tekrarladım."),
            new Line(Ayla, "Artık korkmana gerek yok. Söz vermiştik, hatırladın mı? Güneşin doğuşunu birlikte izleyeceğiz."),
            new Line(Deniz, "...Söz."),
            new Line(Narrator, "Ayla yayını gerdi. Son ok, Kızıl Kalp'in tam ortasına saplandı."),
        };

        public const string GameOverTitle = "DÖNGÜ SENİ YUTTU";
        public const string GameOverSubtitle = "Saat yine 03:33...";

        public const string VictoryTitle = "DÖNGÜ KIRILDI";
        public const string VictoryBody =
            "Yıllardır ilk kez saat ilerledi.\n" +
            "Kızıl gökyüzü soldu ve limanın üstünden güneş doğdu.\n" +
            "Ayla ile Deniz sözlerini tuttular.";
        public const string Credits = "RED LOOP — Kızıl Döngü\nOyunu oynadığın için teşekkürler.";
    }
}
