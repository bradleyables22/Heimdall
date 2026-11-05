import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { chromium } from "../Heimdall.Web/node_modules/playwright/index.mjs";

const folder = path.dirname(fileURLToPath(import.meta.url));
const label = process.argv[2] || "before";
const cssPath = process.argv[3];
const browser = await chromium.launch({ headless: true });
try {
  const page = await browser.newPage();
  if (cssPath) {
    await page.route("**/css/app.css", route => route.fulfill({
      contentType: "text/css",
      body: fs.readFileSync(cssPath, "utf8")
    }));
  }
  for (const [name, width, height] of [["desktop", 1440, 1000], ["tablet", 900, 1100], ["mobile", 390, 1200], ["small-mobile", 320, 1200]]) {
    await page.setViewportSize({ width, height });
    await page.goto("http://127.0.0.1:51234/", { waitUntil: "networkidle" });
    const details = await page.locator(".landing-hero-copy").evaluate(copy => {
      const buttons = copy.querySelector(".landing-invocations, .landing-actions");
      const cards = copy.querySelector(".landing-proof-row");
      const box = el => { const r = el.getBoundingClientRect(); return { x: r.x, y: r.y, width: r.width, height: r.height, right: r.right, bottom: r.bottom }; };
      return {
        buttonsClass: buttons.className,
        display: getComputedStyle(buttons).display,
        gap: getComputedStyle(buttons).gap,
        marginBottom: getComputedStyle(buttons).marginBottom,
        buttonBoxes: [...buttons.children].map(el => ({ text: el.textContent.trim(), ...box(el) })),
        cardGap: getComputedStyle(cards).gap,
        cardTopSpacing: cards.getBoundingClientRect().top - buttons.getBoundingClientRect().bottom,
        cardBoxes: [...cards.children].map(box),
        overflow: document.documentElement.scrollWidth > window.innerWidth
      };
    });
    console.log(JSON.stringify({ label, name, width, ...details }));
    await page.locator(".landing-hero").screenshot({ path: path.join(folder, `docs-landing-${label}-${name}.png`) });
  }
} finally {
  await browser.close();
}
