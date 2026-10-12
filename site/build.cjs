// Static website only. Binaries are downloaded directly from the published GitHub release.
const fs = require('node:fs');
const path = require('node:path');
const root = path.resolve(__dirname, '..');
const destination = path.join(root, 'dist/site');
fs.mkdirSync(destination, {recursive:true});
for (const file of ['index.html','docs.html','style.css','app.js','FerryClip-logo.png']) fs.copyFileSync(path.join(__dirname,file),path.join(destination,file));
fs.cpSync(path.join(__dirname,'images'),path.join(destination,'images'),{recursive:true});
const downloads = path.join(destination,'downloads');
fs.mkdirSync(downloads,{recursive:true});
fs.writeFileSync(path.join(downloads,'SHA256SUMS.txt'), 'c711dd76caeca148ec25b855068b7feb48fdf690d74e24a55ffdf96bc8ac9f2f  FerryClip-0.8.11-win-x64.exe\nd414436bda8a0842e3db79bc5ca3c7a896e019c338fdeca3982f94d6dd36096c  FerryClip-beta-0.8.11.apk\n');
console.log('Built static website with FerryClip 0.8.11 release links and checksums.');
