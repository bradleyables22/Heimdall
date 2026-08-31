import assert from "node:assert/strict";
import fs from "node:fs";
import os from "node:os";
import path from "node:path";
import {
  assertLocalTimeMatrix,
  appProject,
  expectedLocalTimeFormats,
  openHarness,
  repoRoot,
  run,
  waitForActionAfter,
  waitForText,
  withStaticFileServer
} from "../helpers/e2e-support.mjs";

async function testStaticSiteGeneration(page) {
  const outputPath = fs.mkdtempSync(path.join(os.tmpdir(), "heimdall-e2e-static-"));
  const stalePath = path.join(outputPath, "stale.txt");
  fs.writeFileSync(stalePath, "stale");

  try {
    const stdout = run("dotnet", [
      "run",
      "--no-build",
      "--no-launch-profile",
      "--project",
      appProject,
      "--",
      "--heimdall-generate-static"
    ], {
      env: {
        ...process.env,
        ASPNETCORE_ENVIRONMENT: "Development",
        DOTNET_ENVIRONMENT: "Development",
        HEIMDALL_E2E_STATIC_OUTPUT: outputPath
      }
    });

    assert.match(stdout, /Generated 5 page\(s\) and \d+ asset\(s\)\./);
    assert.equal(fs.existsSync(stalePath), false, "CleanOutputPath should remove stale output files.");

    const indexPath = path.join(outputPath, "index.html");
    const e2ePath = path.join(outputPath, "e2e", "index.html");
    const docsPath = path.join(outputPath, "docs", "start", "index.html");
    const feedPath = path.join(outputPath, "feed.xml");
    const notFoundPath = path.join(outputPath, "404.html");
    const sitemapPath = path.join(outputPath, "sitemap.xml");
    const robotsPath = path.join(outputPath, "robots.txt");
    const manifestPath = path.join(outputPath, "heimdall.static.manifest.json");
    const appCssPath = path.join(outputPath, "css", "app.css");
    const heimdallBundlePath = path.join(outputPath, "_content", "HeimdallFramework.Web", "heimdall-bundle.min.js");

    for (const filePath of [
      indexPath,
      e2ePath,
      docsPath,
      feedPath,
      notFoundPath,
      sitemapPath,
      robotsPath,
      manifestPath,
      appCssPath,
      heimdallBundlePath
    ]) {
      assert.equal(fs.existsSync(filePath), true, `${filePath} should exist.`);
    }

    const indexHtml = fs.readFileSync(indexPath, "utf8");
    assert.match(indexHtml, /id="static-page"/);
    assert.match(indexHtml, /data-route="\/"/);
    assert.match(indexHtml, /href="\/e2e-static\/css\/app\.css"/);
    assert.match(indexHtml, /src="\/e2e-static\/_content\/HeimdallFramework\.Web\/heimdall-bundle\.min\.js"/);

    const docsHtml = fs.readFileSync(docsPath, "utf8");
    assert.match(docsHtml, /Static Docs/);
    assert.match(docsHtml, /data-route="\/docs\/start"/);

    assert.match(fs.readFileSync(feedPath, "utf8"), /<feed><title>Heimdall E2E<\/title><\/feed>/);
    assert.match(fs.readFileSync(notFoundPath, "utf8"), /data-kind="not-found"/);

    const sitemap = fs.readFileSync(sitemapPath, "utf8");
    assert.match(sitemap, /https:\/\/heimdall-e2e\.example\/e2e-static\//);
    assert.match(sitemap, /https:\/\/heimdall-e2e\.example\/e2e-static\/docs\/start\//);
    assert.doesNotMatch(sitemap, /feed\.xml/);
    assert.doesNotMatch(sitemap, /404\.html/);

    const robots = fs.readFileSync(robotsPath, "utf8");
    assert.match(robots, /Allow: \/e2e-static\//);
    assert.match(robots, /Sitemap: https:\/\/heimdall-e2e\.example\/e2e-static\/sitemap\.xml/);

    const manifest = JSON.parse(fs.readFileSync(manifestPath, "utf8"));
    assert.equal(manifest.schemaVersion, 1);
    assert.equal(manifest.manifestRelativePath, "heimdall.static.manifest.json");

    const pageRoutes = manifest.files
      .filter(file => file.kind === "page")
      .map(file => file.route)
      .sort();
    assert.deepEqual(pageRoutes, ["/", "/404.html", "/docs/start", "/e2e", "/feed.xml"]);

    const relativePaths = manifest.files.map(file => file.relativePath);
    assert.ok(relativePaths.includes("css/app.css"));
    assert.ok(relativePaths.includes("_content/HeimdallFramework.Web/heimdall-bundle.min.js"));
    assert.ok(relativePaths.includes("sitemap.xml"));
    assert.ok(relativePaths.includes("robots.txt"));

    await withStaticFileServer(outputPath, "/e2e-static", async origin => {
      await page.goto(`${origin}/e2e-static/`);
      await page.locator("#static-page").waitFor();
      assert.equal(await page.locator("#static-page").getAttribute("data-route"), "/");
      assert.equal(await page.locator("#static-page").getAttribute("data-path-base"), "/e2e-static");
      assert.equal(await page.locator("#static-docs-link").getAttribute("href"), "/e2e-static/docs/start/");

      await page.goto(`${origin}/e2e-static/docs/start/`);
      await page.locator("#static-page").waitFor();
      assert.equal(await page.locator("#static-page").getAttribute("data-route"), "/docs/start");
      await page.getByRole("heading", { name: "Static Docs" }).waitFor();

      const bundleResponse = await page.request.get(`${origin}/e2e-static/_content/HeimdallFramework.Web/heimdall-bundle.min.js`);
      assert.equal(bundleResponse.ok(), true);
    });
  } finally {
    fs.rmSync(outputPath, { recursive: true, force: true });
  }
}

export const tests = [
  ["generates and serves static site output", testStaticSiteGeneration]
];
