"""PyInstaller runtime shims for optional Argos tokenizers removed from the bundle."""

import sys
import types


# CTranslate2 imports its conversion toolchain from package __init__ even though
# PingYi only needs the native Translator. Stub those optional namespaces first.
for optional_name in (
    "ctranslate2.converters",
    "ctranslate2.models",
    "ctranslate2.specs",
):
    if optional_name not in sys.modules:
        sys.modules[optional_name] = types.ModuleType(optional_name)


if "stanza" not in sys.modules:
    stanza = types.ModuleType("stanza")

    class UnavailablePipeline:
        def __init__(self, *args, **kwargs):
            raise RuntimeError("Stanza is not bundled; PingYi uses MiniSBD offline.")

    stanza.Pipeline = UnavailablePipeline
    sys.modules["stanza"] = stanza


if "minisbd" not in sys.modules:
    minisbd = types.ModuleType("minisbd")
    minisbd_models = types.ModuleType("minisbd.models")
    minisbd_models.cache_dir = ""
    minisbd_models.list_models = lambda: ["en", "zh-hans"]

    class UnavailableSBDetect:
        def __init__(self, *args, **kwargs):
            raise RuntimeError("MiniSBD is not bundled; PingYi uses its lightweight splitter.")

    minisbd.SBDetect = UnavailableSBDetect
    minisbd.models = minisbd_models
    sys.modules["minisbd"] = minisbd
    sys.modules["minisbd.models"] = minisbd_models
