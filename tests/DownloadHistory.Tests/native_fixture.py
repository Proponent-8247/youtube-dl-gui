"""Offline provider adapter: real yt-dlp CLI parsing, extraction and archive writes.

Only the network/extractor is a deterministic fixture. No YouTube requests are made.
The .NET test executable proxies this script to exercise the application's actual
process-launch, locking, configuration and reconciliation code.
"""
import io
import json
import os
import sys
import threading
import wave
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

import yt_dlp
from yt_dlp.extractor.common import InfoExtractor
from yt_dlp.postprocessor.common import PostProcessor
from yt_dlp.utils import DownloadError, PostProcessingError


def main():
    parsed = yt_dlp.parse_options(sys.argv[1:])
    config = json.loads(Path(os.environ['YTDLG_TEST_CONFIG']).read_text(encoding='utf-8-sig'))
    payload = io.BytesIO()
    with wave.open(payload, 'wb') as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(8000)
        output.writeframes(b'\0\0' * 800)
    data = payload.getvalue()

    class Handler(BaseHTTPRequestHandler):
        def do_GET(self):
            source_id = self.path.rsplit('/', 1)[-1].split('.')[0]
            with Path(config['requests']).open('a', encoding='utf-8') as log:
                log.write(source_id + '\n')
            if source_id == config.get('http_failure'):
                self.send_error(500, 'Deliberate fixture failure')
                return
            self.send_response(200)
            self.send_header('Content-Type', 'application/octet-stream')
            self.send_header('Content-Length', str(len(data)))
            self.end_headers()
            self.wfile.write(data)

        def log_message(self, *_):
            pass

    server = ThreadingHTTPServer(('127.0.0.1', 0), Handler)
    threading.Thread(target=server.serve_forever, daemon=True).start()

    class HistoryFixtureIE(InfoExtractor):
        IE_NAME = 'historyfixture'
        _VALID_URL = r'https://history\.invalid/(?P<kind>video|channel|playlist)/(?P<id>[A-Za-z0-9_-]+)'

        def _real_extract(self, url):
            match = self._match_valid_url(url)
            kind, source_id = match.group('kind', 'id')
            if kind != 'video':
                ids = config['channel'] if kind == 'channel' else config['playlists'][source_id]
                return self.playlist_result([
                    self.url_result('https://history.invalid/video/' + item, self.ie_key(), item)
                    for item in ids
                ], source_id, kind + '-' + source_id)
            return {
                'id': source_id,
                'title': config.get('title', 'Fixture') + '-' + source_id,
                'url': f'http://127.0.0.1:{server.server_port}/media/{source_id}.{config["ext"]}',
                'ext': config['ext'],
                'format_id': 'fixture',
                'vcodec': 'none',
                'acodec': 'pcm_s16le',
            }

    class DeliberateFailurePP(PostProcessor):
        def run(self, info):
            if info['id'] == config.get('postprocess_failure'):
                raise PostProcessingError('Deliberate fixture post-processing failure')
            return [], info

    try:
        with yt_dlp.YoutubeDL(parsed.ydl_opts, auto_init=False) as downloader:
            downloader.add_info_extractor(HistoryFixtureIE())
            downloader.add_post_processor(DeliberateFailurePP(), when='post_process')
            return downloader.download(parsed.urls)
    except DownloadError:
        return 1
    finally:
        server.shutdown()
        server.server_close()


if __name__ == '__main__':
    sys.exit(main())
