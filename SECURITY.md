# Security policy

## Supported versions

This project has no released supported version yet. Security fixes for the current MVP are reviewed on `main`.

## Report a vulnerability

Please use the repository's **Report a vulnerability** option under GitHub Security, if available. If it is unavailable, open a minimal issue asking the maintainer for a private reporting channel. Do not post exploit details, credentials, personal audio, or transcripts in a public issue.

Include the affected commit, reproducible steps, impact, and a safe proof of concept. The maintainer will acknowledge and coordinate a fix before public disclosure. There is no guaranteed response time while the project is in MVP development.

Azure API keys belong to the user's own subscription. If a key may have been exposed, rotate it in Azure immediately and remove it from local or published copies. The application stores the key in Windows Credential Manager; see [privacy notes](docs/privacy.md).
