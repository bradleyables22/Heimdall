import * as esbuild from "esbuild";
import { existsSync } from "node:fs";
import { readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const projectRoot = path.dirname(fileURLToPath(import.meta.url));
const sourceDir = path.join(projectRoot, "_build", "heimdall");
const staticAssetRoot = path.join(projectRoot, "wwwroot");
const outputs = [
  {
    label: "readable",
    minify: false,
    path: path.join(staticAssetRoot, "heimdall-bundle.js")
  },
  {
    label: "minified",
    minify: true,
    path: path.join(staticAssetRoot, "heimdall-bundle.min.js")
  }
];
const entryFile = "heimdall.entry.js";

const verify = process.argv.includes("--verify");

function bytesEqual(a, b) {
  if (a.byteLength !== b.byteLength) {
    return false;
  }

  for (let i = 0; i < a.byteLength; i += 1) {
    if (a[i] !== b[i]) {
      return false;
    }
  }

  return true;
}

async function readRuntimeSource() {
  const entryPath = path.join(sourceDir, entryFile);
  if (!existsSync(entryPath)) {
    throw new Error(`Missing Heimdall entrypoint: ${entryPath}`);
  }

  return readFile(entryPath, "utf8");
}

async function buildBundle(source, { minify }) {
  const result = await esbuild.build({
    absWorkingDir: sourceDir,
    bundle: true,
    charset: "utf8",
    format: "iife",
    legalComments: "inline",
    minify,
    stdin: {
      contents: source,
      loader: "js",
      resolveDir: sourceDir,
      sourcefile: entryFile
    },
    target: "es2020",
    write: false
  });

  return result.outputFiles[0].contents;
}

const source = await readRuntimeSource();
const bundles = await Promise.all(outputs.map(async output => ({
  ...output,
  bytes: await buildBundle(source, output)
})));

if (verify) {
  for (const bundle of bundles) {
    if (!existsSync(bundle.path)) {
      throw new Error(`Cannot verify Heimdall ${bundle.label} bundle because output does not exist: ${bundle.path}`);
    }

    const currentBundleBytes = await readFile(bundle.path);
    if (!bytesEqual(currentBundleBytes, bundle.bytes)) {
      throw new Error(`Heimdall ${bundle.label} bundle is out of date: ${bundle.path}`);
    }
  }

  console.log("Heimdall verified: bundles are up to date.");
} else {
  for (const bundle of bundles) {
    await writeFile(bundle.path, bundle.bytes);
    console.log(`Heimdall ${bundle.label} bundle built: ${bundle.path}`);
  }
}
