# Nane Okey

Klasik masaustu Okey hissine yakin, `WinForms` tabanli ve `Windows 7 SP1+` hedefli oyun. v4.0.1.0 surumunde Nane Okey, Klasik Okey ve 101 Okey secilebilir. Cikti: `build/NaneOkey.exe`.

## v4 Oyun Secimi
- Tek oyunculu, Yeni Oyun, LAN oda kurma ve online oda kurma akisinda once oyun turu, sonra Oyun Ayarlari acilir.
- Nane Okey'in mevcut kurallari ve bot motoru korunur.
- Nane Okey ayarlarindaki `Yeni gorunumu kullan` kutusu varsayilan olarak kapalidir. Secilince yesil cuha, ahsap istakalar ve cizgisiz masa kullanilir; perleri bolme/yeniden duzenleme ve tas atmadan tur tamamlama kurallari aynidir. Tercih LAN ve online oda durumunda korunur.
- Klasik Okey: dort oyuncu, 106 tas, gosterge, iki gercek okey ve iki sahte okey; 14 tas, ilk oyuncuda 15 tas. Per veya 7 cift elde tamamlanir, son tas atilarak bitilir.
- 101 Okey: dort oyuncu, 21 tas, ilk oyuncuda 22 tas; katlamasiz 101 puan veya 5 cift acilis. Acilmis perler bolunemez veya ele geri alinamaz. Yandan alinan tas ayni tur masada kullanilir; kullanilamiyorsa ortadan cekmek alinan tasi geri birakir. Masadaki okey, temsil ettigi dogal tasla degistirilerek ele alinabilir.
- Klasik/101'de ilk oyuncu cekmeden atar; diger turlarda cekme ve atma zorunludur. Kendi atik alanina tek tas suruklemek turu tamamlar. Klasikte son tas `Bitir` alanina birakilir. `Tas At / Bitir` veya F4 alternatif secim ekranini acar. `Yandan Tas Al` onceki oyuncunun son atigini alir.
- Klasik/101 sirasi sagdaki oyuncuya gecer: Cenup, Sark, Simal, Garp. Gereksiz tas sagdaki atik alanina birakilir; onceki oyuncunun attigi tas soldan alinir. Kapali koltuklar atlanir. Nane Okey'in mevcut tur akisi korunur.
- Klasik/101 ceza puani kullanir; en dusuk toplam puan onde. Ayarlardaki ceza sinirina bir oyuncu ulasinca en dusuk toplam puanli oyuncu kazanir. Ayrintilar modun `Nasil Oynanir` ekranindadir.
- Klasik/101 canli onizleme kapatilir. LAN protokol v4, Photon uygulama surumu 4.0.1.0 kullanir; eski surumlerle ortak oda desteklenmez.
- 101 kural kaynaklari: [Zynga okey degisimi](https://zyngasupport.zendesk.com/hc/tr/articles/115003539012-Yerden-Okey-al%C4%B1nabilir-mi), [cift isleme](https://zyngasupport.zendesk.com/hc/tr/articles/115003521251-Seri-a%C3%A7an-biri-%C3%A7ifte-i%C5%9Fleme-yapabilir-mi), [deste bitisi cezasi](https://zyngasupport.zendesk.com/hc/tr/articles/115003521431-El-a%C3%A7madan-oyun-biterse-ne-kadar-ceza-al%C4%B1r%C4%B1m), [elden bitme](https://zyngasupport.zendesk.com/hc/tr/articles/115003521471-Elden-bitme-nedir), [Baro 101 turnuva kurallari madde 11](https://medya.barobirlik.org.tr/barowebsite/uploads/52/kural1.pdf). Cift acanin bir pere ayni tur en fazla iki tas islemesi dogrulanir. Deste bitince acilmayan 202, acilmis oyuncu elde kalan taslarinin cezasini alir; kimse acmadan elden bitis cezalari katlar. Ilk acilista 101 baraji korunur.

## Geleneksel Masalar ve Fare Kullanimi
- Nane Okey eski kareli tahtayi veya ayarlardan secilen yeni gorunumu kullanir. Her iki gorunumde ayni serbest duzenleme, tek tas/dizi surukleme ve Geri Al davranisi vardir.
- Rakiplerin istakalari ahsap arka yuz, iki raf rayi ve ince tas kenarlariyla cizilir; rakiplerin tas yuzleri gorunmez.
- Klasik Okey'in yesil cuha/ahsap masasi, gosterge, kapali deste, rakip istakalari ve dort yandaki atik yiginlari vardir. Perler elde tutulur; kazananin eli tur sonunda masada gosterilir.
- 101 masasi acilan perler ve ciftler icin ayri bolumler kullanir. Acik pere tek tas surukleyerek islenir; kalabalik masa fare tekerlegiyle kaydirilir. Bu tur hazirlanan acilis tas atilinca onaylanir.
- 101'de `Ctrl+tik` veya cift tik ile taslari secip `Per Ac` / `Cift Ac` alanina tikla. Yan yana bir diziyi sag tusla bu alanlara da surukleyebilirsin. `Geri Al`, bu tur masaya konan taslari geri getirir.
- Ortadaki desteden kapali tasi surukleyip bos istaka yuvasina birakarak tas alabilirsin. Iptal edilen veya dolu yuvaya birakilan surukleme desteyi tuketmez. Desteye tiklamak da tek tas ceker; onceki atik alanina tiklayarak soldan tas alinir. Secili tek tas icin kendi atik/Bitir alanina tiklamak da surukleme ile ayni islemi yapar.
- Gunluk ve sohbet, ustteki `Yardim > Gunluk / Sohbet` menusunden acilir. Dar pencerelerde yan gunluk gizlenerek masaya daha fazla alan ayrilir.
- `Bot Debug` baslangicta gizli ve kapalidir. `Yardim > Hakkinda` penceresinde F1'e uc kez basinca o oturum icin Yardim menusunde gorunur.

## Ekran Olceklemesi
Pencere ekranin kullanilabilir alanina sigar. Taslar en az 30x40 piksel, normal ekranlarda daha buyuk cizilir; numaralari buyutulup ortalanir. Istaka olcegi 24 sutunun tamamini, panel kenarliklarini ve ic/dis bosluklari hesaba katar. 1920x1080 ekranda 96/120/144 DPI icin iki raf ve 48 yuva yatay kaydirma olmadan sigar. Daha dar pencerelerde okunabilir tas boyutu korunarak gerektiginde kaydirma kullanilir. Nane masasinin 22x10 yuvasi da 1080p ekranda kaydirmadan sigar; masa taslari elden bagimsiz olceklenir. Dar pencerelerde tum yuvalar kaydirilarak erisilebilir. Yeni gorunumde rakip istakalari buyuk ve kenarlarda ortalidir. Bos masa hucreleri cizilmez, kece dokusu pencere boyutuna gore onbelleklenir ve surukleme sonunda yinelenen tam ekran cizimleri kaldirilmistir. Suruklenen tasi kaydirma alaninin kenarinda tutmak otomatik kaydirir. Klasik/101 baslangic elleri iki rafa dagilir. 800x600, 1024x768, 1366x768 ve 1920x1080 icin 96/120/144 DPI yerlesim ve okunabilirlik testleri vardir.

## Windows Derleme ve Test
Bu ortamda PowerShell ile `.\build.ps1` EXE uretir. `.\build.ps1 -Tests` motor, Nane regresyon, LAN loopback, ekran yerlesimi ve UI akis testlerini calistirir. Derleme .NET SDK Roslyn derleyicisini ve kurulu .NET Framework referanslarini kullanir; uygulama x86 .NET Framework olarak uretilir. Photon DLL, ikon, ifade/cay resimleri ve sesler EXE'e gomuludur. LAN testleri sadece 127.0.0.1 uzerinden gecici port kullanir.

Gercek Photon odasi ve ikinci fiziksel Windows 7 bilgisayariyla karsilikli oyun bu ortamda dogrulanmamistir.

## Nane Okey Kurallari
- Her oyuncu `15` tasla baslar.
- Joker yoktur.
- Gecerli seri: ayni renk, artan sayi, en az `3` tas.
- `12-13-1` gecerli seridir.
- Gecerli grup: ayni sayi, farkli renk, en az `3` tas.
- Oyuncu hamle boyunca masadaki taslari bolup yeniden duzenleyebilir.
- `Hamleyi Onayla` aninda masadaki tum seriler ve gruplar gecerli olmak zorundadir.
- Ilk acilista oyuncu elinden en az `2` gecerli seri/grup birakmalidir.
- Ilk acilistan sonra oyuncu dogrudan masaya isleyebilir.
- Islenemeyen turde oyuncu ortadan `1` tas ceker.
- Zorunlu tas atma yoktur; eldeki tas sayisi artabilir.
- Oyun, bir oyuncunun elinde hic tas kalmadiginda biter.

## Teknik Yapi
- `src/NaneOkey/NaneOkey.csproj`: WinForms masaustu uygulamasi.
- `Domain`: tas, grup, oyuncu, oyun durumu modelleri.
- `Engine`: kural dogrulama, deste, tur yonetimi, basit bot davranisi.
- `Network`: ayni agdaki host/join icin temel LAN lobby ve mesajlasma altyapisi.
- `UI`: klasik mavi masa gorunumu, 4 yon oyuncu yerlesimi ve sec-birak hamle akisi.

## Uygulanan Plan Ozetleri
- Oyun motoru UI'dan ayri tutuldu.
- Gecici tur duzenleme ve hamle onay mekanigi kuruldu.
- Basit botlar acilis yapmayi, masa uzerine ekleme denemeyi ve olmuyorsa tas cekmeyi destekliyor.
- Sol menulu klasik masaustu gorunumu kuruldu.
- LAN icin host ve join akisi eklendi.

## Build Notlari
- Kod, bu ortamda `mcs` ile derleme kontrolunden gecti.
- Visual Studio tarafinda hedef `Windows 7 SP1 ve uzeri` sistemlerde `exe` uretmek icin proje dosyasi `TargetFrameworkVersion v4.7.2` kullanir.
- Windows'ta derlemek icin Visual Studio 2019/2022 veya .NET Framework Developer Pack yeterlidir.
