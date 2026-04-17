import fs from "node:fs";
import path from "node:path";
import { spawnSync } from "node:child_process";

const [artifactDirectoryArg, nugetSource] = process.argv.slice(2);

if (!artifactDirectoryArg || !nugetSource) {
  console.error("Usage: node scripts/publish-packages.mjs <artifact-directory> <nuget-source>");
  process.exit(1);
}

const artifactDirectory = path.resolve(artifactDirectoryArg);

if (!fs.existsSync(artifactDirectory)) {
  throw new Error("No packages found. Run `just pack` first.");
}

const packages = fs
  .readdirSync(artifactDirectory)
  .filter((entry) => entry.endsWith(".nupkg"))
  .sort((left, right) => left.localeCompare(right));

if (packages.length === 0) {
  throw new Error("No packages found. Run `just pack` first.");
}

const configFile = process.env.NUGET_CONFIG_FILE;
const apiKey = process.env.NUGET_API_KEY;

for (const packageName of packages) {
  const packagePath = path.join(artifactDirectory, packageName);
  const args = ["nuget", "push", packagePath, "--source", nugetSource, "--skip-duplicate"];

  if (configFile) {
    args.push("--configfile", configFile);
  }

  if (apiKey) {
    args.push("--api-key", apiKey);
  }

  console.log(`==> Publishing ${packageName} to ${nugetSource}`);

  const result = spawnSync("dotnet", args, { stdio: "inherit" });

  if (result.error) {
    throw result.error;
  }

  if (result.status !== 0) {
    process.exit(result.status ?? 1);
  }
}
