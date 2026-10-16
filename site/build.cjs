// Static website only. Binaries are downloaded directly from the published GitHub release.
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '..');
const destination = path.join(root, 'dist/site');
fs.mkdirSync(destination, {recursive:true});
for (const file of ['index.html','docs.html','robots.txt','sitemap.xml','style.css','app.js','FerryClip-logo.png','FerryClip-Android-APK-QR.svg']) fs.copyFileSync(path.join(__dirname,file),path.join(destination,file));
fs.cpSync(path.join(__dirname,'images'),path.join(destination,'images'),{recursive:true});
for (const file of ['LICENSE','THIRD_PARTY_NOTICES.md']) fs.copyFileSync(path.join(root,file),path.join(destination,file));
const legacyChecksums = path.join(destination, 'downloads', 'SHA256SUMS.txt');
if (fs.existsSync(legacyChecksums)) fs.unlinkSync(legacyChecksums);
console.log('Built static website with current fixed-name GitHub release links.');
