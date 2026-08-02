from __future__ import annotations

import argparse
import hashlib
import shutil
import tempfile
import urllib.request
import zipfile
from pathlib import Path


OCR_FILES = {
    "paddle/official_models/PP-OCRv5_mobile_det_onnx/inference.onnx": (
        "https://huggingface.co/PaddlePaddle/PP-OCRv5_mobile_det_onnx/resolve/main/inference.onnx?download=true",
        "a431985659dc921974177a95adcfbb90fd9e51989a5e04d70d0b75f597b6e61d",
    ),
    "paddle/official_models/PP-OCRv5_mobile_det_onnx/inference.yml": (
        "https://huggingface.co/PaddlePaddle/PP-OCRv5_mobile_det_onnx/resolve/main/inference.yml?download=true",
        "98069072e1b6b37d727fd9d9f11725faa46d6ea0de012f2ed26caea011c37699",
    ),
    "paddle/official_models/PP-OCRv5_mobile_rec_onnx/inference.onnx": (
        "https://huggingface.co/PaddlePaddle/PP-OCRv5_mobile_rec_onnx/resolve/main/inference.onnx?download=true",
        "da72dc72ca4dc220df0dfde68c1dedc31c58d3e76a25871122e5056227d50092",
    ),
    "paddle/official_models/PP-OCRv5_mobile_rec_onnx/inference.yml": (
        "https://huggingface.co/PaddlePaddle/PP-OCRv5_mobile_rec_onnx/resolve/main/inference.yml?download=true",
        "5dfeb2777f6d0db8177d8128a8acfcf6e6276dc4ac73ea3bf0dc06d6a5e85d8e",
    ),
}

ARGOS_ARCHIVES = {
    "translate-zh_en-1_9.argosmodel": (
        "https://argos-net.com/v1/translate-zh_en-1_9.argosmodel",
        "62e7af5a3a48b530e47b7b3e5c78c2de79073ecd815750d2bf3ab35b4a67da2d",
        "translate-zh_en-1_9",
        "edd8c8a6863d36959613ff291074627a1635fab2f51b872ef437e924d238921a",
    ),
    "translate-en_zh-1_9.argosmodel": (
        "https://argos-net.com/v1/translate-en_zh-1_9.argosmodel",
        "433e7c4f034d87fbe2353161e05f18646d7999452f801a4e1f0378522b9850ab",
        "translate-en_zh-1_9",
        "1a039114d9456b6528fabb65b455b6f156319634a0f984b1f6018f7737d67598",
    ),
}


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def download(url: str, destination: Path, expected_sha256: str) -> None:
    request = urllib.request.Request(url, headers={"User-Agent": "PingYi-release-builder/1.0"})
    digest = hashlib.sha256()
    with urllib.request.urlopen(request, timeout=120) as response, destination.open("wb") as output:
        while block := response.read(1024 * 1024):
            digest.update(block)
            output.write(block)
    actual = digest.hexdigest()
    if actual != expected_sha256:
        destination.unlink(missing_ok=True)
        raise RuntimeError(f"Checksum mismatch for {url}: expected {expected_sha256}, got {actual}")


def safe_extract(archive: Path, destination: Path) -> None:
    destination = destination.resolve()
    with zipfile.ZipFile(archive) as package:
        for member in package.infolist():
            target = (destination / member.filename).resolve()
            if target != destination and destination not in target.parents:
                raise RuntimeError(f"Unsafe archive path: {member.filename}")
        package.extractall(destination)


def main() -> int:
    parser = argparse.ArgumentParser(description="Download pinned PingYi offline baseline models.")
    parser.add_argument("--destination", type=Path, required=True)
    args = parser.parse_args()
    destination = args.destination.resolve()
    destination.mkdir(parents=True, exist_ok=True)

    with tempfile.TemporaryDirectory(prefix="pingyi-models-") as temporary:
        temporary_root = Path(temporary)
        for relative, (url, expected_hash) in OCR_FILES.items():
            target = destination / relative
            target.parent.mkdir(parents=True, exist_ok=True)
            if target.is_file() and sha256_file(target) == expected_hash:
                continue
            print(f"Downloading {relative}", flush=True)
            download(url, target, expected_hash)

        argos_root = destination / "argos"
        argos_root.mkdir(parents=True, exist_ok=True)
        for filename, (url, archive_hash, package_name, model_hash) in ARGOS_ARCHIVES.items():
            model_file = argos_root / package_name / "model" / "model.bin"
            if model_file.is_file() and sha256_file(model_file) == model_hash:
                continue
            archive = temporary_root / filename
            print(f"Downloading {filename}", flush=True)
            download(url, archive, archive_hash)
            package_directory = argos_root / package_name
            if package_directory.exists():
                shutil.rmtree(package_directory)
            safe_extract(archive, argos_root)
            if not model_file.is_file() or sha256_file(model_file) != model_hash:
                raise RuntimeError(f"Extracted Argos model failed validation: {model_file}")

    print(f"Pinned offline model source is ready: {destination}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
