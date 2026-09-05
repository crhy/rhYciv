# rhYciv website

Static Cloudflare Pages site for `rhyciv.org` and `rhyciv.com`.

Cloudflare Pages settings:
- Production branch: `master`
- Framework preset: None
- Build command: `bash website/build.sh`
- Build output directory: `website/dist`

The build copies current art from `RaylibUI/FOSSart`, so future art replacements remain easy.

Recommended canonical host: `https://rhyciv.org`. Redirect `www.rhyciv.org`, `rhyciv.com`, and `www.rhyciv.com` to the canonical host while preserving paths and query strings.
