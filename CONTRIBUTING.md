# Contributing to PingYi

Thanks for helping improve PingYi.

## Before you start

- Search existing issues before opening a new one.
- Keep changes focused and explain their user impact.
- Do not include screenshots, OCR text, API keys, tokens, certificates, or other private data in issues or logs.

## Development

Requirements:

- .NET 10 SDK
- Python 3.13 when working on the Argos translation engine

Build and test:

```powershell
dotnet restore PingYi.slnx
dotnet build PingYi.slnx
dotnet test PingYi.slnx
py -3 -m unittest discover -s engine_host -p "test_*.py"
```

See `README.md` for local engine and release packaging commands.

## Pull requests

- Describe what changed and why.
- Include the checks you ran.
- Add or update tests for behavior changes.
- Update documentation and third-party notices when applicable.

By contributing, you agree that your contribution is licensed under the MIT License.
