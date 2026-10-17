# Security policy

## Supported versions

Security fixes go into the latest release. Please update before reporting.

## Reporting a vulnerability

Please **don't** open a public issue for security problems. Use GitHub's private reporting instead: [Report a vulnerability](https://github.com/anand34577/winspeed/security/advisories/new).

Include what you found, how to reproduce it and the version affected. You'll get a reply as soon as possible, and credit in the release notes if you'd like.

## Verifying downloads

Every release is built from this repository by GitHub Actions. Each release includes `SHA256SUMS.txt`; compare it with:

```powershell
Get-FileHash .\WinSpeed-Setup-1.0.0.exe -Algorithm SHA256
```
