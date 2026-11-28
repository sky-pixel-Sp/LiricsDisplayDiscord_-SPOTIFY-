# SkyLyrics Desktop 2.3 — Asset License Register

This register is the approval record for files shipped with SkyLyrics Desktop 2.3. An asset must have an identifiable source and a documented redistribution basis before it is included in the installer.

## Approval rules

- Do not add album artwork, commercial illustrations, screenshots, proprietary interface captures, or stock images without documented rights.
- Keep original master files where source quality permits and record every optimized derivative.
- Record creator/source, license name or terms URL, attribution requirement, redistribution permission, and a SHA-256 computed from the exact file.
- An unknown or unverified license is a release blocker for that asset. Do not infer a license from a file extension, visual style, or filename.
- Font licensing is independent from the license of any image, video, or UI artwork.

## Existing repository files

| Asset group | Current evidence | 2.3 disposition |
|---|---|---|
| Poppins | Existing OFL notice under `Assets/Fonts/Poppins/OFL.txt` | Retain notice; verify it covers the exact font binary before redistribution |
| Bebas Neue | Existing OFL notice under `Assets/Fonts/BebasNeue/OFL.txt` | Retain notice; verify it covers the exact font binary before redistribution |
| Existing SVG backgrounds, textures, icons, logo and note symbols | Present in the 2.2 repository; per-file provenance is not recorded in the audited tree | Do not represent them as newly produced 2.3 raster assets; provenance review remains open |
| Existing HLSL effect sources | Present in repository as source code | Not multimedia artwork; retain existing source history |

## Generated Aurora Fjord starter asset (not committed)

A separate starter package was generated in the current production workspace. It contains:
- Master PNG: 1672×941, 2,249,034 bytes.
- Display WebP: 1280×720, 86,286 bytes.
- Thumbnail WebP: 480×270, 17,662 bytes.

The package has an exact SHA-256 manifest. It is **not part of the repository, build, installer, or the committed asset catalog**. The asset source is OpenAI image generation, with a prompt authored for SkyLyrics. Before adding it to a public/re-distributable installer, the publisher must record the applicable OpenAI terms version, confirm output ownership/redistribution conditions under those terms and applicable law, and confirm that no third-party rights are implicated. Until that review is recorded in the repository, its status remains **review required**.

## Release gate

The generated catalog and this register must agree on every shipped file's stable ID, file path, byte size, and SHA-256. Any mismatch fails asset validation. An asset with a pending license review must not be included in a redistributable installer.
