PEMS — Parmis Enterprise Management System
Web & Admin Panel Icon Pack

All PNG files are RGBA with transparent pixels outside the rounded navy icon. The navy square is part of the approved icon design and is intentionally retained.

Recommended usage:
- favicon.ico : browser tab (multiple sizes)
- pems-icon-32.png : favicon PNG
- pems-icon-192.png : PWA manifest
- pems-icon-512.png : PWA manifest
- pems-icon-256.png : admin panel menu / shortcut
- pems-icon-1024.png : master raster icon
- pems-logo-transparent.png : full-color transparent marketing logo / login page. This is NOT an SVG vector.

Example HTML:
<link rel="icon" type="image/x-icon" href="/favicon.ico">
<link rel="icon" type="image/png" sizes="32x32" href="/pems-icon-32.png">
<link rel="apple-touch-icon" href="/pems-icon-180.png">

Example manifest icon entries:
"icons": [{"src":"/pems-icon-192.png","sizes":"192x192","type":"image/png"},{"src":"/pems-icon-512.png","sizes":"512x512","type":"image/png"}]

Place the files under the website's static assets directory. No PEMS repository files were modified.
