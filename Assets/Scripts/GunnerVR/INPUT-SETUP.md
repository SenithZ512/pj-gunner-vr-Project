# Gunner VR — Input setup

Everything below was read out of this project, not assumed. Action names, binding paths and
control names are quoted as they appear in the asset and in the OpenXR package.

## What the project already has

One action asset:

```
Assets/Samples/XR Interaction Toolkit/3.1.2/Starter Assets/XRI Default Input Actions.inputactions
```

Its maps: `XRI Head`, `XRI Left`, `XRI Left Interaction`, `XRI Left Locomotion`,
`XRI Right`, `XRI Right Interaction`, `XRI Right Locomotion`, `XRI UI`,
`Touchscreen Gestures`.

`XRI Right Interaction` (and the matching `XRI Left Interaction`) contains:

| Action | Type | Binding in the asset |
| --- | --- | --- |
| `Select` | Button | `<XRController>{RightHand}/{GripButton}` |
| `Select Value` | Value | `<XRController>{RightHand}/{Grip}` |
| **`Activate`** | **Button** | **`<XRController>{RightHand}/{TriggerButton}`** |
| `Activate Value` | Value | `<XRController>{RightHand}/{Trigger}` |
| `UI Press` | Button | `<XRController>{RightHand}/{TriggerButton}` |
| `UI Press Value`, `UI Scroll` | — | UI only |
| `Translate / Rotate / Manipulation`, `Scale Toggle`, `Scale Over Time` | — | far-grab manipulation |

The paths above are the controller bindings. `Select`, `Select Value` and `UI Press` each
carry extra bindings for hand tracking and other devices (`<MetaAimHand>`,
`<HandInteraction>`, `<HoloLensHand>`, `<KHRSimpleController>`); the `<XRController>` entry is
the one that applies on Quest Touch. `Activate` and `Activate Value` have the single
controller binding each.

Note that `Activate` and `UI Press` share the trigger. That is by design in the Starter
Assets — `UI Press` only does anything while a UI ray is pointing at a canvas.

There is **no** `Reload` action. Do not reference one until you have created it; see below.

## OpenXR controller profiles

Checked in `Assets/XR/Settings/Open XR Package Settings.asset`, Standalone group:

- `Oculus Touch Controller Profile` — **enabled**
- `Meta Quest Touch Plus Controller Profile` — **enabled**

These are what make the generic `<XRController>{RightHand}/{TriggerButton}` usage binding
resolve on Quest Touch hardware. Leave both on.

## Firing — no setup needed

`WeaponController` leaves **Fire Action** empty by default. With it empty, firing is driven by
the `activated` event on `XRGrabInteractable`, which XRI raises from whichever hand is holding
the prop, using that hand's own `Activate` action. This is the recommended setup: the prop
works in either hand with no per-hand wiring.

To drive it explicitly instead, set **Fire Action** on the Weapon's `WeaponController` to
`XRI Default Input Actions → XRI Right Interaction → Activate`. Once a fire action with at
least one binding is assigned, it takes over and the `activated` route is ignored, so one
trigger pull is still one shot.

> An untouched `InputActionProperty` serialises an empty action rather than a null one, so
> `WeaponController` treats a field as assigned only when the action has at least one binding.

## Adding a Reload action

Out of the box, reloading is done by pushing the prop into the ammo crate at the firing line
(`ReloadZone`), which needs no input binding at all. To add a button reload:

1. Open `XRI Default Input Actions.inputactions` (double-click it in the Project window).
2. Select the **`XRI Right Interaction`** map, press **+** on the Actions list, name it
   `Reload`, and set **Action Type** to `Button`.
3. Select the binding under it and set **Path** to one of:
   - `<XRController>{RightHand}/secondaryButton` — generic, works across headsets
   - `<OculusTouchController>{RightHand}/secondaryButton` — Quest Touch specific
4. Repeat in `XRI Left Interaction` with `{LeftHand}` if you want it on both hands.
5. **Save Asset**.
6. Select the `Weapon` object in `Workshop.unity` and drag the new action into
   **Reload Action** on `WeaponController`.

On Meta Quest Touch controllers the layout is `Oculus Touch Controller (OpenXR)` and its
controls are: `trigger`, `triggerPressed`, `triggerTouched`, `grip`, `gripPressed`,
`primaryButton`, `primaryTouched`, `secondaryButton`, `secondaryTouched`, `thumbstick`,
`thumbstickClicked`, `thumbstickTouched`, `thumbrestTouched`, `menu`, `isTracked`.

`primaryButton` is **A** on the right controller and **X** on the left; `secondaryButton` is
**B** on the right and **Y** on the left. `menu` exists only on the left controller.

## Checking the aim

Select the `Weapon` object in the scene. A yellow gizmo line is drawn from the muzzle along
the exact direction `Physics.Raycast` will use. If that line does not run down the middle of
the barrel, move or rotate the `Muzzle` child until it does — the ray follows the muzzle's
blue Z axis and nothing else.
