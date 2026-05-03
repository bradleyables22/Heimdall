import * as esbuild from "esbuild";
import { existsSync } from "node:fs";
import { readFile, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const projectRoot = path.dirname(fileURLToPath(import.meta.url));
const sourceDir = path.join(projectRoot, "src", "heimdall");
const staticAssetRoot = path.join(projectRoot, "wwwroot");
const referencePath = path.join(staticAssetRoot, "heimdall.js");
const outputPath = path.join(staticAssetRoot, "heimdall-bundle.js");

const fragments = [
  "00-shell-and-constants.js",
  "10-utils-and-payloads.js",
  "20-dom-swaps-and-directives.js",
  "30-security-tokens.js",
  "40-actions-and-invocation.js",
  "50-boot-triggers.js",
  "60-sse-bifrost.js",
  "70-event-delegates.js",
  "80-public-api-and-startup.js"
];

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

async function readFragments() {
  const buffers = [];

  for (const fragment of fragments) {
    const fragmentPath = path.join(sourceDir, fragment);
    if (!existsSync(fragmentPath)) {
      throw new Error(`Missing Heimdall fragment: ${fragmentPath}`);
    }

    buffers.push(await readFile(fragmentPath));
  }

  return Buffer.concat(buffers);
}

async function buildBundle(sourceBytes) {
  const source = sourceBytes.toString("utf8");

  const result = await esbuild.transform(source, {
    charset: "utf8",
    legalComments: "inline",
    loader: "js",
    minify: false,
    sourcefile: "heimdall.js",
    target: "es2020"
  });

  return Buffer.from(result.code, "utf8");
}

const sourceBytes = await readFragments();
const bundleBytes = await buildBundle(sourceBytes);

if (verify) {
  if (!existsSync(referencePath)) {
    throw new Error(`Cannot verify Heimdall source because reference does not exist: ${referencePath}`);
  }

  const referenceBytes = await readFile(referencePath);
  if (!bytesEqual(referenceBytes, sourceBytes)) {
    throw new Error(`Heimdall source fragments differ from source-of-truth reference: ${referencePath}`);
  }

  if (!existsSync(outputPath)) {
    throw new Error(`Cannot verify Heimdall bundle because output does not exist: ${outputPath}`);
  }

  const currentBundleBytes = await readFile(outputPath);
  if (!bytesEqual(currentBundleBytes, bundleBytes)) {
    throw new Error(`Heimdall bundle is out of date: ${outputPath}`);
  }

  console.log("Heimdall verified: source matches heimdall.js and bundle is up to date.");
} else {
  await writeFile(outputPath, bundleBytes);
  console.log(`Heimdall bundle built: ${outputPath}`);
}
