// Production-server launcher: pins cwd to the frontend dir (so Tailwind/PostCSS config
// resolve) and runs `next start`. Serves the built .next without the dev HMR websocket,
// so headless screenshots reach network-idle reliably.
const path = require('path');
process.chdir(__dirname);
const nextBin = path.join(__dirname, 'node_modules', 'next', 'dist', 'bin', 'next');
process.argv = [process.argv[0], nextBin, 'start', '-p', '3000'];
require(nextBin);
