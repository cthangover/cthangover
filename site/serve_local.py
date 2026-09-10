"""Local development server for the site with SPA fallback.

Serves files from this directory, and returns index.html for any path
that does not match a real file, so history-API routes such as
/docs/<slug> and /quickstart/<lang>/<page> work locally the same way
they do behind nginx in production.

Usage:
    python serve_local.py [port]

Default port: 8081. Binds to 127.0.0.1 only.
"""

import http.server
import os
import sys

ROOT = os.path.dirname(os.path.abspath(__file__))
PORT = int(sys.argv[1]) if len(sys.argv) > 1 else 8081


class SpaHandler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=ROOT, **kwargs)

    def send_head(self):
        path = self.translate_path(self.path)
        has_index = os.path.isdir(path) and os.path.exists(
            os.path.join(path, "index.html")
        )
        if not os.path.exists(path) or (os.path.isdir(path) and not has_index):
            self.path = "/index.html"
        return super().send_head()

    def log_message(self, fmt, *args):
        sys.stderr.write("%s - %s\n" % (self.address_string(), fmt % args))


if __name__ == "__main__":
    server = http.server.ThreadingHTTPServer(("127.0.0.1", PORT), SpaHandler)
    print(f"Serving site on http://127.0.0.1:{PORT}/  (Ctrl+C to stop)")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        pass
