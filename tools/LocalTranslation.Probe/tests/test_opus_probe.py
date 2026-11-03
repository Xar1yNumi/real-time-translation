import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from opus_probe import source_tokens


class FakeSentencePiece:
    def encode(self, text, out_type):
        assert out_type is str
        return text.split()


class OpusProbeTests(unittest.TestCase):
    def test_target_language_tag_is_added_before_source_tokens(self):
        self.assertEqual(
            source_tokens(FakeSentencePiece(), "Hello world", ">>cmn_Hans<<"),
            [">>cmn_Hans<<", "Hello", "world", "</s>"],
        )

    def test_chinese_to_english_does_not_add_a_tag(self):
        self.assertEqual(source_tokens(FakeSentencePiece(), "你好", ""), ["你好", "</s>"])

    def test_empty_text_does_not_reach_model(self):
        self.assertIsNone(source_tokens(FakeSentencePiece(), "   ", ""))

    def test_long_text_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "too long"):
            source_tokens(FakeSentencePiece(), "word " * 300, "")


if __name__ == "__main__":
    unittest.main()
