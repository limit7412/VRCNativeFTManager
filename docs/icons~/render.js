// docs/icons~/src の SVG を Runtime/Icons の PNG（256x256、透過背景）へ書き出す。
// 使い方: リポジトリのルートで `node docs/icons~/render.js`。Playwright の Chromium を使う
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

const srcDir = path.join(__dirname, 'src');
const outDir = path.join(__dirname, '..', '..', 'Runtime', 'Icons');

(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 256, height: 256 } });
  fs.mkdirSync(outDir, { recursive: true });
  for (const file of fs.readdirSync(srcDir).filter((name) => name.endsWith('.svg')).sort()) {
    const svg = fs.readFileSync(path.join(srcDir, file), 'utf8');
    await page.setContent(`<html><body style="margin:0;background:transparent">${svg}</body></html>`);
    const element = await page.$('svg');
    const output = path.join(outDir, file.replace(/\.svg$/, '.png'));
    await element.screenshot({ path: output, omitBackground: true });
    console.log(path.relative(process.cwd(), output));
  }
  await browser.close();
})();
