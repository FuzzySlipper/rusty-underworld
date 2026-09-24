# Reviewer packet

Starting authority for every review delegation, read before the named lane.

1. Read this packet, then the lane file(s) named in the delegation. If this
   packet or a named lane is absent, say so and stop rather than improvising
   a process.
2. The delegation's inline brief is authoritative for scope: the commit under
   review (as a diff vs its parent), the files in scope, the checks to run,
   and the reporting format. Do not review other lanes or other files.
3. Findings are claims for the implementer to verify, not instructions. Report
   concrete defects with file/line evidence. Verification commands in the
   brief are requirements, not suggestions.
4. The standing lanes live in `lanes/`. The workflow rownames are in
   `README.md`. Donor checkouts are read-only references; never copy donor
   code, and never modify the paired Engine checkout.
