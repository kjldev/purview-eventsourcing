import fs from "node:fs";
import path from "node:path";

const [outputDirectoryArg, releaseTag] = process.argv.slice(2);

if (!outputDirectoryArg || !releaseTag) {
  console.error("Usage: node scripts/download-release-packages.mjs <output-directory> <release-tag>");
  process.exit(1);
}

const repository = process.env.GITHUB_REPOSITORY;
const token = process.env.GITHUB_TOKEN;

if (!repository || !token) {
  console.error("GITHUB_REPOSITORY and GITHUB_TOKEN are required.");
  process.exit(1);
}

const outputDirectory = path.resolve(outputDirectoryArg);

const jsonHeaders = {
  Authorization: `Bearer ${token}`,
  Accept: "application/vnd.github+json",
  "X-GitHub-Api-Version": "2022-11-28",
  "User-Agent": "purview-eventsourcing-cd",
};

const binaryHeaders = {
  ...jsonHeaders,
  Accept: "application/octet-stream",
};

const releaseResponse = await fetch(
  `https://api.github.com/repos/${repository}/releases/tags/${encodeURIComponent(releaseTag)}`,
  { headers: jsonHeaders },
);

if (!releaseResponse.ok) {
  throw new Error(`Failed to load release ${releaseTag}: ${releaseResponse.status} ${releaseResponse.statusText}`);
}

const release = await releaseResponse.json();
const packageAssets = release.assets
  .filter((asset) => asset.name.endsWith(".nupkg"))
  .sort((left, right) => left.name.localeCompare(right.name));

if (packageAssets.length === 0) {
  throw new Error(`Release ${releaseTag} does not contain any .nupkg assets.`);
}

fs.rmSync(outputDirectory, { recursive: true, force: true });
fs.mkdirSync(outputDirectory, { recursive: true });

for (const asset of packageAssets) {
  const assetResponse = await fetch(asset.url, { headers: binaryHeaders });

  if (!assetResponse.ok) {
    throw new Error(`Failed to download asset ${asset.name}: ${assetResponse.status} ${assetResponse.statusText}`);
  }

  const packagePath = path.join(outputDirectory, asset.name);
  const packageBytes = Buffer.from(await assetResponse.arrayBuffer());
  fs.writeFileSync(packagePath, packageBytes);
  console.log(`Downloaded ${asset.name}`);
}
