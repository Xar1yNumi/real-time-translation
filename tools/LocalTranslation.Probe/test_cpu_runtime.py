"""Check that the packaged CPU worker produces stable, useful translations."""

from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
import unittest
from pathlib import Path


class CpuRuntimeTests(unittest.TestCase):
    model_root: Path
    worker_path: Path

    def run_worker(self) -> list[str]:
        samples = [
            ("en", "I am testing real-time translation captions."),
            ("en", "The train has arrived. Please take your belongings."),
            ("zh", "请在下一站下车。"),
            ("zh", "如果网络断开，软件还能继续工作吗？"),
            ("zh", "我正在测试实时翻译字幕。"),
        ]
        requests = "".join(
            json.dumps({"id": str(i), "source": source, "text": text}) + "\n"
            for i, (source, text) in enumerate(samples)
        )
        result = subprocess.run(
            [sys.executable, str(self.worker_path), "--models", str(self.model_root)],
            input=requests.encode("utf-8"), capture_output=True, timeout=60,
            env={**os.environ, "PYTHONIOENCODING": "utf-8"},
        )
        stderr = result.stderr.decode("utf-8", errors="replace")
        self.assertEqual(result.returncode, 0, stderr)
        self.assertNotIn("Bad memory unallocation", stderr)
        output = result.stdout.decode("utf-8")
        self.assertTrue(all(line.startswith("{") for line in output.splitlines()), output)
        messages = [json.loads(line) for line in output.splitlines()]
        self.assertEqual(messages.pop(0), {"type": "ready", "version": 1})
        self.assertEqual([m.get("id") for m in messages], ["0", "1", "2", "3", "4"])
        self.assertTrue(all(m.get("translation") for m in messages), messages)
        return [m["translation"] for m in messages]

    def test_repeated_processes_keep_protocol_and_direction(self) -> None:
        captions_preserved = 0
        for _ in range(4):
            outputs = self.run_worker()
            self.assertIn("测试", outputs[0])
            self.assertIn("翻译", outputs[0])
            self.assertTrue(any(word in outputs[1] for word in ("火车", "列车")))
            self.assertTrue(any(place in outputs[2].lower() for place in ("next stop", "next station")), outputs)
            self.assertIn("network", outputs[3].lower())
            self.assertTrue(outputs[4].isascii(), outputs)
            self.assertTrue(any(word in outputs[4].lower() for word in ("test", "translat")), outputs)
            if any(word in outputs[4].lower() for word in ("subtitle", "caption")):
                captions_preserved += 1
        self.assertGreaterEqual(captions_preserved, 2)

    def test_long_session_never_inserts_native_output_into_protocol(self) -> None:
        requests = "".join(
            json.dumps({
                "id": str(i),
                "source": "zh" if i % 2 else "en",
                "text": "请在下一站下车。" if i % 2 else "Please get off at the next stop.",
            }, ensure_ascii=False) + "\n"
            for i in range(64)
        )
        result = subprocess.run(
            [sys.executable, str(self.worker_path), "--models", str(self.model_root)],
            input=requests.encode("utf-8"), capture_output=True, timeout=120,
            env={**os.environ, "PYTHONIOENCODING": "utf-8"},
        )
        self.assertEqual(result.returncode, 0, result.stderr.decode("utf-8", errors="replace"))
        lines = result.stdout.decode("utf-8").splitlines()
        self.assertEqual(len(lines), 65, lines[-5:])
        messages = [json.loads(line) for line in lines]
        self.assertEqual(messages[0], {"type": "ready", "version": 1})
        self.assertEqual([message.get("id") for message in messages[1:]], [str(i) for i in range(64)])
        self.assertTrue(all(message.get("translation") for message in messages[1:]))

    def test_native_stdout_write_does_not_corrupt_json(self) -> None:
        harness = """
import importlib.util, os, sys
path, models = sys.argv[1:]
spec = importlib.util.spec_from_file_location('worker_under_test', path)
worker = importlib.util.module_from_spec(spec)
spec.loader.exec_module(worker)
class NativeWritingEngine:
    def __init__(self, _): pass
    def translate(self, text, source):
        os.write(1, b'BLAS : Bad memory unallocation! : simulated\\n')
        return 'translated'
worker.OpusEngine = NativeWritingEngine
sys.argv = [path, '--models', models]
worker.main()
"""
        request = json.dumps({"id": "one", "source": "en", "text": "Hello"}) + "\n"
        result = subprocess.run(
            [sys.executable, "-c", harness, str(self.worker_path), str(self.model_root)],
            input=request.encode("utf-8"), capture_output=True, timeout=10,
            env={**os.environ, "PYTHONIOENCODING": "utf-8"},
        )
        self.assertEqual(result.returncode, 0, result.stderr.decode("utf-8", errors="replace"))
        self.assertEqual(
            [json.loads(line) for line in result.stdout.decode("utf-8").splitlines()],
            [{"type": "ready", "version": 1}, {"id": "one", "translation": "translated"}],
        )


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--models", type=Path, required=True)
    parser.add_argument("--worker", type=Path, default=Path(__file__).resolve().parents[1] / "LocalTranslation.Worker" / "worker.py")
    args = parser.parse_args()
    CpuRuntimeTests.model_root = args.models.resolve()
    CpuRuntimeTests.worker_path = args.worker.resolve()
    unittest.main(argv=[sys.argv[0]])
