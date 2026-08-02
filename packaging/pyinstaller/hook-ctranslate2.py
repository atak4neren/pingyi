"""Collect only the CPU-safe CTranslate2 runtime binaries used by PingYi."""

from pathlib import Path

from PyInstaller.utils.hooks import collect_dynamic_libs


_PROPRIETARY_GPU_TOKENS = (
    "cublas",
    "cudart",
    "cudnn",
    "cufft",
    "curand",
    "cusolver",
    "cusparse",
    "nvidia",
    "nvjitlink",
    "nvrtc",
    "tensorrt",
)


def is_cpu_safe(binary: tuple[str, str]) -> bool:
    filename = Path(binary[0]).name.lower()
    return not any(token in filename for token in _PROPRIETARY_GPU_TOKENS)


binaries = [binary for binary in collect_dynamic_libs("ctranslate2") if is_cpu_safe(binary)]
