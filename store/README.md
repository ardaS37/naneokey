# Microsoft Store packaging template

This folder contains a sanitized MSIX/Desktop Bridge packaging template for Nane Okey.

## What is intentionally excluded

- Partner Center package identity and publisher values
- Certificates, PFX files, passwords, and signing data
- Generated `package/` layout files
- Generated `.msix` files

## Requirements

- Windows 10/11 SDK with `makeappx.exe`
- A Windows desktop build of `NaneOkey.exe`
- Your own Microsoft Partner Center identity values
- An optional code-signing certificate that you keep outside this repository

## Example

```powershell
powershell -ExecutionPolicy Bypass -File .\store\make-msix.ps1 `
  -PackageName "YOUR_PACKAGE_NAME" `
  -Publisher "CN=YOUR_PARTNER_CENTER_PUBLISHER" `
  -PublisherDisplayName "YOUR_PUBLISHER_NAME" `
  -Version "1.0.0.0"
```

For a signed local package, pass `-PfxPath` and `-PfxPassword` from a secure local location. Do not commit those values or the certificate.
