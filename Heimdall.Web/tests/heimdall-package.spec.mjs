import assert from "node:assert/strict";
import { mkdtempSync, rmSync, readdirSync, statSync } from "node:fs";
import os from "node:os";
import path from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const projectRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const projectFile = path.join(projectRoot, "Heimdall.Web.csproj");

function run(command, args, options = {}) {
  const result = spawnSync(command, args, {
    cwd: projectRoot,
    encoding: "utf8",
    shell: false,
    ...options
  });

  if (result.status !== 0) {
    throw new Error([
      `${command} ${args.join(" ")} failed with exit code ${result.status}.`,
      result.stdout || "",
      result.stderr || ""
    ].join("\n"));
  }

  return result.stdout || "";
}

function listPackageEntries(nupkgPath) {
  return run("tar", ["-tf", nupkgPath])
    .split(/\r?\n/)
    .map(line => line.trim())
    .filter(Boolean);
}

function listFilesRecursive(root) {
  const files = [];

  function walk(dir) {
    for (const entry of readdirSync(dir)) {
      const fullPath = path.join(dir, entry);
      const stat = statSync(fullPath);
      if (stat.isDirectory()) {
        walk(fullPath);
      } else {
        files.push(path.relative(root, fullPath).replace(/\\/g, "/"));
      }
    }
  }

  walk(root);
  return files.sort();
}

function assertNoInternalFiles(entries, label) {
  const forbidden = entries.filter(entry =>
    entry.startsWith("_build/") ||
    entry.startsWith("src/") ||
    entry.includes("/_build/") ||
    entry.includes("/src/") ||
    entry === "build.mjs" ||
    entry === "build-heimdall.ps1" ||
    entry === "package.json" ||
    entry === "package-lock.json" ||
    entry === "heimdall.entry.js" ||
    entry.startsWith("tests/") ||
    entry.startsWith("node_modules/") ||
    /^core\/.+\.js$/.test(entry)
  );

  assert.deepEqual(forbidden, [], `${label} should not include internal build/test/source files.`);
}

const tempRoot = mkdtempSync(path.join(os.tmpdir(), "heimdall-package-"));
const packDir = path.join(tempRoot, "pack");
const publishDir = path.join(tempRoot, "publish");

try {
  run("dotnet", ["pack", projectFile, "--no-restore", "-o", packDir], { stdio: "pipe" });
  run("dotnet", ["publish", projectFile, "--no-restore", "-o", publishDir], { stdio: "pipe" });

  const packageFile = readdirSync(packDir).find(entry =>
    /^HeimdallFramework\.Web\.\d+\.\d+\.\d+\.nupkg$/.test(entry)
  );
  assert.ok(packageFile, "Expected package output to exist.");
  const nupkgPath = path.join(packDir, packageFile);

  const packageEntries = listPackageEntries(nupkgPath);
  assert.ok(packageEntries.includes("staticwebassets/heimdall-bundle.js"), "Package should contain generated runtime.");
  assert.ok(packageEntries.includes("staticwebassets/heimdall-bundle.min.js"), "Package should contain minified generated runtime.");
  assertNoInternalFiles(packageEntries, "Package");

  const publishEntries = listFilesRecursive(publishDir);
  assert.ok(publishEntries.includes("wwwroot/heimdall-bundle.js"), "Publish output should contain generated runtime.");
  assert.ok(publishEntries.includes("wwwroot/heimdall-bundle.min.js"), "Publish output should contain minified generated runtime.");
  assertNoInternalFiles(publishEntries, "Publish output");

  console.log("Heimdall package shape tests passed.");
} finally {
  rmSync(tempRoot, { recursive: true, force: true });
}
