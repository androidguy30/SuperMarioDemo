#!/usr/bin/env node
/**
 * Build script to generate .env file from Vercel environment variables
 * and update the fetch path in index.html to work with Vercel deployment
 * 
 * Since outputDirectory is "WebDemo", the .env file needs to be in WebDemo
 * and the fetch path needs to be './.env' instead of '../.env'
 */

const fs = require('fs');
const path = require('path');

// Read environment variables (Vercel injects these during build)
const DRILL_API_KEY = process.env.DRILL_API_KEY || '';
const DRILL_API_URL = process.env.DRILL_API_URL || 'http://localhost:3000';

// Generate .env file content
const envContent = `# Auto-generated at build time from Vercel environment variables
# DO NOT EDIT MANUALLY - this file is overwritten during build

DRILL_API_KEY=${DRILL_API_KEY}
DRILL_API_URL=${DRILL_API_URL}
`;

// Generate .env in WebDemo directory (output directory)
const webDemoEnvPath = path.join(__dirname, 'WebDemo', '.env');
fs.writeFileSync(webDemoEnvPath, envContent, 'utf8');
console.log(`✓ Generated .env file at ${webDemoEnvPath}`);

// Also generate in root for local development (keeps ../.env working locally)
const rootEnvPath = path.join(__dirname, '.env');
fs.writeFileSync(rootEnvPath, envContent, 'utf8');
console.log(`✓ Also generated .env in root for local dev`);

// Update index.html to use './.env' instead of '../.env' for Vercel deployment
const indexPath = path.join(__dirname, 'WebDemo', 'index.html');
let indexContent = fs.readFileSync(indexPath, 'utf8');
// Only change if we're in a Vercel build (has DRILL_API_KEY set)
if (DRILL_API_KEY) {
  indexContent = indexContent.replace(
    /fetch\(['"]\.\.\/\.env['"]\)/g,
    "fetch('./.env')"
  );
  fs.writeFileSync(indexPath, indexContent, 'utf8');
  console.log(`✓ Updated fetch path in index.html for Vercel deployment`);
}

console.log(`  DRILL_API_KEY: ${DRILL_API_KEY ? '***' + DRILL_API_KEY.slice(-4) : '(not set)'}`);
console.log(`  DRILL_API_URL: ${DRILL_API_URL}`);

