# Third-party notices

PingYi is licensed under MIT. Standard offline release packages additionally contain:

- PaddlePaddle PP-OCRv5 mobile detection and recognition ONNX models, licensed under Apache-2.0. Source: https://huggingface.co/PaddlePaddle
- Argos Translate runtime components, licensed under MIT. Source: https://github.com/argosopentech/argos-translate
- Chinese-English and English-Chinese Argos model packages derived from OPUS-MT. The model package README identifies the original model license as CC BY 4.0 and credits Jörg Tiedemann and Santhosh Thottingal, “OPUS-MT — Building open translation services for the World,” EAMT 2020.
- CTranslate2, licensed under MIT. Source: https://github.com/OpenNMT/CTranslate2
- SentencePiece and PaddleOCR, licensed under Apache-2.0; ONNX Runtime, Avalonia, SkiaSharp and their transitive runtime components under their respective licenses.

PingYi release builds intentionally exclude NVIDIA CUDA, cuDNN and related
proprietary GPU runtime binaries. The release dependency audit fails the build
if these files, an NVIDIA Python package, or a Torch runtime are detected.

The upstream projects and authors retain all rights granted by their respective licenses. This notice does not replace the full upstream license texts.
