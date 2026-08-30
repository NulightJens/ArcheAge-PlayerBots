# Security Policy

## Supported release

Security fixes target the current development line and the latest public prerelease on the documented AAEmu 1.2 compatibility base. The ArcheAge 3.0 track is experimental and is not a supported runtime release.

## Reporting

Do not publish credentials, private server data, or an exploitable report in a public issue. Use GitHub's private vulnerability reporting feature when it is enabled for this repository; otherwise contact the repository owner privately through the account's published contact method.

## Operator boundary

The `@system` command actor is unauthenticated administrator access intended only for local automation. Bind AAEmu's command Web API to `127.0.0.1` and never expose port 1280 publicly.

Review current NuGet advisories at deployment time. The optional patch under `compatibility/` is a retained tested baseline, not a promise that future advisories do not exist.
