# Repository Guidelines

## Project Structure & Module Organization

This repository is currently empty. No application source, tests, assets, or build configuration exist yet. When adding the initial implementation, organize application code in `src/`, automated tests in `tests/`, and static resources in `assets/` where appropriate. Group related modules by feature and document the resulting structure in `README.md`.

## Build, Test, and Development Commands

No build, test, or local development commands are configured. When introducing a toolchain, include its dependency manifest and document exact installation, development, build, and test commands in `README.md`. Ensure commands run from the repository root and identify any required runtime versions. Do not assume commands such as `npm test` are available until configured.

## Coding Style & Naming Conventions

Follow the conventions of the language selected for the implementation. Use consistent indentation, descriptive names, and small modules with clear responsibilities. Prefer the ecosystem’s standard formatter and linter; commit their configuration alongside the initial source code. Keep filenames consistent within each module and avoid unrelated formatting changes.

## Testing Guidelines

No testing framework or coverage threshold is defined. Introduce automated tests with the first functional code change. Use descriptive test names that state expected behavior, and cover normal operation, boundary conditions, and failure cases. Document how to run the complete suite and individual tests.

## Commit & Pull Request Guidelines

No commit history was available to establish existing conventions. Use concise, imperative commit subjects, such as `Add camera configuration loader`. Keep each commit focused on one logical change.

Pull requests should explain the purpose, summarize behavior changes, and describe validation performed. Link relevant issues when available. Include screenshots for visual changes and explicitly identify any missing tests or setup requirements.

## Security & Configuration

Never commit credentials, access tokens, or private connection details. Provide placeholder configuration examples and ignore local secrets and generated output when adding project tooling.