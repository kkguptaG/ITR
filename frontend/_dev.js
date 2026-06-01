// Dev-server launcher: pins process.cwd() to the frontend dir so PostCSS/Tailwind
// resolve tailwind.config.ts + postcss.config.js (the preview launcher otherwise runs
// from the repo root, where there is no Tailwind config → "border-ink-200 does not exist").
const path = require('path');
process.chdir(__dirname);
const nextBin = path.join(__dirname, 'node_modules', 'next', 'dist', 'bin', 'next');
process.argv = [process.argv[0], nextBin, 'dev', '-p', '3000'];
require(nextBin);
