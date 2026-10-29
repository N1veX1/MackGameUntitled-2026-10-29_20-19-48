UNITY MASK SIX VIEWS

Open Assets/Scenes/MaskSixViews.unity and press Play.

Controls:
WASD   Move the character.
M      Put on / remove the mask.
1      X / -X. Press again to flip the direction.
2      Y / -Y. Press again to flip the direction.
3      Z / -Z. Press again to flip the direction.
You can also click the three axis buttons at the top of the Game view.

The same blocking wall is hidden and made passable while the mask is on.
Y hides the ceiling renderer so you can see from above.
-Y shows the underside of the solid floor; the character is behind it.
-X hides the left corridor wall renderer as a cutaway so you can see the
same level and character from the opposite side. Its collider remains.

Editable objects are in the scene hierarchy. On Player Camera, adjust
MaskAxisCamera's camera distance, size, focus, key bindings, wall arrays,
and cutaway renderer arrays. The equipped visual is the MaskXYZ prefab from
the installed MaskXYZ.unitypackage. On Player, adjust MaskDemoPlayer's speed.
Tools > Mask Demo > Open Scene reopens this scene without rebuilding it.

The six separate SVG design diagrams are in this Design folder.
