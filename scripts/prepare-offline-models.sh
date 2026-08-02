#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
artifacts_root="$(realpath -m "$project_root/artifacts")"
source_root="$(realpath -m "${1:-${XDG_DATA_HOME:-$HOME/.local/share}/pingyi/models}")"
destination_root="$(realpath -m "${2:-$artifacts_root/offline-models}")"

case "$destination_root/" in
  "$artifacts_root"/*) ;;
  *) echo "Offline model destination must stay inside $artifacts_root" >&2; exit 1 ;;
esac
[[ -d "$source_root" ]] || { echo "Offline model source was not found: $source_root" >&2; exit 1; }

rm -rf -- "$destination_root"
mkdir -p "$destination_root"

copy_required() {
  local relative="$1"
  [[ -f "$source_root/$relative" ]] || { echo "Required model file was not found: $source_root/$relative" >&2; exit 1; }
  mkdir -p "$(dirname "$destination_root/$relative")"
  cp "$source_root/$relative" "$destination_root/$relative"
}

detection_relative="paddle/official_models/PP-OCRv5_mobile_det_onnx/inference.onnx"
recognition_relative="paddle/official_models/PP-OCRv5_mobile_rec_onnx/inference.onnx"
copy_required "$detection_relative"
copy_required "paddle/official_models/PP-OCRv5_mobile_det_onnx/inference.yml"
copy_required "$recognition_relative"
copy_required "paddle/official_models/PP-OCRv5_mobile_rec_onnx/inference.yml"

zh_package="$(find "$source_root/argos" -mindepth 1 -maxdepth 1 -type d -name 'translate-zh_en-*' | sort -V | tail -n 1)"
en_package="$(find "$source_root/argos" -mindepth 1 -maxdepth 1 -type d -name 'translate-en_zh-*' | sort -V | tail -n 1)"
[[ -n "$zh_package" && -n "$en_package" ]] || { echo "Required zh-en and en-zh Argos packages were not found." >&2; exit 1; }

for package in "$zh_package" "$en_package"; do
  package_name="$(basename "$package")"
  for relative in metadata.json sentencepiece.model model/config.json model/model.bin model/shared_vocabulary.json; do
    copy_required "argos/$package_name/$relative"
  done
done

detection_hash="$(sha256sum "$source_root/$detection_relative" | cut -d' ' -f1)"
recognition_hash="$(sha256sum "$source_root/$recognition_relative" | cut -d' ' -f1)"
zh_hash="$(sha256sum "$zh_package/model/model.bin" | cut -d' ' -f1)"
en_hash="$(sha256sum "$en_package/model/model.bin" | cut -d' ' -f1)"

printf '{\n  "schemaVersion": 1,\n  "sha256": {\n    "paddle-detection": "%s",\n    "paddle-recognition": "%s"\n  }\n}\n' \
  "$detection_hash" "$recognition_hash" > "$destination_root/ocr-models.json"
printf '{\n  "schemaVersion": 1,\n  "sha256": {\n    "argos-installed-zh-en": "%s",\n    "argos-installed-en-zh": "%s"\n  }\n}\n' \
  "$zh_hash" "$en_hash" > "$destination_root/translation-models.json"

du -sh "$destination_root"
