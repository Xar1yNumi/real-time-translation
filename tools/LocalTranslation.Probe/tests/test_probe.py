import sys
import hashlib
import json
import tempfile
import unittest
import zipfile
from pathlib import Path
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import probe
from probe import encode_text, percentile, prepare, validate_route


class FakeTokenizer:
    def encode(self, text, out_type):
        assert out_type is str
        return text.split()


class ProbeTests(unittest.TestCase):
    def test_only_opposite_chinese_english_routes_are_translated(self):
        self.assertEqual(validate_route("zh", "en"), ("zh", "en"))
        self.assertEqual(validate_route("en", "zh"), ("en", "zh"))

        for source, target in (("zh", "zh"), ("en", "en"), ("ru", "en")):
            with self.subTest(source=source, target=target):
                with self.assertRaises(ValueError):
                    validate_route(source, target)

    def test_percentiles_include_tail_latency(self):
        samples = [10.0, 20.0, 30.0, 40.0, 100.0]
        self.assertEqual(percentile(samples, 50), 30.0)
        self.assertEqual(percentile(samples, 95), 88.0)

    def test_empty_latency_sample_is_rejected(self):
        with self.assertRaises(ValueError):
            percentile([], 50)

    def test_empty_text_is_not_sent_to_model(self):
        self.assertIsNone(encode_text(FakeTokenizer(), "   ", "en"))

    def test_long_text_is_rejected_without_silent_truncation(self):
        with self.assertRaisesRegex(ValueError, "too long"):
            encode_text(FakeTokenizer(), "word " * 300, "en")

    def test_prepare_verifies_archive_and_extracted_file(self):
        scratch = probe.ROOT / "artifacts/localtranslation-test-temp"
        scratch.mkdir(parents=True, exist_ok=True)
        with tempfile.TemporaryDirectory(dir=scratch) as temporary:
            root = Path(temporary)
            archive = root / "model.zip"
            model = b"test model bytes"
            with zipfile.ZipFile(archive, "w") as bundle:
                bundle.writestr("models/m2m100_418m_ct2/model.bin", model)
            manifest = root / "manifest.json"
            manifest.write_text(json.dumps({
                "archive_sha256": probe.sha256_file(archive),
                "files": {"model.bin": {
                    "size": len(model), "sha256": hashlib.sha256(model).hexdigest()
                }},
            }), encoding="utf-8")

            with patch.object(probe, "MANIFEST", manifest):
                prepare(archive, root / "output")
            self.assertEqual((root / "output/model.bin").read_bytes(), model)

            manifest.write_text(json.dumps({
                "archive_sha256": "0" * 64,
                "files": {"model.bin": {
                    "size": len(model), "sha256": hashlib.sha256(model).hexdigest()
                }},
            }), encoding="utf-8")
            with patch.object(probe, "MANIFEST", manifest):
                with self.assertRaisesRegex(ValueError, "archive SHA-256"):
                    prepare(archive, root / "rejected")
            self.assertFalse((root / "rejected/model.bin").exists())


if __name__ == "__main__":
    unittest.main()
