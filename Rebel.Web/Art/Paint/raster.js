// node raster.js in.svg... -> same name .png at 1000x1300, transparent
const { chromium } = require(require('child_process').execSync('npm root -g').toString().trim() + '/playwright');
const fs = require("fs");
(async () => {
  const browser = await chromium.launch();
  const page = await browser.newPage({ viewport: { width: 400, height: 520 }, deviceScaleFactor: 2.5 });
  for (const f of process.argv.slice(2)) {
    await page.setContent(`<!doctype html><html><body style="margin:0;background:transparent">${fs.readFileSync(f, "utf8")}</body></html>`);
    await page.waitForTimeout(100);
    await page.screenshot({ path: f.replace(/\.svg$/, ".png"), omitBackground: true, clip: { x: 0, y: 0, width: 400, height: 520 } });
  }
  await browser.close();
})();
