# PlayerBots build and test loop

This is the mandatory fast-feedback path for AAEmu 1.2 feature work.

1. **Writer gate:** compile the changed project and run the feature's smallest
   deterministic tests immediately. A candidate cannot be handed off with a
   failing or zero-test lane.
2. **Merged feature gate:** replay exact candidates once, assemble all required
   host seams in a fresh build-only checkout, and run every contributing
   feature lane together.
3. **Compatibility gate:** run the stable adjacent regression lanes for host
   scheduling, configuration, command output, allocation, and prior behavior
   touched by the merge.
4. **Clean build gate:** run one no-incremental AAEmu 1.2 solution build and
   require zero errors. Retain warning count and assembly fingerprints.
5. **Full-suite gate:** after the preceding gates are green, run exactly one
   full suite for the integration wave. Retain exact pass/fail/skip counts and
   the complete log. A failure is a visible failed gate.
6. **Correction rule:** fix only the diagnosed regression, rerun the failed
   focused and compatibility lanes, rebuild cleanly, and use a fresh
   verification task for the next full-suite invocation. Do not hide the
   original failure with an in-task retry.
7. **Live gate:** only a full-suite-green candidate may enter a separately
   leased live smoke. The live smoke must prove the user-visible behavior with
   autonomous markers, not merely command responses or compilation.

Expected feedback is minutes, not the end of a long development wave. Source
proof and live proof remain distinct; neither may be reported as the other.
