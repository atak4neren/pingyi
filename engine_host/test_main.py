from __future__ import annotations

import hashlib
import json
import socket
import tempfile
import unittest
from pathlib import Path
import main


class EngineHostTests(unittest.TestCase):
    def test_checksum_manifest_detects_modified_model(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            model_dir = Path(temporary_directory)
            zh_model = model_dir / "argos" / "translate-zh_en-1_9" / "model" / "model.bin"
            en_model = model_dir / "argos" / "translate-en_zh-1_9" / "model" / "model.bin"
            zh_model.parent.mkdir(parents=True)
            en_model.parent.mkdir(parents=True)
            zh_model.write_bytes(b"zh-en model")
            en_model.write_bytes(b"en-zh model")
            (model_dir / "translation-models.json").write_text(
                json.dumps(
                    {
                        "sha256": {
                            "argos-installed-zh-en": hashlib.sha256(zh_model.read_bytes()).hexdigest(),
                            "argos-installed-en-zh": hashlib.sha256(en_model.read_bytes()).hexdigest(),
                        }
                    }
                ),
                encoding="utf-8",
            )

            main._hash_cache.clear()
            self.assertTrue(main.verify_translation_manifest(model_dir))
            zh_model.write_bytes(b"modified model")
            self.assertFalse(main.verify_translation_manifest(model_dir))

    def test_simple_sentencizer_splits_and_bounds_long_text(self) -> None:
        text = "第一句。Second sentence!" + "长" * 500
        sentences = main.SimpleSentencizer().split_sentences(text)

        self.assertGreaterEqual(len(sentences), 4)
        self.assertTrue(all(len(sentence) <= 221 for sentence in sentences))

    def test_local_only_guard_blocks_outbound_socket(self) -> None:
        with self.assertRaisesRegex(RuntimeError, "本地模式"):
            with main.local_only_network_guard():
                socket.create_connection(("127.0.0.1", 9), timeout=0.01)


if __name__ == "__main__":
    unittest.main()
