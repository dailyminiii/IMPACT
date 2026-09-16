# Desktop XR mode

The public project retains XRI UI input modules in the Unity Editor by default,
so HoloLens, remoting, and synthetic-hand interactions work without an
additional setting.

If a desktop-only session produces repeated `Screen position out of view
frustum ... NaN` warnings, stop Play mode and turn off
`IMPACT > Editor XR UI Input`. Start Play mode again to use the warning-safe
desktop mode. Re-enable the menu item before testing HoloLens, remoting, or
synthetic-hand interaction.

The guard is compiled only for the Unity Editor; it never changes a device
build's input path.
