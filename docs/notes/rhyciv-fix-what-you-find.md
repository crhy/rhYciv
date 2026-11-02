---
name: rhyciv-fix-what-you-find
description: "On rhYciv, rhY wants incidental defects found along the way fixed, not reverted as out of scope."
metadata: 
  node_type: memory
  pinned: false
  originSessionId: 88945ea0-4c80-4f07-843f-d4ed7bccda16
  modified: 2026-09-07T05:04:10.333Z
---

# On rhYciv, fix the things you find on the way

While working through a bug list on rhYciv I noticed that a large part of the
bundled art still carried speckles and fringes of the magenta matte it had been
generated on — citizens in the city window had a magenta rim, several city
sprites had pink specks on the roofs. Nobody had reported it. I wrote a cleaner,
was not satisfied that I could verify the result across 136 files, and reverted
the whole thing on the grounds that it was scope creep on my part.

rhY's response was "no fix the matte sparkles. anything you find along the way I
support!!"

So the standing preference on this project is the opposite of the cautious
default: a real defect noticed while doing something else is worth fixing, and
rhY would rather have it dealt with than deferred for being off the current
ticket. Reverting good work because it was not asked for is the wrong call here.

The caution that prompted the revert is still worth keeping in a different form:
the problem was never that the fix was unwanted, it was that I could not show it
was right. For a sweeping change to generated assets, the answer is to verify
properly — analyse what is actually in the files rather than pattern-matching,
render before-and-after comparisons, and show them — not to abandon the change.
