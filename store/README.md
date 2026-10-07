# Nane Okey Microsoft Store Paketi

Bu klasör WinForms `NaneOkey.exe` için MSIX/Desktop Bridge paketleme şablonudur.

## Gerekenler

- Windows 10/11 SDK (`makeappx.exe`, opsiyonel `signtool.exe`)
- Microsoft Partner Center uygulama kimliği
- Store'daki `Package/Identity/Publisher` değeri
- İmzalama için PFX sertifikası gerekiyorsa aynı Publisher ile oluşturulmuş sertifika

## Kullanım

Önce normal exe'yi üret:

```powershell
# build\NaneOkey.exe hazır olmalı
```

Sonra paketle:

```powershell
powershell -ExecutionPolicy Bypass -File .\store\make-msix.ps1 `
  -PackageName "Sapsoft.NaneOkeyOyunu" `
  -Publisher "CN=7F83F93C-2867-4649-9D70-B32934D533D5" `
  -PublisherDisplayName "Sapsoft" `
  -Version "4.0.1.0"
```

İmzalı paket gerekiyorsa:

```powershell
powershell -ExecutionPolicy Bypass -File .\store\make-msix.ps1 `
  -PackageName "Sapsoft.NaneOkeyOyunu" `
  -Publisher "CN=7F83F93C-2867-4649-9D70-B32934D533D5" `
  -PublisherDisplayName "Sapsoft" `
  -Version "4.0.1.0" `
  -PfxPath "C:\path\sertifika.pfx" `
  -PfxPassword "sifre"
```

Çıktı: `store\out\NaneOkey_4.0.1.0_x86.msix`

## Güncel Store Güncellemesi

- Paket adı: `Sapsoft.NaneOkeyOyunu`
- Publisher: `CN=7F83F93C-2867-4649-9D70-B32934D533D5`
- PublisherDisplayName: `Sapsoft`
- Uygulama / EXE / paket sürümü: `4.0.1.0` (önceki paket: `4.0.0.0`)
- Mimari: `x86`
- Store hedefi: Windows 10 1809 (`10.0.17763.0`) ve üzeri.
- Paketleme, EXE sürümünün paket sürümüyle aynı olduğunu kontrol eder; Store sürümünün son parçası `0` olmalıdır.
- Partner Center yüklemesi için PFX gerekmez; Store MSIX paketini kendisi imzalar. Yerel kurulum için imzalı paket ayrıca üretilebilir.
- Partner Center'da mevcut uygulamanın yeni gönderimini oluşturup Paketler bölümüne güncel MSIX dosyasını yükle. Kimlik bilgileri mevcut uygulamayla aynı tutulur.
- Güncelleme notları: `store\release-notes-4.0.1.0.txt`

Kaynaklar: [Store paket gereksinimleri](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/app-package-requirements?pivots=store-installer-msix), [MSIX imzalama](https://learn.microsoft.com/en-us/windows/msix/package/sign-msix-package-guide).

## Notlar

- Bu paket `runFullTrust` kullanan Desktop Bridge paketidir.
- LAN için `privateNetworkClientServer`, Photon için `internetClient` capability vardır.
- Store'a yüklemeden önce manifestteki `Publisher` kesinlikle Partner Center'daki Publisher ile aynı olmalıdır.
