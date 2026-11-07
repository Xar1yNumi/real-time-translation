import io
import json
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from worker import serve


class FakeEngine:
    def __init__(self):
        self.calls = []

    def translate(self, text, source):
        self.calls.append((text, source))
        return "translated: " + text


class WorkerTests(unittest.TestCase):
    def test_ready_and_correlated_translation(self):
        engine = FakeEngine()
        request = json.dumps({"id": "a-1", "source": "en", "text": "Hello"}).encode() + b"\n"
        output = io.StringIO()

        serve(io.BytesIO(request), output, engine)

        messages = [json.loads(line) for line in output.getvalue().splitlines()]
        self.assertEqual(messages[0], {"type": "ready", "version": 1})
        self.assertEqual(messages[1], {"id": "a-1", "translation": "translated: Hello"})
        self.assertEqual(engine.calls, [("Hello", "en")])

    def test_invalid_language_and_empty_text_are_not_translated(self):
        engine = FakeEngine()
        requests = b"".join(json.dumps(item).encode() + b"\n" for item in (
            {"id": "a", "source": "ru", "text": "Hello"},
            {"id": "b", "source": "en", "text": " "},
        ))
        output = io.StringIO()

        serve(io.BytesIO(requests), output, engine)

        messages = [json.loads(line) for line in output.getvalue().splitlines()]
        self.assertEqual([item["error"] for item in messages[1:]],
                         ["invalid_request", "invalid_request"])
        self.assertEqual(engine.calls, [])

    def test_oversized_line_is_drained_before_next_request(self):
        engine = FakeEngine()
        valid = json.dumps({"id": "next", "source": "zh", "text": "你好"}).encode() + b"\n"
        stream = io.BytesIO(b"x" * 20_000 + b"\n" + valid)
        output = io.StringIO()

        serve(stream, output, engine)

        messages = [json.loads(line) for line in output.getvalue().splitlines()]
        self.assertEqual(messages[1]["error"], "request_too_large")
        self.assertEqual(messages[2]["id"], "next")
        self.assertEqual(engine.calls, [("你好", "zh")])


if __name__ == "__main__":
    unittest.main()
