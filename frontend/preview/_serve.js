// Minimal Node-6-compatible static server for the design previews.
// Usage: node _serve.js [port] [defaultFile]
var http = require('http'), fs = require('fs'), path = require('path');
var ROOT = __dirname;            // frontend/preview
var PORT = parseInt(process.argv[2], 10) || 8137;
var DEFAULT_FILE = process.argv[3] || 'dashboard-preview.html';
var TYPES = { '.html': 'text/html; charset=utf-8', '.css': 'text/css', '.js': 'application/javascript', '.svg': 'image/svg+xml', '.png': 'image/png', '.json': 'application/json' };
http.createServer(function (req, res) {
  var url = req.url.split('?')[0];
  if (url === '/' || url === '') url = '/' + DEFAULT_FILE;
  var file = path.join(ROOT, url.replace(/^\/+/, ''));
  if (file.indexOf(ROOT) !== 0) { res.writeHead(403); return res.end('forbidden'); }
  fs.readFile(file, function (err, data) {
    if (err) { res.writeHead(404); return res.end('not found'); }
    res.writeHead(200, { 'Content-Type': TYPES[path.extname(file).toLowerCase()] || 'application/octet-stream' });
    res.end(data);
  });
}).listen(PORT, function () { console.log('preview server on http://localhost:' + PORT + ' (default ' + DEFAULT_FILE + ')'); });
