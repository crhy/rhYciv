---
name: rhyciv-generated-art-direction
description: "rhY wants procedurally generated rhYciv tile art to look hand-made and physically plausible, not ruler-straight or merely layered."
metadata: 
  node_type: memory
  pinned: false
  originSessionId: 88945ea0-4c80-4f07-843f-d4ed7bccda16
  modified: 2026-09-07T04:23:11.231Z
---

# Generated tile art for rhYciv should look hand-made, not machine-drawn

When I generate map overlays for rhYciv procedurally — irrigation, roads,
rivers, coastlines — rhY consistently steers the result toward irregularity and
physical plausibility, and away from anything that reads as a printed pattern.

Two corrections make the standard clear. On a first pass at irrigation I drew
the furrows as straight lines; rhY said it "should look a little rougher... it
looks a little too straight and linear," and specifically singled out *plain*
irrigation as needing it more than farmland. The reasoning is in-world: plain
irrigation is ditches dug by hand, while farmland is the same field worked after
Refrigeration, so the mechanised version may run closer to true but the earlier
one should not. Generated geometry therefore wants a deterministic wobble and
jittered spacing, sized to what actually cut the ground.

The second correction is about how layers meet. When farmland's two sets of
crossing channels were drawn as one full set composited over the other, rhY
objected that "the canals shouldn't overlap. they should interact," clarifying
with the single word "intersect." Compositing whole layers puts the second set's
banks across the first set's water at every junction, so the field reads as one
ribbon lying on top of another. Drawing pass by pass instead — every set's
banks, then every set's channels, then every set's water — lets the water run
continuously through the crossings. The general rule: when two generated
features cross, they should join, not stack.

rhY also pushed the water colour up, from a muted teal to a bright blue, on the
grounds that standing water is the whole point of an irrigated square and a
muted tone just read as another shade of the field. So the thing a feature
exists to communicate should be the thing that reads first.

rhY reviews this work by eye from rendered previews, so it is worth composing a
small contact sheet of the generated art over real terrain and showing it rather
than describing the change.
