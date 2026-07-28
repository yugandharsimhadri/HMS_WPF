// Renders docs/USER_GUIDE.md to docs/USER_GUIDE.pdf.
//
// The temporary HTML is written next to the markdown so the relative image
// paths in the guide resolve without rewriting them.
//
// Usage: node guide_to_pdf.mjs [input.md] [output.pdf]

import fs from 'node:fs';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { marked } from 'marked';
import puppeteer from 'puppeteer-core';

const docs = path.resolve(import.meta.dirname, '..', '..', 'docs');
const inputPath = process.argv[2] || path.join(docs, 'USER_GUIDE.md');
const outputPath = process.argv[3] || path.join(docs, 'USER_GUIDE.pdf');

const BROWSERS = [
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Google/Chrome/Application/chrome.exe',
  'C:/Program Files/Microsoft/Edge/Application/msedge.exe',
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
  '/usr/bin/google-chrome',
  '/usr/bin/chromium-browser',
];

function findBrowser() {
  if (process.env.CHROME_PATH) return process.env.CHROME_PATH;

  const found = BROWSERS.find((p) => fs.existsSync(p));
  if (!found) {
    console.error('No Chrome or Edge found. Set CHROME_PATH to the executable.');
    process.exit(1);
  }
  return found;
}

marked.use({ gfm: true, breaks: false });
const source = fs.readFileSync(inputPath, 'utf-8');
const body = marked.parse(source);

// The document names itself. This renders more than the user guide now, and
// footing every PDF "user guide" mislabels the ones that are not.
const heading = source.match(/^#\s+(.+)$/m);
const title = heading ? heading[1].trim() : path.basename(inputPath, '.md');

// Matches the application's own palette so the printed guide and the screen
// look like the same product.
const html = `<!doctype html>
<html><head><meta charset="utf-8"><title>${title}</title>
<style>
  @page { margin: 18mm 15mm; }
  body {
    font-family: 'Segoe UI', Calibri, Arial, sans-serif;
    font-size: 10.5pt; line-height: 1.55; color: #1B2733;
    max-width: 940px; margin: 0 auto;
  }
  h1 { font-size: 21pt; color: #0B5A54; border-bottom: 3px solid #0F766E; padding-bottom: 8px; margin: 0 0 6px; }
  h2 { font-size: 15pt; color: #0F766E; border-bottom: 1px solid #DCE1E7; padding-bottom: 4px;
       margin-top: 26px; page-break-after: avoid; }
  h3 { font-size: 12pt; color: #1B2733; margin-top: 18px; page-break-after: avoid; }
  p, li { orphans: 3; widows: 3; }
  blockquote {
    border-left: 4px solid #0F766E; margin: 12px 0; padding: 8px 14px;
    background: #E4EFED; font-size: 10pt; page-break-inside: avoid;
  }
  blockquote p { margin: 4px 0; }
  table { border-collapse: collapse; width: 100%; margin: 10px 0 18px; font-size: 9.5pt; }
  tr { page-break-inside: avoid; }
  th, td { border: 1px solid #DCE1E7; padding: 6px 9px; text-align: left; vertical-align: top; }
  th { background: #EEF2F5; font-weight: 600; color: #0B5A54; }
  tr:nth-child(even) td { background: #FAFBFC; }
  code { background: #F1F4F7; padding: 1px 5px; border-radius: 3px;
         font-family: Consolas, 'Courier New', monospace; font-size: 9pt; }
  pre { background: #F6F8FA; border: 1px solid #DCE1E7; border-radius: 5px; padding: 10px 12px;
        font-size: 8.5pt; page-break-inside: avoid; }
  pre code { background: none; padding: 0; }
  /* Screenshots are wider than the page; keep them whole and never split. */
  img { max-width: 100%; height: auto; display: block; margin: 12px auto 18px;
        border: 1px solid #DCE1E7; border-radius: 6px; page-break-inside: avoid; }
  hr { border: none; border-top: 1px solid #DCE1E7; margin: 22px 0; }
  a { color: #0F766E; text-decoration: none; }
  h2 + p > img, h3 + p > img { margin-top: 8px; }
</style></head>
<body>${body}</body></html>`;

const htmlPath = path.join(path.dirname(inputPath), '.user-guide.tmp.html');
fs.writeFileSync(htmlPath, html, 'utf-8');

const browser = await puppeteer.launch({
  executablePath: findBrowser(),
  headless: true,
  args: ['--no-sandbox', '--disable-setuid-sandbox', '--disable-gpu'],
});

try {
  const page = await browser.newPage();
  await page.goto(pathToFileURL(htmlPath).href, { waitUntil: 'networkidle0' });

  await page.pdf({
    path: outputPath,
    format: 'A4',
    printBackground: true,
    displayHeaderFooter: true,
    headerTemplate: '<div></div>',
    footerTemplate:
      '<div style="width:100%;font-size:8pt;color:#61707E;padding:0 15mm;' +
      'display:flex;justify-content:space-between;">' +
      `<span>${title.replace(/[<>&]/g, '')}</span>` +
      '<span class="pageNumber"></span></div>',
    margin: { top: '18mm', bottom: '16mm', left: '15mm', right: '15mm' },
  });
} finally {
  await browser.close();
  if (!process.env.KEEP_HTML) fs.unlinkSync(htmlPath);
}

const kb = Math.round(fs.statSync(outputPath).size / 1024);
console.log(`Wrote ${outputPath} (${kb} KB)`);
