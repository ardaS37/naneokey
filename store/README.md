# Nane Okey Microsoft Store packaging template

This folder contains a generic MSIX/Desktop Bridge packaging template for the locally built `NaneOkey.exe`.

Configure your own Partner Center package identity and publisher values locally. Credentials, certificates, passwords and generated packages are intentionally excluded from this repository; `store/package/` and `store/out/` are ignored.

The Microsoft Store signs packages submitted through Partner Center. For local testing, use a certificate managed in your own environment and never commit it.

See `make-msix.ps1` and the Microsoft packaging documentation for the required Windows SDK tools.
