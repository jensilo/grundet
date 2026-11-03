#!/usr/bin/env node

import { spawn, execSync } from "node:child_process";
import puppeteer from "puppeteer-core";

const useProfile = process.argv[2] === "--profile";

if (process.argv[2] && process.argv[2] !== "--profile") {
	console.log("Usage: browser-start.js [--profile]");
	console.log("\nOptions:");
	console.log("  --profile  Copy your default Chrome profile (cookies, logins)");
	process.exit(1);
}

const SCRAPING_DIR = `${process.env.HOME}/.cache/browser-tools`;

const BROWSERS = [
	{
		name: "Chrome",
		executable: "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
		profileDir: `${process.env.HOME}/Library/Application Support/Google/Chrome/`,
	},
	{
		name: "Brave",
		executable: "/Applications/Brave Browser.app/Contents/MacOS/Brave Browser",
		profileDir: `${process.env.HOME}/Library/Application Support/BraveSoftware/Brave-Browser/`,
	},
	{
		name: "Microsoft Edge",
		executable: "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge",
		profileDir: `${process.env.HOME}/Library/Application Support/Microsoft Edge/`,
	},
	{
		name: "Chromium",
		executable: "/Applications/Chromium.app/Contents/MacOS/Chromium",
		profileDir: `${process.env.HOME}/Library/Application Support/Chromium/`,
	},
];

const browserConfig = BROWSERS.find((browser) => {
	try {
		execSync(`test -x "${browser.executable}"`, { stdio: "ignore" });
		return true;
	} catch {
		return false;
	}
});

if (!browserConfig) {
	console.error("✗ No supported Chromium browser found. Install Chrome, Brave, Edge, or Chromium.");
	process.exit(1);
}

// Check if already running on :9222
try {
	const browser = await puppeteer.connect({
		browserURL: "http://localhost:9222",
		defaultViewport: null,
	});
	await browser.disconnect();
	console.log("✓ Chromium browser already running on :9222");
	process.exit(0);
} catch {}

// Setup profile directory
execSync(`mkdir -p "${SCRAPING_DIR}"`, { stdio: "ignore" });

// Remove SingletonLock to allow new instance
try {
	execSync(`rm -f "${SCRAPING_DIR}/SingletonLock" "${SCRAPING_DIR}/SingletonSocket" "${SCRAPING_DIR}/SingletonCookie"`, { stdio: "ignore" });
} catch {}

if (useProfile) {
	console.log("Syncing profile...");
	execSync(
		`rsync -a --delete \
			--exclude='SingletonLock' \
			--exclude='SingletonSocket' \
			--exclude='SingletonCookie' \
			--exclude='*/Sessions/*' \
			--exclude='*/Current Session' \
			--exclude='*/Current Tabs' \
			--exclude='*/Last Session' \
			--exclude='*/Last Tabs' \
			"${browserConfig.profileDir}" "${SCRAPING_DIR}/"`,
		{ stdio: "pipe" },
	);
}

// Start browser with flags to force new instance
spawn(
	browserConfig.executable,
	[
		"--remote-debugging-port=9222",
		`--user-data-dir=${SCRAPING_DIR}`,
		"--no-first-run",
		"--no-default-browser-check",
	],
	{ detached: true, stdio: "ignore" },
).unref();

// Wait for Chrome to be ready
let connected = false;
for (let i = 0; i < 30; i++) {
	try {
		const browser = await puppeteer.connect({
			browserURL: "http://localhost:9222",
			defaultViewport: null,
		});
		await browser.disconnect();
		connected = true;
		break;
	} catch {
		await new Promise((r) => setTimeout(r, 500));
	}
}

if (!connected) {
	console.error(`✗ Failed to connect to ${browserConfig.name}`);
	process.exit(1);
}

console.log(`✓ ${browserConfig.name} started on :9222${useProfile ? " with your profile" : ""}`);
