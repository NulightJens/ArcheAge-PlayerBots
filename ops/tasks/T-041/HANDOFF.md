# T-041 handoff — FAIL

T-041 stopped at the first material invalid counted gate. The exact three-bot
Director bootstrap passed, but the first shared fixture batch did not produce
one independently credited completion per bot: all three native kill credits
went to bot `20001`. Bots `20002` and `20003` selected `nearby_mortal` and
entered combat, but received no native kill credit, progression, completion,
or self-logout. No retry, second batch, or restart was attempted.

## Source and authority

- Binding commit: `4383c072fbf34ad0c42db248d39e704f8aa42db1`
- Thread: `01a05d8b-5c96-71c2-9e14-40959480d28f`
- Dispatch head: `e32a96ac20adc83b41662a3cb39e638bf12aaaf1`
- Host/module/tree: `62e3eb1d87da01194802ac886cd500134facad28` /
  `2243b53dcbda7a65ab66123c4bce4864d4c743dd` /
  `731314bf03169ffcc08a383a9652afd9281c75fa`
- Lease: `aaemu12`, exact T-041 runtime authority only

## Proof retained

- Fresh root: `D:\Codex-Labs\evidence\T-041\one-zone-autonomy-v1`
- Director admitted `20001`, `20002`, and `20003` one per 15-second tick in
  zone `221`, reached target/max `3`, and recorded zero admission failures,
  wrong-zone activation, or concurrent duplicates.
- All observers had live offline samples `0` and `1` before admission. They
  retained 2,080 derived samples, 2,056 raw responses, and zero raw-hash
  failures.
- At the three-bot full-resource safe boundary, exactly three
  `spawnpassive 10004 12` fixtures were staged: `44963`, `44974`, `44975`.
- All three bots independently selected `grind / nearby_mortal`. Bot `20001`
  received all three credits, progressed `8082084 -> 8082126` (`+42 XP`),
  recovered debt-free, self-logged out normally, and was refilled by the
  Director. Bots `20002` and `20003` received zero credits and never completed.
- Four metrics snapshots retained zero tick errors and runtime overlaps. The
  exact operator inventory is four metrics snapshots, one roster GET, and three
  allowed fixture commands; there was no `addbot`, `removebot`, or directed
  gameplay command.

## Cleanup and retained boundaries

Game PID `123512` then Login PID `10964` exited by graceful Ctrl+C. The three
observers ended through their console sessions. Final state is zero bots,
runtimes, AAEmu/client/observer processes, required listeners, and live logs;
MySQL PIDs remained `[6308, 8076]` and no database access occurred.

The deployed config changed only the nine contracted Director properties. Its
original bytes and final restored bytes both hash to
`7b57a52149f6059cbcd972c38b161c46781d6730dbb3c6b93890171ae8c43202`.
Three runtime logs were moved losslessly into evidence. The known post-cleanup
PhysicsThread disposed-provider warning is retained under Error.log SHA-256
`1760ece520bcc4d8ba8cf04148190edc0d0aa8fb924bd64acc66e90600826cab`.
Preflight and cleanup client snapshots were zero, but continuous client-process
sampling between those boundaries was not retained.

Wave 2, second refill, distinct-PID restart, Director rebootstrap, and its
two-minute dwell are unproven. The sealed manifest covers 6,271 payloads and
28,811,374 bytes; SHA-256 is
`a141d75bffc87ef37e6a01f53d71261c2f5b441885522637a4a4adb190460a00`, with
zero missing, mismatched, or unlisted payloads.

## Exact integration action

PB-000/Integrator should integrate this task's final commit containing only
`ops/evidence/aaemu12-t041-one-zone-autonomy-v1.yaml` and this handoff, retain
T-041 as `FAIL`, release the `aaemu12` lease only after accepting cleanup, and
keep T-037 blocked. Never reuse or overwrite this evidence root; recovery needs
a bounded source correction and a fresh versioned runtime-proof task.
