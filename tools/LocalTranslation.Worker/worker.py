"""Resident, line-delimited JSON translation worker. Stdout is protocol only."""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

MAX_LINE_BYTES = 16_384
MAX_TEXT_BYTES = 12_000


class OpusEngine:
    def __init__(self, model_root: Path):
        import ctranslate2
        import sentencepiece
        from opencc import OpenCC

        self.models = {}
        self.simplifier = OpenCC("t2s")
        for source in ("zh", "en"):
            model_dir = model_root / ("opus-mt-zh-en" if source == "zh" else "opus-mt-en-zh")
            metadata = json.loads((model_dir / "opus_mt.json").read_text(encoding="utf-8"))
            expected = "Helsinki-NLP/opus-mt-zh-en" if source == "zh" else "Helsinki-NLP/opus-mt-en-zh"
            if metadata.get("repo") != expected:
                raise ValueError(f"unexpected model metadata for {source}")
            self.models[source] = (
                ctranslate2.Translator(str(model_dir), device="cpu", compute_type="auto"),
                sentencepiece.SentencePieceProcessor(model_file=str(model_dir / "source.spm")),
                sentencepiece.SentencePieceProcessor(model_file=str(model_dir / "target.spm")),
                metadata["language_tag"],
            )

    def translate(self, text: str, source: str) -> str:
        translator, source_spm, target_spm, tag = self.models[source]
        tokens = source_spm.encode(text.strip(), out_type=str)
        if len(tokens) > 256:
            raise ValueError("input exceeds 256 tokens")
        if tag:
            tokens.insert(0, tag)
        tokens.append("</s>")
        result = translator.translate_batch(
            [tokens], beam_size=2 if source == "en" else 1, repetition_penalty=1.1,
            no_repeat_ngram_size=3, max_decoding_length=128,
        )[0]
        output = target_spm.decode(
            [token for token in result.hypotheses[0] if token not in ("</s>", "<s>", "<pad>")]
        ).strip()
        return self.simplifier.convert(output) if source == "en" else output


def _write(output, message: dict) -> None:
    output.write(json.dumps(message, ensure_ascii=False, separators=(",", ":")) + "\n")
    output.flush()


def serve(input_stream, output, engine) -> None:
    _write(output, {"type": "ready", "version": 1})
    while line := input_stream.readline(MAX_LINE_BYTES + 1):
        if len(line) > MAX_LINE_BYTES:
            while line and not line.endswith(b"\n"):
                line = input_stream.readline(MAX_LINE_BYTES + 1)
            _write(output, {"error": "request_too_large"})
            continue
        request_id = None
        try:
            request = json.loads(line)
            if isinstance(request, dict):
                request_id = request.get("id")
            if (not isinstance(request, dict) or
                not isinstance(request_id, str) or not 0 < len(request_id) <= 64 or
                request.get("source") not in ("en", "zh") or
                not isinstance(request.get("text"), str) or
                not request["text"].strip() or
                len(request["text"].encode("utf-8")) > MAX_TEXT_BYTES):
                raise ValueError("invalid request")
            translated = engine.translate(request["text"], request["source"])
            if not translated:
                raise RuntimeError("empty translation")
            _write(output, {"id": request_id, "translation": translated})
        except (ValueError, TypeError, UnicodeDecodeError, json.JSONDecodeError):
            _write(output, {"id": request_id, "error": "invalid_request"})
        except Exception:
            _write(output, {"id": request_id, "error": "translation_failed"})


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--models", type=Path, required=True)
    args = parser.parse_args()
    engine = OpusEngine(args.models)
    serve(sys.stdin.buffer, sys.stdout, engine)


if __name__ == "__main__":
    main()
