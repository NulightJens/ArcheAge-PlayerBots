# Deterministic evidence receipt translator

`New-EvidenceReceipt.ps1` turns a bounded check summary into an immutable,
fingerprinted JSON receipt. It reads but never changes the metadata and evidence
inputs, refuses to overwrite an output, and uses no clock, host, process, or
environment values in the receipt.

```powershell
pwsh -NoProfile -File scripts/evidence/New-EvidenceReceipt.ps1 `
  -MetadataPath metadata.json `
  -EvidencePath checks.json `
  -OutputPath receipt.json
```

The metadata input is a `playerbots.evidence-metadata.v1` JSON object:

```json
{
  "schema_version": "playerbots.evidence-metadata.v1",
  "receipt_id": "example-v1",
  "generated_at_utc": "2026-08-31T12:00:00Z",
  "gate_id": "focused-aaemu12-tests",
  "fingerprint": {
    "module_source_commit": "1111111111111111111111111111111111111111",
    "aaemu_host_base_commit": "2222222222222222222222222222222222222222",
    "compatibility_patch_sha256": "3333333333333333333333333333333333333333333333333333333333333333",
    "client_provenance_sha256_or_not_applicable": "not-applicable",
    "database_provenance": "not-applicable",
    "gate_definition_version": 1,
    "command_and_environment_fingerprint": "4444444444444444444444444444444444444444444444444444444444444444"
  }
}
```

JSON evidence uses this closed shape:

```json
{
  "schema_version": "playerbots.evidence-input.v1",
  "checks": [
    { "id": "build", "verdict": "PASS" },
    { "id": "focused-tests", "verdict": "PASS" }
  ]
}
```

Line evidence starts with its schema marker, followed by one
`<check-id><TAB><verdict>` record per line:

```text
playerbots.evidence-lines.v1
build	PASS
focused-tests	PASS
```

Check IDs are unique lowercase identifiers. Verdicts are exactly `PASS`, `FAIL`,
or `INCOMPLETE`. The receipt is `INCOMPLETE` if any check is incomplete, otherwise
`FAIL` if any check fails, and otherwise `PASS`. Missing, malformed, duplicate, or
empty evidence is rejected without creating an output.

Run the self-contained focused proof with:

```powershell
pwsh -NoProfile -File scripts/evidence/tests/Test-EvidenceReceipt.ps1
```

Each proof run is retained under the ignored `scripts/evidence/.test-runs/`
directory. The test never starts AAEmu, a client, or a database.
