// Production-build launcher: pins cwd to the frontend dir (so Tailwind/PostCSS config
// resolve) and runs `next build`. Mirrors _start.js so builds work regardless of the
// shell's current directory.
const path = require('path');
process.chdir(__dirname);
const nextBin = path.join(__dirname, 'node_modules', 'next', 'dist', 'bin', 'next');
process.argv = [process.argv[0], nextBin, 'build'];
require(nextBin);
