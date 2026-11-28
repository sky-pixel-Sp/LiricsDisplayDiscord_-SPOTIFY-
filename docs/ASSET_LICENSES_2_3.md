# SkyLyrics Desktop 2.3 — Asset License Register

This register is the approval record for files shipped with SkyLyrics Desktop 2.3. An asset must have an identifiable source and redistribution basis before it is included in a redistributable package.

## Approval rules

- Do not add album artwork, commercial illustrations, screenshots, proprietary interface captures, or stock images without documented rights.
- Keep original master files where source quality permits and record the transformation used for each optimized derivative.
- Record creator/source, license name or terms URL, attribution requirement, redistribution permission, and a SHA-256 computed from the exact file.
- An unknown or unverified license is a release blocker for that asset. Do not infer a license from a file extension, visual style, or filename.
- Font licensing is independent from the license of any image, video, or UI artwork.

## Existing repository files

| Asset group | Current evidence | 2.3 disposition |
|---|---|---|
| Poppins | Existing OFL notice under `Assets/Fonts/Poppins/OFL.txt` | Retain notice; verify notice covers the exact font binary before redistribution |
| Bebas Neue | Existing OFL notice under `Assets/Fonts/BebasNeue/OFL.txt` | Retain notice; verify notice covers the exact font binary before redistribution |
| Existing SVG backgrounds, textures, icons, logo and note symbols | Present in 2.2 repository; per-file provenance is not recorded in the audited tree | Do not represent as newly produced 2.3 raster assets; provenance review remains open |
| Existing HLSL effect sources | Present in repository as source code | Not multimedia artwork; retain existing source history |

## 2.3 generated media

No generated media file has been committed to the repository at the time of this register's creation. Generated files are not listed as approved assets until their exact bytes, source record, applicable usage terms, and redistribution decision are checked.

## Release gate

The asset catalog and this register must agree on each shipped file's stable ID, file path, byte size, and SHA-256. Any mismatch fails asset validation.
