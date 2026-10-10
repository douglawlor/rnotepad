# Project Codex guidance

This project uses the shared Windows Codex desktop runtime; do not install a second copy here.

- Keep workspace-write sandboxing and on-request approvals.
- Keep this project as the only writable root unless the user explicitly approves another directory.
- Run the PowerShell health check at scripts/codex-health-check.ps1 after Codex updates and before substantial work.
- Use a separate Windows user or VM when strict isolation is required.
