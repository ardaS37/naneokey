using System.Drawing;
using System.Windows.Forms;
using NaneOkey.Domain;

namespace NaneOkey.UI
{
    public sealed class HowToPlayForm : Form
    {
        public HowToPlayForm()
            : this(GameMode.NaneOkey)
        {
        }

        public HowToPlayForm(GameMode mode)
        {
            Text = "Nasıl Oynanır";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Width = 760;
            Height = 620;
            BackColor = Color.FromArgb(238, 229, 212);

            var titleLabel = new Label
            {
                Left = 16,
                Top = 14,
                Width = 710,
                Height = 28,
                Font = new Font("Tahoma", 11F, FontStyle.Bold),
                Text = "Nane Okey Kuralları"
            };

            var textBox = new TextBox
            {
                Left = 16,
                Top = 50,
                Width = 710,
                Height = 490,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = Color.White,
                Font = new Font("Tahoma", 9.5F, FontStyle.Regular),
                Text =
                    "1. Oyun 4 kişi oynanır." + "\r\n\r\n" +
                    "2. Başlangıçta herkese 15 taş dağıtılır." + "\r\n\r\n" +
                    "3. Joker yoktur." + "\r\n\r\n" +
                    "4. Geçerli per türleri:" + "\r\n" +
                    "   - Aynı sayı, farklı renk, yan yana en az 3 taş." + "\r\n" +
                    "   - Aynı renk, soldan sağa artan sayı sırası ile en az 3 taş." + "\r\n\r\n" +
                    "5. 1 taşı seride sadece 13'ten sonra kullanılabilir. Yani 12-13-1 geçerlidir, ama 13-1-2 geçerli değildir." + "\r\n\r\n" +
                    "6. Hamle sırasında oyuncu masadaki taşları istediği gibi düzenleyebilir. Ancak Hamleyi Oyna dediğinde masadaki bütün perler tamamen geçerli olmak zorundadır." + "\r\n\r\n" +
                    "7. İlk açılışı yapan oyuncu elinden en az 2 geçerli per açmalıdır." + "\r\n\r\n" +
                    "8. Masada daha önce biri açtıysa, ilk açılışını yapacak oyuncu elinden en az 1 geçerli per koyarak açılabilir." + "\r\n\r\n" +
                    "9. Aynı ilk hamlede hem açıp hem ortadaki taşlara işleme yapılabilir." + "\r\n\r\n" +
                    "10. Taş atma yoktur. Bu yüzden eldeki taş sayısı artabilir." + "\r\n\r\n" +
                    "11. Hamle yapılamazsa oyuncu ortadan 1 taş çeker ve sıra geçer." + "\r\n\r\n" +
                    "12. Bir turu, elindeki bütün taşları geçerli şekilde masaya bitiren oyuncu kazanır." + "\r\n\r\n" +
                    "13. Tur sonu puanlama:" + "\r\n" +
                    "   - Turu kazanan oyuncu 100 taban puan alır." + "\r\n" +
                    "   - Rakiplerin ellerinde kalan taşların sayı toplamı da tur kazananına eklenir." + "\r\n" +
                    "   - Hiç açılmamış oyuncu ayrıca 100 ceza verir; bu puan da tur kazananına eklenir." + "\r\n" +
                    "   - Kaybeden oyuncuların kendi elinde kalan taş toplamı ve varsa açılmama cezası kendi puanından düşer." + "\r\n" +
                    "   - En yüksek toplam puan öndedir." + "\r\n\r\n" +
                    "14. Genel kazanan, oyun başında belirlenen hedef puana ulaşan oyuncudur. Aynı turda birden fazla oyuncu hedefi geçerse puanı daha yüksek olan kazanır." + "\r\n\r\n" +
                    "15. Fare kullanımı:" + "\r\n" +
                    "   - Sol tık ile eldeki taşı veya masadaki taşı tutup sürükleyebilirsin." + "\r\n" +
                    "   - Ortadaki deste üstünden sol tıkla sürükleyip elindeki boş slota bırakarak taş çekebilirsin." + "\r\n" +
                    "   - Elde veya masada taş üstünde orta tık, taşı görsel olarak çevirmeye yarar." + "\r\n" +
                    "   - Elde veya masada bir dizinin üstünde sağ tıkla o diziyi topluca sürükleyebilirsin." + "\r\n\r\n" +
                    "16. Klavye kısayolları:" + "\r\n" +
                    "   - F1: Nasıl Oynanır" + "\r\n" +
                    "   - F2: Ana Menü" + "\r\n" +
                    "   - F3: Yeni Oyun" + "\r\n" +
                    "   - F4: Hamleyi Oyna" + "\r\n" +
                    "   - F5: Ortadan Taş Çek" + "\r\n" +
                    "   - F6: Geri Al" + "\r\n" +
                    "   - F7: Oto Diz" + "\r\n" +
                    "   - F8: Tahta Rengi" + "\r\n" +
                    "   - F9: Oda Kur" + "\r\n" +
                    "   - F10: Katıl"
            };

            var closeButton = new Button
            {
                Text = "Kapat",
                Left = 616,
                Top = 550,
                Width = 110,
                Height = 30,
                DialogResult = DialogResult.OK
            };

            if (mode != GameMode.NaneOkey)
            {
                titleLabel.Text = GameModeForm.ModeName(mode) + " Kuralları";
                textBox.Text = "Dört kişi oynanır. Gösterge rastgele seçilir. Göstergenin aynı renkte bir sonraki sayısı okeydir (13'ten sonra 1). Okey her taşın yerine geçer; yıldızlı sahte okey yalnız okey taşının gerçek sayı ve rengini temsil eder.\r\n\r\n" +
                    (mode == GameMode.ClassicOkey
                    ? "Oyuncular 14 taşla, ilk oyuncu 15 taşla başlar. İlk oyuncu taş çekmeden atar. Sonraki turlarda ortadan veya önceki oyuncunun son atığından bir taş alınır, ardından bir taş atılır.\r\n\r\nElindeki 14 taşın tamamı en az üç taşlı seriler/gruplar ya da yedi aynı renk ve sayı çifti olunca son taşını Bitir alanına bırakarak kazanırsın. Perler elde kalır; klasik masada açma alanı yoktur. Aynı renk ardışık seri, aynı sayı farklı renk grup geçerlidir. 12-13-1 geçerli, 13-1-2 geçersizdir.\r\n\r\nKazanan 0, diğerleri 2 ceza puanı alır. Okey atarak veya yedi çiftle bitiş cezayı ikiye katlar. Deste tükenirse tur puansız biter."
                    : "Oyuncular 21 taşla, ilk oyuncu 22 taşla başlar. İlk oyuncu çekmeden atar; sonraki turlarda çekip atılır. Yandan aldığın taşı aynı tur masada kullanmalısın. Kullanamıyorsan desteye tıklamak alınan taşı geri bırakıp ortadan çeker.\r\n\r\nİlk açılış tek turda en az 101 puanlık seri/grup ya da en az 5 çift olmalıdır. Seri aynı renkte ardışık en az 3 taş, grup aynı sayı farklı renk en az 3 taştır. Seride 1 yalnız başta kullanılır; 12-13-1 geçersizdir. Çift aynı renk ve sayıda iki taştır. Katlama yoktur.\r\n\r\nCtrl+tık ile taşlarını seçip Per Aç veya Çift Aç alanına tıkla. Yan yana taşları sağ tuşla bu alanlara sürükleyebilirsin. Perler ve çiftler masanın ayrı bölümlerine yerleşir; kalabalık masada fare tekerleği ile kaydır.\r\n\r\nAçıldıktan sonra tek taşı açık perin üzerine sürükleyerek işle. Açılmış perler parçalanamaz veya ele geri alınamaz. Masadaki okeyin temsil ettiği doğal taşı perine bırakırsan okeyi eline alırsın. Çift açan yeni seri açamaz; aynı turda bir pere en fazla iki taş işleyebilir. Seri açan masada çift açılmışsa çift işleyebilir.\r\n\r\nBir taşı kendi atık alanına bırakınca açılış ve işleme onaylanır. Onaydan önce Geri Al masaya bu tur koyduklarını geri getirir. Bitmek için son bir taş atılmalıdır.\r\n\r\nKazanan -101, açılmamış oyuncu 202 ceza alır. Açık oyuncunun elindeki sayı toplamı cezasıdır; eldeki okey 101 puandır. Çift açanların cezası ikiye katlanır. Okey atarak veya çift açıp bitiş cezaları ikiye katlar. Kimse açmadan elden bitiş de cezayı ikiye katlar. Deste bittiğinde açmayan 202, açan elde kalan taşlarının cezasını alır.") +
                    "\r\n\r\nHer iki modda en düşük toplam ceza öndedir. Bir oyuncu ayarlardaki ceza sınırına ulaşınca en düşük toplam puanlı oyuncu genel kazanandır.\r\n\r\nKullanım: Deste veya önceki atık alanına tıklayarak taş al; elindeki taşı kendi atık alanına sürükleyerek at. Ctrl+tık veya çift tık ile seçtiğin tek taş için atık/Bitir alanına da tıklayabilirsin. F4 Taş At / Bitir, F5 Ortadan Taş Çek, F6 Geri Al, F7 Oto Diz. Okey taşları O işareti, sahte okey yıldız ile gösterilir.";
            }

            Controls.Add(titleLabel);
            Controls.Add(textBox);
            Controls.Add(closeButton);

            AcceptButton = closeButton;
            CancelButton = closeButton;
        }
    }
}
