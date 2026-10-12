// Static website only. Binaries are downloaded directly from the published GitHub release.
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '..');
const destination = path.join(root, 'dist/site');
fs.mkdirSync(destination, {recursive:true});
for (const file of ['index.html','docs.html','style.css','app.js','FerryClip-logo.png']) fs.copyFileSync(path.join(__dirname,file),path.join(destination,file));
fs.cpSync(path.join(__dirname,'images'),path.join(destination,'images'),{recursive:true});
for (const file of ['LICENSE','THIRD_PARTY_NOTICES.md']) fs.copyFileSync(path.join(root,file),path.join(destination,file));
const downloads = path.join(destination,'downloads');
fs.mkdirSync(downloads,{recursive:true});
fs.writeFileSync(path.join(downloads,'SHA256SUMS.txt'), 'c6b8bad52a7dbc622d8db5e3ff15477408765041dfd51785fcd7d60ab7d0447c  FerryClip-0.8.16-Windows.exe\n64fdfa629c681d2c5ec6ce6a19bbc4a12bcf331f87c029397fecaa54fd71d8e7  FerryClip-0.8.16-Android.apk\n');
console.log('Built static website with FerryClip 0.8.16 release links and checksums.');
