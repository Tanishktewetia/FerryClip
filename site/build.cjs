// Static website only. Binaries are downloaded directly from the published GitHub release.
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '..');
const destination = path.join(root, 'dist/site');
fs.mkdirSync(destination, {recursive:true});
for (const file of ['index.html','docs.html','style.css','app.js','FerryClip-logo.png']) fs.copyFileSync(path.join(__dirname,file),path.join(destination,file));
const downloads = path.join(destination,'downloads');
fs.mkdirSync(downloads,{recursive:true});
fs.writeFileSync(path.join(downloads,'SHA256SUMS.txt'), '1a65f0fd655463568cd4d01f63c1cf4851d4e31affc94a30163036026484dd8f  FerryClip-beta-0.8.9.apk\n6079e1ad111417c66558501cc79cb79bc5bb26c5795ac3299ec3c102dc7850ca  FerryClip-0.8.9-win-x64.exe\n');
console.log('Built static website with direct 0.8.9 APK/EXE download links.');
