using System.Drawing;
using System.Windows.Forms;

namespace NaneOkey.UI
{
    public sealed class HowToPlayForm : Form
    {
        public HowToPlayForm()
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

            Controls.Add(titleLabel);
            Controls.Add(textBox);
            Controls.Add(closeButton);

            AcceptButton = closeButton;
            CancelButton = closeButton;
        }
    }
}
