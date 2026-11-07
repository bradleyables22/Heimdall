# Deferred Bootstrap modernization review

Reviewed 2026-11-07. The user asked to save these thoughts and return to them after the current release
and template work. This is a review backlog, not approval to implement Bootstrap changes immediately.

## Direction

Retain the small, independent `HeimdallFramework.Bootstrap` class-helper package and explicit HTML
composition. Modernize its coverage, consistency, documentation, and optional lifecycle integration.
The package's CSS strings already work in native Blazor and Razor `class` attributes.

## Evidence collected

- Reviewed all 36 source files, covering 420 constants and 73 parameterized helpers.
- Checked 2,343 successful constant/method outputs against the documentation site's Bootstrap 5.3.8 CSS;
  these outputs represented 1,677 unique class tokens. Most mappings matched.
- `Layout.Offset(0)` emits `offset-0`, which has no selector in that CSS. Responsive zero offsets exist;
  define and test the behavior of the unqualified zero case.
- `Navbar.Light` emits the deprecated `navbar-light`, which has no selector in that CSS.
- `Bootstrap.Css(...)` splits on ordinary spaces only, so tabs/newlines prevent complete deduplication.
- `Btn.Primary` returns `btn btn-primary`; the website's direct-mapping example says `btn-primary`.
  Rendering's class joiner preserves duplicate tokens, so examples adding both `Btn.Base` and
  `Btn.Primary` repeat `btn`.
- Some helpers return one class and others return precomposed combinations. Base names vary, for
  example `Modal.ModalBase` and `Toast.ToastBase`; `Offcanvas` is instantiable while other groups are static.
- No dedicated Bootstrap tests were found. XML documentation is not generated/shipped despite source
  comments. The class-only package currently targets .NET 10 only.
- The documentation app loads Bootstrap/Bootswatch 5.3.8 CSS and Bootstrap 5.3.3 JavaScript.

The temporary audit harness is under `tmp/bootstrap-review/`. That folder is ignored and is not the
durable record; retain the findings here if temporary validation projects are cleaned up.

## Coverage gaps worth considering

- Placeholders, focus rings, icon links, carousel, tooltip, and popover vocabulary.
- Responsive offcanvas and print display utilities.
- Responsive ordering, grow/shrink, align-content, and other flex variants.
- `nav-underline`, `progress-stacked`, `navbar-nav-scroll`, and `link-body-emphasis`.
- Form details such as `btn-check`, `form-check-inline`, `form-check-reverse`, and `form-control-color`.
- Typed Bootstrap data/ARIA attributes as an optional rendering integration, preserving the independent
  class-string foundation.

## Recommended order

1. Fix the mapping/tokenization issues and add dedicated compatibility, boundary, and coverage tests.
2. Define a supported Bootstrap 5.3 baseline and audit against its actual CSS. Preserve compatibility
   for shipped names while documenting modern color-mode guidance.
3. Complete and document the class vocabulary, clarify single-token versus compound helpers, and
   correct examples without silently changing existing outputs.
4. Generate package XML documentation and consider broader target frameworks for the helper-only library.
5. Design an optional frontend plugin using Heimdall's managed behavior/command lifecycle. Initialize
   and dispose tooltips/popovers around fragment insertion/removal; consider registered modal,
   offcanvas, and toast commands, including backdrop and focus cleanup.

Bootstrap requires explicit initialization for tooltips and popovers; this is a useful integration
opportunity rather than a reason to make the base class package own application markup or ship a
second Heimdall runtime.

## References

- [Bootstrap navbar color modes](https://getbootstrap.com/docs/5.3/components/navbar/)
- [Bootstrap color utility guidance](https://getbootstrap.com/docs/5.3/utilities/colors/)
- [Bootstrap tooltip initialization](https://getbootstrap.com/docs/5.3/components/tooltips/)
- [Bootstrap popover initialization](https://getbootstrap.com/docs/5.3/components/popovers/)
