# VCSpacePhysics

Work-in-progress mod aiming to add full undampened 6DOF movement to Void Crew. Intended for fans of space games
like Outer Wilds / Elite Dangerous, or just for anyone who knows how a spaceship works.

## How do I get this mod?

This mod is still work-in-progress and currently makes the game essentially unplayable. I also have
not yet made the mod comply with the Void Crew developers' guidelines, such as disabling progression.
For these reasons I am not ready to publish a build of the mod yet.

But the project is open-source, so if you are a developer you should easily be able to build it yourself.

## Contributing

If you'd like to submit bugfixes or simple features from the TODO list, please be my guest.

If you'd like to contribute a more complicated feature, or something not on the TODO list,
I suggest you talk to me before you spend time developing it. I have a very particular vision
for what I want this mod to become, and it would be sad if I had to reject your work because
it doesn't align with my vision.

## TODO

### Ship

- [x] Remove friction
- [x] Unlock rotation and movement
- [x] Remap existing keyboard controls
- [x] Add mouse-based pitch/yaw controls
- [x] Add UI for pitch/yaw controls
- [x] Make ship thruster VFX support new rotations
- [x] Remove uses of velocity from ship audio systems
- [x] Allow exit vector auto-alignment to pitch the ship as well as yawing it
- [x] Adjust ship third-person pilot camera to follow ship rotation
- [ ] Improve support for controllers and flight sticks
- [ ] Add UI to indicate relative velocity from landmarks, not just distance
- [ ] Update autopilot (not intended to make use of turnover maneuvers, at least not initially)

### EVA

- [x] Preserve player rotation when leaving ship
- [x] Make pitching the camera rotate the whole character and not just the head
- [x] Unlock pitch to allow unlimited (360+ degree) rotation
- [ ] Smooth animation to align up direction when landing on platforms with gravity (e.g. ship)
- [ ] Remove friction
- [ ] Add roll control

### Other

- [ ] Update to comply with Void Crew developer's mod guidelines
- [ ] Update to use modding template
- [x] Update binding names in control settings menu
- [ ] Expand play area size
- [ ] Modify terrain generation to space things out more
- [ ] Modify terrain generation to randomly rotate bases
- [ ] Modify exit vector placement to be in any direction
- [ ] Update AI to make better use of larger play area

## Known Bugs

- When an EVA player leaves the ship's hull, sometimes a bit of rotation or momentum is applied to their character
- The automatic exit vector alignment doesn't account for angular momentum. This can cause it to overshoot the vector and slowly spiral to alignment, which takes a long time.
- Mod has only been tested in single player. Multiplayer is completely untested!