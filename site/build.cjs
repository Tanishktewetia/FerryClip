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
fs.writeFileSync(path.join(downloads,'SHA256SUMS.txt'), 'a958e6480b3f466895fdfe2e9918ab6ff509cfed137ff03fc1ecd13e4fd7681f  FerryClip.exe\nd414436bda8a0842e3db79bc5ca3c7a896e019c338fdeca3982f94d6dd36096c  FerryClip-beta-0.8.11.apk\n');
console.log('Built static website with FerryClip 0.8.11 release links and checksums.');
